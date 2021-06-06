---
title: "Memory Corruption"
date: 2021-05-29T11:09:44-07:00
draft: false
---

# Debugging a memory corruption problem
A memory corruption issue refers to a problem that is caused by the fact that the memory content is wrong. For example, the code is having an access violation because it tried to dereference a pointer that is not valid. Debugging a memory corruption issue is hard because we don't know how the process get into a corrupted state to begin with. In this post, I am going to share my experience debugging a memory issue caused by the GC.

# The symptom
We are hitting an access violation at this location:

```c++
int gc_heap::get_region_plan_gen_num (uint8_t* obj)
{
    return heap_segment_plan_gen_num (region_of (obj));
}
```

Obviously, this is a bug related to regions. One of the key property of region is that all managed object lies within a single range. Therefore we can check.

```
0:011> dv
            obj = 0x0000040a`187ffdc0
```

```
0:011> ?? coreclr!svr::global_region_allocator
class SVR::region_allocator
   +0x000 global_region_start : 0x00000203`40400000
   ...
   +0x010 global_region_left_used : 0x00000205`17000000
   ...
```

It is fairly obvious that `obj` is outside of the region's range, so we confirm `obj` is wrong. 

The parent frame tells us where does `obj` come from

```c++
inline void
gc_heap::check_demotion_helper (uint8_t** pval, uint8_t* parent_obj)
{
#ifdef USE_REGIONS
    uint8_t* child_object = *pval;
    if (!child_object) return;
    int child_object_plan_gen = get_region_plan_gen_num (child_object);
```

So `pval` is pointing to the bad pointer. Looking at `pval` itself, we see:

```
0:011> ?? pval
unsigned char ** 0x00000203`d30d89e8
```

so `pval` itself is valid pointer.

Looking further down the stack does not reveal anything more interesting, it looks like usual heap walking.

# Look around
Without a better guide, let's take a look at the memory around it. What is it supposed to be?

The `!lno` command in SOS is meant for that, now we have:

```
0:011> !lno  0x00000203`d30d89e8
Before:  00000203d30d89e0           32 (0x20)	SimpleRefPayLoad
After:   00000203d30d8a00           40 (0x28)	ReferenceItemWithSize+ReferenceItemWithSizeNonFinalizable
Heap local consistency confirmed.
```

Let's take a look at the object

```
0:011> !do 00000203d30d89e0 
Name:        SimpleRefPayLoad
MethodTable: 00007ffb490d2548
EEClass:     00007ffb490e27a8
Size:        32(0x20) bytes
File:        C:\dev\performance\artifacts\bin\GCPerfSim\release\netcoreapp5.0\GCPerfSim.dll
Fields:
              MT    Field   Offset                 Type VT     Attr            Value Name
00007ffb48f21070  400001e        8        System.Byte[]  0 instance 0000040a187ffdc0 payload
00007ffb48cddf00  400001f       10 ...Services.GCHandle  1 instance 00000203d30d89f0 handle
00007ffb48c9faa8  4000020       88        System.UInt32  1   static               16 FieldSize
00007ffb48c9faa8  4000021       8c        System.UInt32  1   static               32 SimpleRefPayLoadSize
00007ffb48c9faa8  4000022       90        System.UInt32  1   static               56 ArrayOverhead
```

Except from the bad payload pointer, everything else looks normal, including the method table pointer. This suggest that the memory corruption might be caused by a single pointer write. That's is useful information, because we can basically narrow that down to the few operations that GC modifies the managed heap.

> Debugging tip: Looking around the corrupted memory could be helpful to guess what gone wrong.

If I had a magic wand, I would set a data breakpoint on the address of payload and run backwards. It is actually possible with [time travel tracing](https://docs.microsoft.com/en-us/windows-hardware/drivers/debugger/time-travel-debugging-record). Sadly, it is often too slow to be useful.

But we could reuse the idea, what if we just logged the pointer write ourselves? The key difficulty is that the GC writes to memory all the time. If we just logged all the accesses, it would be just as slow and not useful. 

What we needed is an informed guess of what pointer access might caused the corruption. That leads us to the next section.

# Ways that the GC modifies the managed heap?
There are a few operations that the GC do to modify the managed heap:

1. `gcmemcopy` - this move a block of memory from one place to another.
2. `make_unused_array` - this change the beginning of a method table and modify the size so that it looks like an array but it is free.
3. `relocate_address` - this change an address pointing to the old memory to point to the new memory before the compaction will move it, and
4. `set_node_relocation_distance` (and similar calls) - this leverage the space before a plug to store some information related to the plug.

There are, of course, other pointers writes. For example, updating the brick table. But they are outside of the managed heap range. Could that be wrong too? Yes, they could, for example, if we got a wrong brick index value. It is just unlikely and therefore we can rule it out.

1 and 2 modify at least more than one pointer, but the symptom involves only one bad pointer, so we can rule it out. 3 and 4 are suscipious, so I started with 3.

> Debugging tip: Logging can be used to provide the needed missed history.

# Logging the write
The key instrument that we can use is the stress log mechanism. By writing a `dprintf` in the code, this macro will append a record in a thread static list of messages that is backed by a memory mapped page on disk. That will log the message with no contention. The log can be subsequently analyzed by a stress log analyzer that can go through the list and find interesting stuff for us.

This [PR](https://github.com/dotnet/runtime/pull/53547) summarize how I added the instrumentation for the relocation of an address, it should be straightforward to add as needed.

# Wrong relocation
With the logging, I discovered that the issue is caused by a wrong relocation. In particular, the new address is outside of the region's range. But why? We don't know yet. In order to figure it out, it would be nice if we can stop at that moment. Indeed, we can! We can just assert the new address is within range, and whenever that happen, the assert will fire and we will be able to look at the state at that point of time.

> Debugging tip: If we can fail earlier, do so.

After adding the assert, I stopped at the point where the relocation is wrong. That gives me an opportunity to inspect the state of the program when that happen, and that is very useful. Here is some interesting finding:

First of all, here is the code that caused the problem.
```c++
    size_t  brick = brick_of (old_address);
    int    brick_entry =  brick_table [ brick ];
    uint8_t*  new_address = old_address;
    if (! ((brick_entry == 0)))
    {
    retry:
        {
            while (brick_entry < 0)
            {
                brick = (brick + brick_entry);
                brick_entry =  brick_table [ brick ];
            }
            uint8_t* old_loc = old_address;

            uint8_t* node = tree_search ((brick_address (brick) + brick_entry-1),
                                      old_loc);
            if ((node <= old_loc))
                new_address = (old_address + node_relocation_distance (node));
            else
            {
                if (node_left_p (node))
                {
                    dprintf(3,(" L: %Ix", (size_t)node));
                    new_address = (old_address +
                                   (node_relocation_distance (node) +
                                    node_gap_size (node)));
                }
                else
                {
                    brick = brick - 1;
                    brick_entry =  brick_table [ brick ];
                    goto retry;
                }
            }
        }

        dprintf (2, (ThreadStressLog::gcRelocateReferenceMsg(), pold_address, new_address));
        global_region_allocator.validate(new_address);
        *pold_address = new_address;
        return;
    }
```

Given the `new_address` is wrong, one would naturally want to figure out how it is computed. Apparently, it is computed by the `node_relocation_distance` of the `node`. Therefore, we would like to access the `node` variable. However, since we are breaking on the validate line, the node variable is already out of scope, and the debugger refuses to show us the value.

I experimented with making the variable `node` out of the current scope, but somehow that make the reproduction of the bug difficult (for reason I still don't understand), therefore we need to resort to other techniques. I did that by looking at the disassembly. Here I can talk a little about that disassembly technique. Here is the truncated stack:

```
0:009> k
 # Child-SP          RetAddr           Call Site
00 00000034`8787a950 00007ffb`e9c0497a coreclr!DbgAssertDialog+0x34c [C:\dev\runtime\src\coreclr\utilcode\debug.cpp @ 700] 
01 00000034`8787ab90 00007ffb`e9be9823 coreclr!SVR::region_allocator::validate+0x5a [C:\dev\runtime\src\coreclr\gc\gc.cpp @ 3484] 
02 00000034`8787abc0 00007ffb`e9be9e6a coreclr!SVR::gc_heap::relocate_address+0x283 [C:\dev\runtime\src\coreclr\gc\gc.cpp @ 29420] 
```
The return address of the `validate` frame will be a location within the `relocate_address` function, so we can ask for the function disassembly of it.

```
0:009> uf 00007ffb`e9be9823
coreclr!SVR::gc_heap::relocate_address [C:\dev\runtime\src\coreclr\gc\gc.cpp @ 29358]:
29358 00007ffb`e9be95a0 4489442418      mov     dword ptr [rsp+18h],r8d
...
29396 00007ffb`e9be96b4 e857680100      call    coreclr!SVR::tree_search (00007ffb`e9bfff10)
29396 00007ffb`e9be96b9 4889442448      mov     qword ptr [rsp+48h],rax
...
```

It looks like we are storing the result of `tree_search` to `[rsp+48h]`, that is where we will inspect. To do so, we need to figure out the `rsp` value of that frame:

```
0:009> .frame -r 2
...
... rsp=000000348787abc0 ...
...
0:009> dq 000000348787abc0+0x48 L1
00000034`8787ac08  000001c5`58bff468
```

The node relocation distance is obtained just by reinterpreting a few bytes before it as the `plug_and_reloc` structure, so we can look at that.

```
0:009> ?? (svr::plug_and_reloc*)(0x000001c5`58bff468 - 0x18)
struct SVR::plug_and_reloc * 0x000001c5`58bff450
   +0x000 reloc            : 0n1947109160040
   +0x008 m_pair           : SVR::pair
   +0x010 m_plug           : SVR::plug
```

Now it is obvious that the `reloc` is suspiciously high. Using the same idea, we inspect the memory around it.

```
0:009> !lno 0x000001c5`58bff468
Before:  000001c558bff448           32 (0x20)	SimpleRefPayLoad
Current: 000001c558bff468         2968 (0xb98)	System.Byte[]
After:  couldn't find any object between 000001C558BFF468 and 000001C558C00000
Heap local consistency not confirmed.
```

Now it is fairly obvious that somehow we got the `node` wrong. The space before `node` is another object, which means it is not a plug tree root. 

Following our logic, we should take a look at the tree search. But I decided I will give some faith to the existing code there and look at its source, `brick` and `brick_entry`. 

```
0:009> ?? brick
unsigned int64 0n645119
0:009> ?? (old_address - this->lowest_address)/4096
int64 0n645120
0:009> ?? this->brick_table[645120]
short 0n-1
0:009> ?? this->brick_table[645119]
short 0n1129
```

So the search go back by one brick and reach the wrong node. To really understand what is going on, we need to understand what they should be. For a region that is not sweep in plan, the brick should correspond to the root of the plug tree. Since we are relocating an address in a brick, that brick should have at least one plug, therefore we assert that `this->brick_table[645120] == -1` is wrong.

> Debugging tips: Knowing what it should be is just as important as what it is currently.

# Who set my brick wrong?

Now we knew the bricks are wrong, but why? The `brick_table` is only set in the `set_brick` function, but the `set_brick` function is called 23 times throughout the GC code base. Writing a log message at set_brick is meaningless. We need to know exactly where we did it.

To do that, I added an extra argument named `reason` to `set_brick` and make sure each caller pass a different value, then we log the brick table write to the log messages and search for it when we knew we are writing a wrong value to the brick.

To my excitement, I found the culprit, this line setting the brick to a wrong value with reason 12: 

```c++
            set_brick (last_marked_obj_end_b, 
                    (last_marked_obj_start_b - last_marked_obj_end_b), 12);
```

This is exciting because it is new code. We have good reason to believe the old code are most likely fine. In this particular case, we confirm that code is wrong and it results in this [PR](https://github.com/dotnet/runtime/pull/53446) as the fix.

> Debugging tips: If it is a regression, the bug is likely in the new code.

This conclude the investigation of this memory corruption issue.