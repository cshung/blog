---
title: "False Memory Leak"
date: 2021-08-13T12:18:40-07:00
draft: false
---
I am writing this post to share a memory leak investigation which turns out to be a false positive (i.e. we are not having a managed memory leak). This goal of this document is to share the analysis method so that it could be applied to similar situations.

# Highlights
Before I go into the details, here is a one line summary of the conclusion. This case is *NOT* a managed memory leak, but because a compiler generated temp is considered a reference and therefore the garbage collector cannot eliminate it. It is unfortunate that this relatively simple code is not freeing the objects as expected.

# Context
One customer reported a memory leak on Mono [here](https://github.com/mono/mono/issues/20009) and he claimed that it also reproduce on CoreCLR. Therefore, I tried to reproduce the issue and investigate it.

# The repro code
In a nutshell, the repro:
1. Setup a linked list of length 1 with a distinguished type (`RootNode`). The nodes are pointing from `Tail` to `Head`.
2. Setup and start a thread to remove the list node from the `Tail` end.
3. Insert 10 nodes into the linked list started from the `Head` end.
4. Wait until the `List` contains just one node.
5. Optionally, make sure the work thread completes, force a gen 2 GC, and take a heap dump.

The code is a bit too long to be embedded here, the full project is available [here](https://github.com/cshung/blog-samples/tree/main/FalseLeakRepro). It is edited from the original one.

The expectation is that since the worker thread eliminated all but one node, we should have exactly one node left on the heap when we generate the heap dump.

# The repro steps
To reproduce the issue, we need a way to observe how many objects are there still alive on the heap. To do that, we first build the project. For my convenience, this investigation is done on Windows, x64. But I expect exactly the same happens on Linux ARM as well.

```
C:\FalseLeakRepro> dotnet build
C:\FalseLeakRepro> windbg bin\Debug\net5.0\FalseLeakRepro.exe
```

In the debugger, we let the process go. To make sure the objects goes away, I used 'R', 'G', 'H'. That make sure the work thread terminates and we forced full blocking gen 2 GC. Then we take a look at the heap using the `!DumpHeap` SOS extension command.

```
0:000> !DumpHeap
...
00007ffdce947768       10          320 FalseLeakRepro.Node
...
```

So we have reproduced the problem! All the 10 nodes are still on the heap after forcing the GC.

# Analysis
Next, we wanted to understand why they are still on the heap. We can use the `!gcroot` SOS extension command to do that. First, we take a look at the individual objects with this type:

```
0:000> !DumpHeap -mt 00007ffdce947768
         Address               MT     Size
0000025a2e2536c0 00007ffdce947768       32     
0000025a2e2536f8 00007ffdce947768       32     
0000025a2e253730 00007ffdce947768       32     
0000025a2e253768 00007ffdce947768       32     
0000025a2e2537a0 00007ffdce947768       32     
0000025a2e2537d8 00007ffdce947768       32     
0000025a2e253810 00007ffdce947768       32     
0000025a2e253848 00007ffdce947768       32     
0000025a2e253880 00007ffdce947768       32     
0000025a2e2538b8 00007ffdce947768       32     

Statistics:
              MT    Count    TotalSize Class Name
00007ffdce947768       10          320 FalseLeakRepro.Node
Total 10 objects
...
```

This gave us the addresses to the 10 objects, now we pick a random one (to avoid picking the `Head` or `Tail` which is just not interesting) and ask for the GC root.

```
0:000> !gcroot 0000025a2e2537d8
Thread 464c:
    000000C7FC57E170 00007FFDCE876E03 FalseLeakRepro.Program.Run(System.String[]) [C:\dev\blog-samples\FalseLeakRepro\Program.cs @ 124]
        rbp-a0: 000000c7fc57e240
            ->  0000025A2E2533E8 FalseLeakRepro.RootNode
            ->  0000025A2E2536C0 FalseLeakRepro.Node
            ->  0000025A2E2536F8 FalseLeakRepro.Node
            ->  0000025A2E253730 FalseLeakRepro.Node
            ->  0000025A2E253768 FalseLeakRepro.Node
            ->  0000025A2E2537A0 FalseLeakRepro.Node
            ->  0000025A2E2537D8 FalseLeakRepro.Node

Found 1 unique roots (run '!gcroot -all' to see all roots).
```

Now the debugger output tells us that the node is alive because of this chain of references. The most important one being the `RootNode`. If we think about it, the root node should be the first one that got removed in the worker thread, why does it still there? The debugger tells us it is `rbp-a0` of the `Run` method stack frame. 

The stack frame is supposed to have parameters, return address and local variables. Run has no parameters (except the `this` pointer), the return address cannot be the `RootNode`, and we don't have a local variable of type `RootNode`. So what is going on here?

# Looking at JITDump
While we could look at the disassembly and try to figure it out, it is much more informational to let the JIT tells us what is going on with [`JITDump`](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/jit/viewing-jit-dumps.md). We need to patch the runtime with a debug build, and enable dumping the method with the following environment variable:

```
set COMPLUS_JITDump=FalseLeakRepro.Program:Run(System.String[]):this
```

Here are the relevant parts - the full JITDump can be found [here](https://raw.githubusercontent.com/cshung/blog-samples/main/FalseLeakRepro/debug.bug.jitdump.txt):

```
IL_0024  7d 05 00 00 04    stfld        0x4000005
IL_0029  02                ldarg.0     
IL_002a  7b 05 00 00 04    ldfld        0x4000005
IL_002f  7b 03 00 00 04    ldfld        0x4000003
IL_0034  73 0c 00 00 0a    newobj       0xA00000C
...
STMT00011 (IL 0x029...  ???)
               [000039] -A----------              *  ASG       ref   
               [000038] D------N----              +--*  LCL_VAR   ref    V23 tmp5         
               [000037] ------------              \--*  ALLOCOBJ  ref   
               [000036] H-----------                 \--*  CNS_INT(h) long   0x7ffdb5ee58b8 token
Marked V23 as a single def local

lvaSetClass: setting class for V23 to (00007FFDB5EE58B8) System.Object  [exact]
 0A00000C
In Compiler::impImportCall: opcode is newobj, kind=0, callRetType is void, structSize is 0

lvaGrabTemp returning 24 (V24 tmp6) called for impAppendStmt.


STMT00013 (IL   ???...  ???)
               [000043] -A-XG-------              *  ASG       ref   
               [000042] D------N----              +--*  LCL_VAR   ref    V24 tmp6         
               [000035] ---XG-------              \--*  FIELD     ref    Head
               [000034] ---XG-------                 \--*  FIELD     ref    _myList
               [000033] ------------                    \--*  LCL_VAR   ref    V00 this         
Marked V24 as a single def temp
...
;  V24 tmp6         [V24    ] (  1,  1   )     ref  ->  [rbp-A0H]   do-not-enreg[] must-init class-hnd "impAppendStmt"
...
```

The right way to read this is to start from the bottom. We have a variable internally named `V24` on `rbp-A0H`. `V24` is introduced when trying to compile the expression tree that get `this._myList.Head`, and this is because the IL source on `IL_0029` is asking for it.

ILSpy can be an awesome tool to map IL back to C# source code using the IL with C# view. Here is the relevant part:

```
	// _myList.Head.Data = new object();
	IL_0029: ldarg.0
	IL_002a: ldfld class FalseLeakRepro.List FalseLeakRepro.Program::_myList
	IL_002f: ldfld class FalseLeakRepro.Node FalseLeakRepro.List::Head
	IL_0034: newobj instance void [System.Runtime]System.Object::.ctor()
	IL_0039: stfld object FalseLeakRepro.Node::Data
```

So now it is crystal clear that this line in the C# code is causing the JIT to generate a temp on that stack. When that line is executed, the address of `RootNode` is written to `rbp-0xA0`, and it is never overridden, so by the time we execute a gen2 GC, it cannot remove the root node because there is still a reference to it.

# Why does a compiler generated temp counts?
This particular behavior is actually expected in debug mode. In debug mode, all the variables are defined to be untracked, meaning that they will be considered live throughout the whole method. This is by design to make debugging easier. It would be a hassle to see if a certain variable cannot be inspected during debugging while the method is still running. 

Also, in debug mode, you want to iterate with the code fast. By leaving all the variables (even the compiler generated temps) untracked, we can potentially JIT the code faster.

In the view of this, let's experiment with a release build. By repeating the steps: I confirmed that the memory leak is gone.

```
0:000> !DumpHeap
...
00007ffdcdc06508        1           32 FalseLeakRepro.Node
...
```
# Optimized JITDump
It remains the analyze what happen under the optimized build. 

Here are the relevant parts - the full JITDump can be found [here](https://raw.githubusercontent.com/cshung/blog-samples/main/FalseLeakRepro/release.jitdump.txt):

```
lvaGrabTemp returning 10 (V10 tmp3) called for impAppendStmt.


STMT00007 (IL   ???...  ???)
               [000030] -A-XG-------              *  ASG       ref   
               [000029] D------N----              +--*  LCL_VAR   ref    V10 tmp3         
               [000022] ---XG-------              \--*  FIELD     ref    Head
               [000021] ---XG-------                 \--*  FIELD     ref    _myList
               [000020] ------------                    \--*  LCL_VAR   ref    V00 this         
Marked V10 as a single def temp
...
Generating: N091 (  1,  1) [000031] ------------        t31 =    LCL_VAR   ref    V10 tmp3         u:2 rdi (last use) REG rdi <l:$248, c:$94>
                                                              /--*  t31    ref    
Generating: N093 (  2,  2) [000287] ------------       t287 = *  LEA(b+16) byref  REG rcx
							V10 in reg rdi is becoming dead  [000031]
...
;  V10 tmp3         [V10,T14] (  2,  4   )     ref  ->  rdi         class-hnd single-def "impAppendStmt"
...
```

We have the same temp defined, this time it is called `V10`. With an optimizing compiler, we are assigning it to a register `rdi`. To perform optimal register allocation, the compiler tracks when the value is defined, used, and unneeded. The compiler is able to detect `V10` is dead after generating `N093`, that's why it is able to repurpose `rdi` to store something else later, and it is also why the problem is gone because `rdi` no longer store the reference to the `RootNode`. 

# Does optimized build always work?
In this particular case, we are lucky that the compiler reused that register. In fact, even when the register is not reused, by virtue of the fact that the JIT discovered that register is dead, it would have emitted the information to the runtime to stop reporting rdi as live to the garbage collector when it ask for live slots.

But it is up to the compiler's internal implementation. The compiler must report a slot as live if there are actually live, but it doesn't have to report a slot is dead exactly when it is dead. For example, when there are a lot of local variables and temps, we have to spill them to the stack anyway, and the JIT tends to be less conservative with stack space usage and untrack the variable instead in favor for JIT speed. So your miles may vary depending on the exact situation.

# How to ensure things work?
In this particular case, we could make things work, by ensuring the `Run` method never access the `RootNode`. To do that, we make helper functions to access them, and make sure the JIT cannot inline them. This way we can ensure the stack frame of `Run` will never contain an instance of the `RootNode` and therefore the GC will always be able to collect it. Note that this will work even in debug mode as well.

# Conclusion
This is an in depth investigation into why we are not able to collect the `Node` objects. The investigation demonstrate how to investigate an issue like this. It also explained why does things works the way they were, and provided practical fixes for it.

To reiterate, this is not a managed memory leak. The JIT is free to extend the lifetime of objects within a scope therefore prevent the GC from collecting those objects.