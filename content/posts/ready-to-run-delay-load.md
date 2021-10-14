---
title: "Ready to Run Delay Load"
date: 2021-10-14T12:27:20-07:00
draft: false
---

In this series of posts, I will deep dive into ReadyToRun to understand its mechanisms. In my [previous post](/posts/introduction-to-ilcompiler-reflection-readytorun), I briefly explained what is ReadyToRun and what problems do we need to solve. Now we will look into the exact mechanisms.

# The example
We will start with this very simple example:

```c#
using System;

namespace ReadyToDebug
{
    class Program
    {
        static void Main(string[] args)
        {
            for (int i = 0; i < 3; i++)
            {
                Console.WriteLine("Hello World!");
            }
        }
    }
}
```

To be precise, I put it in a project targetting .NET 6.0, and compiled using the ready to run option as follow:

`dotnet publish -r win-x64 /p:PublishReadyToRun=true`

Of course, the program simply prints "Hello World" 3 times. 

# The tools
We will use [ILSpy.ReadyToRun](https://github.com/icsharpcode/ILSpy/wiki/ILSpy.ReadyToRun) to disassemble the compiled code, and we will use the [Time Travel Debugging](https://docs.microsoft.com/en-us/windows-hardware/drivers/debugger/time-travel-debugging-record) in WinDBG capability to make debugging easier.

# Disassembling the code
Using ILSpy.ReadyToRun, here is the method disassembled.

```
; void ReadyToDebug.Program.Main(string[])
; Prolog
00000000000017C0 57                   push      rdi
00000000000017C1 56                   push      rsi
00000000000017C2 4883EC28             sub       rsp,28h
; IL_0001
00000000000017C6 33F6                 xor       esi,esi
; IL_0016
00000000000017C8 488B3DD9080000       mov       rdi,[rel 20A8h]
; IL_0006
00000000000017CF 488B0F               mov       rcx,[rdi];
00000000000017D2 FF15C8080000         call      qword [rel 20A0h] ; void [System.Console]System.Console::WriteLine(string)
; IL_0012
00000000000017D8 FFC6                 inc       esi; rsi = Local 0
; IL_0016
00000000000017DA 83FE03               cmp       esi,3
00000000000017DD 0F9CC0               setl      al
00000000000017E0 0FB6C0               movzx     eax,al
; IL_001b
00000000000017E3 85C0                 test      eax,eax
00000000000017E5 75E8                 jne       short 0000`0000`0000`17CFh
; IL_001e
; Epilog
00000000000017E7 4883C428             add       rsp,28h
00000000000017EB 5E                   pop       rsi;
00000000000017EC 5F                   pop       rdi
00000000000017ED C3                   ret
```

First of all, the address column looks weird. We seldom have instruction pointer pointing to such a small number. They are NOT actual addresses, they are relative virtual addresses. The idea is that they are a relative offset from the base address of the image. Later, we will talk about how to do this simple translation when we dive into debugging.

Second, note that ILSpy.ReadyToRun nicely annotated that the call on `00000000000017D2` is going to `Console.WriteLine`. Note that the call is in indirect call, therefore the address of the callee is stored on the memory cell `20A0h`. Note that it has to be indirect, because by the time we compile the application, we have no idea where will the code for `Console.WriteLine` lives. We call such a location a redirection cell. Basically, a memory location where we can write to it to redirect execution. 

Given that even the compiler don't know where the function is, how ILSpy.ReadyToRun knows it *will* lead to `Console.WriteLine`. This is the real ReadyToRun magic.

Without further ado, let's dive into the debugging.

# Recording the trace
First, we run this command:

`c:\debuggers\amd64\ttd\TTTracer.exe c:\ReadyToDebug\bin\Debug\net6.0\win-x64\publish\ReadyToDebug.exe`

This will record the execution of the program and allow us to move back-and-forth to understand the execution. Then, we can start inspecting the trace as follow:

`c:\debuggers\amd64\windbg -z ReadyToDebug01.run`

This will show the normal debugging session, and we will start debugging it.

# Analyzing the trace
We would like to know what happened on the indirect calls. So naturally we would like to set a breakpoint to it. Right now, `ReadyToDebug.dll` is not even loaded in the process yet. So the first thing to do is to run until it is loaded. We can do that using this command

```
0:000> sxe ld ReadyToDebug
0:000> g
```

This will run until `ReadyToDebug.dll` is loaded, and when it does, the debugger shows:

```
...
ModLoad: 000001e2`f7b50000 000001e2`f7b54000   c:\Garbage\ReadyToDebug\bin\Debug\net6.0\win-x64\publish\ReadyToDebug.dll
```

The first address, `000001e2f7b50000`, is the base address of the module. It may or may not be the same on other machines (and even executions on the same machine). But once we have this address, then we have this very simple formula:

```
virtual address = base address + relative virtual address
```

The virtual refers to virtual memory (which is used in practically any OS today). So if we ignore the word virtual, this is basically saying the address is the base address plus the relative address. Therefore, or call site is simply `000001e2f7b50000 + 17d2 = 000001e2f7b517d2`. We will set a breakpoint there.

```
0:000> bp 000001e2f7b517d2
0:000> g
...
Breakpoint 0 hit
Time Travel Position: 171F7:9 [Unindexed] Index
...
0:000> g
...
Breakpoint 0 hit
Time Travel Position: 1A995:16 [Unindexed] Index
...
0:000> g
Breakpoint 0 hit
Time Travel Position: 1A9A1:16 [Unindexed] Index
...
0:000> g
TTD: End of trace reached.
...
```

As we can see, the breakpoint hit 3 times, and then the process terminates. Of course, that matches our expectation that `Console.WriteLine` is called 3 times. 

# The first call
Now we will go back in time to see what happened in the first call.

```
0:000> !tt 171F7:9
Setting position: 171F7:9
0:000> dq 000001e2`f7b520a0 L1
000001e2`f7b520a0  000001e2`f7b51804
0:000> u 000001e2`f7b51804
000001e2`f7b51804 33c0            xor     eax,eax
000001e2`f7b51806 6a03            push    3
000001e2`f7b51808 ff3572080000    push    qword ptr [ReadyToDebug!COM+_Entry_Point <PERF> (ReadyToDebug+0x2080) (000001e2`f7b52080)]
000001e2`f7b5180e ff2574080000    jmp     qword ptr [ReadyToDebug!COM+_Entry_Point <PERF> (ReadyToDebug+0x2088) (000001e2`f7b52088)]
```

Remember we said the address to the actual call site is stored in `20a0`? We inspected the call site and found this code. This code is weird, we do not setup a frame and just push and jump. This pattern is often called a trunk or a trampoline. Roughly speaking, this is not a function, but a decorator of the function. If we think about the decorator pattern, you can use a decorator to execute something before of after a method. This is exactly what we are trying to do here.

The key thing to notice here is that we set `eax` to 0 and pushed two more values onto the stack. Overall, the stack remain unchanged. So the argument to the target function and the return address are still there! 

Next, we look at the last jump.

```
0:000> dq 000001e2`f7b52088 L1
000001e2`f7b52088  00007ffd`39f349a0
0:000> uf 00007ffd`39f349a0
coreclr!DelayLoad_MethodCall:
00007ffd`39f349a0 4155            push    r13
00007ffd`39f349a2 4154            push    r12
00007ffd`39f349a4 55              push    rbp
00007ffd`39f349a5 53              push    rbx
00007ffd`39f349a6 56              push    rsi
00007ffd`39f349a7 57              push    rdi
00007ffd`39f349a8 4883ec68        sub     rsp,68h
00007ffd`39f349ac 48898c24b0000000 mov     qword ptr [rsp+0B0h],rcx
00007ffd`39f349b4 48899424b8000000 mov     qword ptr [rsp+0B8h],rdx
00007ffd`39f349bc 4c898424c0000000 mov     qword ptr [rsp+0C0h],r8
00007ffd`39f349c4 4c898c24c8000000 mov     qword ptr [rsp+0C8h],r9
00007ffd`39f349cc 660f7f442420    movdqa  xmmword ptr [rsp+20h],xmm0
00007ffd`39f349d2 660f7f4c2430    movdqa  xmmword ptr [rsp+30h],xmm1
00007ffd`39f349d8 660f7f542440    movdqa  xmmword ptr [rsp+40h],xmm2
00007ffd`39f349de 660f7f5c2450    movdqa  xmmword ptr [rsp+50h],xmm3
00007ffd`39f349e4 4c8b8c2498000000 mov     r9,qword ptr [rsp+98h]
00007ffd`39f349ec 4c89b42498000000 mov     qword ptr [rsp+98h],r14
00007ffd`39f349f4 4c8b8424a0000000 mov     r8,qword ptr [rsp+0A0h]
00007ffd`39f349fc 4c89bc24a0000000 mov     qword ptr [rsp+0A0h],r15
00007ffd`39f34a04 488d4c2468      lea     rcx,[rsp+68h]
00007ffd`39f34a09 488bd0          mov     rdx,rax
00007ffd`39f34a0c e85f4decff      call    coreclr!ExternalMethodFixupWorker (00007ffd`39df9770)
00007ffd`39f34a11 660f6f442420    movdqa  xmm0,xmmword ptr [rsp+20h]
00007ffd`39f34a17 660f6f4c2430    movdqa  xmm1,xmmword ptr [rsp+30h]
00007ffd`39f34a1d 660f6f542440    movdqa  xmm2,xmmword ptr [rsp+40h]
00007ffd`39f34a23 660f6f5c2450    movdqa  xmm3,xmmword ptr [rsp+50h]
00007ffd`39f34a29 488b8c24b0000000 mov     rcx,qword ptr [rsp+0B0h]
00007ffd`39f34a31 488b9424b8000000 mov     rdx,qword ptr [rsp+0B8h]
00007ffd`39f34a39 4c8b8424c0000000 mov     r8,qword ptr [rsp+0C0h]
00007ffd`39f34a41 4c8b8c24c8000000 mov     r9,qword ptr [rsp+0C8h]
00007ffd`39f34a49 4883c468        add     rsp,68h
00007ffd`39f34a4d 5f              pop     rdi
00007ffd`39f34a4e 5e              pop     rsi
00007ffd`39f34a4f 5b              pop     rbx
00007ffd`39f34a50 5d              pop     rbp
00007ffd`39f34a51 415c            pop     r12
00007ffd`39f34a53 415d            pop     r13
00007ffd`39f34a55 415e            pop     r14
00007ffd`39f34a57 415f            pop     r15
00007ffd`39f34a59 e93cffffff      jmp     coreclr!ExternalMethodFixupPatchLabel (00007ffd`39f3499a)  Branch
```

First of all, note that this is a function in coreclr.dll. That means by the time our application is compiled, we have no idea where is the function. That's why it must be done through an indirect call.

This functions looks complicated, but all it really does is pushing stuff to the stack, call the `ExternalMethodFixupWorker` with some arguments, and then pop them back all, and jump somewhere else.

Note that at the end we popped `r14` and `r15` which is not pushed. This is to make sure we balance out the two additional pushes before we reach `DelayLoad_MethodCall`. By the time we jump, the stack will be exactly as it was at the callsite.

Looking at just the stack, this is simply another trunk. The key is what `ExternalMethodFixupWorker` to prepare the `ExternalMethodFixupPatchLabel`.

Note that the last jump is NOT an indirect jump, all it does is simply jumping to where `rax` points to:

```
0:000> u 00007ffd`39f3499a
coreclr!ExternalMethodFixupPatchLabel:
00007ffd`39f3499a 48ffe0          jmp     rax
```

`rax` is of course the return value of ExternalMethodFixupWorker.

# ExternalMethodFixupWorker
The function `ExternalMethodFixupWorker` is implemented in C++, so we can simply read the source code. It is a long function so we will only outline what it does. The code uses the address of the indirection cell to find a signature. Using the signature, it asks the rest of the runtime to prepare for that function and return the address of that code.

Why a signature? A signature is basically the name of the method in binary form. When we ask the runtime for the code, we need to give it an identifier of the method, and the signature is such an identifier.

We also notice that there is a way to find the signature from the indirection cell address. That is how ILSpy.ReadyToRun figure out where the call will lead to.

Further downstream, we will simply run the `Console.WriteLine` implementation, so we will skip that.

# The third call
Now you might wonder, why do I call the function 3 times to show you how a method call is done? The reason is that calling `ExternalMethodFixupWorker` is expensive, and we don't want want to call it repeatedly after we already know where it should be. Let's inspect the 3rd call now.

```
0:000> !tt 1A9A1:16
0:000> dq 000001e2`f7b520a0 L1
000001e2`f7b520a0  00007ffc`da3a2a68
0:000> u 00007ffc`da3a2a68
00007ffc`da3a2a68 e9effaffff      jmp     00007ffc`da3a255c
```

The code changed! This is why we called it multiple times. At the third call, we no longer do the `DelayLoad_MethodCall` work and instead just jump directly to the implementation. Something must have changed the code!

```
0:000> !ttpw 000001e2`f7b520a0
Time Travel Position: 1A995:217 [Unindexed] Index
0:000> k
 # Child-SP          RetAddr               Call Site
00 (Inline Function) --------`--------     coreclr!PatchNonVirtualExternalMethod+0x9c [D:\a\_work\1\s\src\coreclr\vm\prestub.cpp @ 2484] 
01 000000b9`cd77e300 00007ffd`39f34a11     coreclr!ExternalMethodFixupWorker+0xaf3 [D:\a\_work\1\s\src\coreclr\vm\prestub.cpp @ 2793] 
...
```

Now it is clear that `ExternalMethodFixupWorker` takes care of changing the indirection cell so that we don't do the heavy work to find the method again.

The timestamp of this change is `1A995:217`, which is a bit later than the second call at `1A995:16`. Therefore we are actually doing `DelayLoad_MethodCall` twice and the patch happen at the second call to `ExternalMethodFixupWorker` for the same redirection cell. This is why I need to run it 3 times instead of just twice. 

The exact reason why we need to call it twice instead of just once is due to `ThePreStub` and tiered compilation. We will skip that for now.

This conclude our journey to understand how the ready to run code calls an external method.