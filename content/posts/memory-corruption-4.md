---
title: "Memory Corruption (4)"
date: 2023-05-21T10:01:16-07:00
draft: false
---

# An object reference pointing to outside of gen 1
In this post, I will talk about yet another memory corruption bug we found and fixed. Check out my [previous post](/posts/memory-corruption-3) for more context and examples.

# Symptom
As reported in [this issue](https://github.com/microsoft/FASTER/issues/835), we are hitting random crashes when running the FASTER database. Their developers kindly narrowed it down into a simple repro.

# A few observations
- It crashes quickly on some machines, but on some others, it doesn't.
- It crashes on .NET 7, but not on .NET 8. Some other reports it does also crash on .NET 8.
- When it crashes, we are trying to mark an object, but the object is invalid, it is just a bunch of zeros.
- The bad object seems to be always slightly after gen 1 of one of the heaps.

# Time travel tracing
Since the repro is short in duration, we can use the time travel tracing to see what happened. Since the bug does not repro on my machine, Mukund kindly helped to provide a trace. Time travel trace is awesome for the purpose of investigating memory corruption, because it allows us to see the entire history of the process, so we can get a full picture of what happened.

# Time 0: Bad marking happened
Without knowing what gone wrong in the past, let's look at the crime scene first, at the moment of crash, we have this stack trace:

```txt
coreclr!SVR::CObjectHeader::IsMarked
coreclr!SVR::mark_queue_t::queue_mark+0x39
coreclr!SVR::gc_heap::mark_object_simple+0x66
coreclr!SVR::gc_heap::mark_through_cards_helper+0x5a
coreclr!SVR::gc_heap::mark_through_cards_for_uoh_objects+0x5b7
...
```

The crash happened when we are trying to mark an object. Let's take a look at the object:

```txt
0:010> .frame 2
0:010> ?? o
unsigned char * 0x0000021c`1fa71090
0:010> dq 0x0000021c`1fa71090
0000021c`1fa71090  00000000`00000000 00000000`00000000
0000021c`1fa710a0  00000000`00000000 00000000`00000000
0:010> !eeheap -gc
...
Heap 19 (0000021B935E8A30)
...
generation 1:
         segment             begin         allocated         committed    allocated size    committed size
0000021B90041078  0000021C1F800020  0000021C1FA5A1C0  0000021C1FA5D000  0x25a1a0(2466208)  0x25cfe0(2478048)
```

We need to get back the frame 2 just because the earlier frames are inlined so no debug info is available there. As we expected, the pointer is bad, it is just a bunch of zero, and it is outside of gen 1 of heap 19. Note that the bad object `0x0000021c1fa71090` is close to the allocated `0000021C1FA5A1C0`, but it is larger, so it is not considered allocated. Therefore we have a case of a pointer pointing to somewhere not allocated.

In this trace, we turned off the mark prefetch so that the marking is never delayed. Now we can use the stack to see the parent object leading to the marking of the bad object.

```txt
0:010> .frame 4
0:010> ?? o
unsigned char * 0x0000021b`fc400040
0:010> !do 0x0000021b`fc400040
Name:        <error>
MethodTable: 00007fff807b6368
EEClass:     00007fff8050a9d0
Tracked Type: false
Size:        131096(0x20018) bytes
Array:       Rank 1, Number of elements 16384, Type CLASS (Print Array)
Fields:
None
```

The parent object is a big array, presumably one of its entry is the bad object. Let's take a look at the array:

```txt
0:010> dq 0x0000021b`fc400040
0000021b`fc400040  00007fff`807b6368 00000000`00004000
0000021b`fc400050  0000021c`1fa70d90 0000021c`1fa70dc0
0000021b`fc400060  0000021c`1fa70df0 0000021c`1fa70e20
```

As usual, the array starts with a method table pointer `00007fff807b6368`, and then a size `0000000000004000`, and then the rest are array entries. Note the regularity of the array entries, it must be the case that the allocation was done so fast that we simply bumped the pointer very quickly without any interruption. Now we can use some simple arithmetic to see where the bad pointer is in the array.

```txt
0:010> dq 0000021b`fc4000d0 L1
0000021b`fc4000d0  0000021c`1fa71090
```

Now we have found the memory location storing the bad pointer.

# Time -1: The bad pointer is populated

Now we know the bad pointer is stored in the array, but who put a bad pointer there. Let's go back in time and see how that happened.

```txt
0:010> !ttpw 0000021b`fc4000d0
Searching for all writes at 0x21bfc4000d0.
(96b8.b268): Break instruction exception - code 80000003 (first/second chance not available)
Time Travel Position: 15A5E3:1131
0:010> k
coreclr!SVR::gc_heap::relocate_address+0xf8
coreclr!SVR::gc_heap::mark_through_cards_helper+0x5a
coreclr!SVR::gc_heap::mark_through_cards_for_uoh_objects+0x5b7
coreclr!SVR::gc_heap::relocate_phase+0x20a
coreclr!SVR::gc_heap::plan_phase+0x205c
...
```

So it is clear, during the relocate phase, we relocated the pointer to the bad object, but why? The relocation is calculated by finding the plug tree root and then read the offset. We see something suspicious here:

```txt
0:010> dv
...
    old_address = 0x0000021c`21a70ed0 "???"
           node = 0x0000021c`21a58020 "???"
```

The `old_address` and `node` are too far apart, indicating a possibly wrong `node` is found. The `node` is found using the bricks, so let's take a look at the brick:

```txt
0:010> ?? svr::gc_heap::g_heaps[0]->brick_table[(old_address - coreclr!svr::gc_heap::g_heaps[0]->lowest_address)/4096]
short 0n-1
```

> The expression is meant for simply `brick_of`.

This is bad, as we knew we have many small objects allocated around it, there is no way we don't have a plug tree root within the brick. In retrospect, exactly the same happened in [this](/posts/memory-corruption-1) earlier case, although this time around we have a different reason for having a bad brick.

# Time -2: The bad brick is populated

Now we know the brick is bad, but who populated it? Let's go back in time again.

```txt
0:010> ?? &svr::gc_heap::g_heaps[0]->brick_table[(old_address - coreclr!svr::gc_heap::g_heaps[0]->lowest_address)/4096]
short * 0x0000021b`880cc520
0:010> !ttpw 0x0000021b`880cc520
Searching for all writes at 0x21b880cc520.
(96b8.9e20): Break instruction exception - code 80000003 (first/second chance not available)
Time Travel Position: 15A4C0:266B
0:025> k
coreclr!SVR::gc_heap::set_brick+0x7
coreclr!SVR::gc_heap::update_brick_table+0x70
coreclr!SVR::gc_heap::plan_phase+0x839
...
```

Not surprisingly, the brick is populated during by the `set_brick` function, looking back in the code, the setting of the brick is because we believe there are no marked object between `plug_end` and `x`.
```txt
0:025> ?? plug_end
unsigned char * 0x0000021c`21a5a000
0:025> ?? x
unsigned char * 0x0000021c`21bfff00
```

But that's wrong. The object we cared about, `0x0000021c21a70ed0` is right between this two pointers. So either it is not marked, or the code determined that there are no marked object between two pointers is wrong. Using a simple check, we determined that it is the latter case.

```txt
0:025> dq 0x0000021c21a70ed0 L1
0000021c`21a70ed0  00007fff`807b62d9
```

The method table of that object still has mark bit on, so it was indeed marked.

Stepping a few calls backward, we found that `find_next_marked` returns the end of the region while its input was `plug_end`. That tells us somewhere inside the mark list processing, it is gone.

# Time -4: It was there!

We know that the object is marked, it must have been marked in some time in the past. Let's see how it was marked.

```txt
0:000> !ttpw 0x0000021c21a70ed0
Searching for all writes at 0x21c21a70ed0.
(96b8.a32c): Break instruction exception - code 80000003 (first/second chance not available)
Time Travel Position: 1582BA:104E
0:014> k
 # Child-SP          RetAddr               Call Site
00 (Inline Function) --------`--------     coreclr!Object::RawSetMethodTable [C:\runtime-2\src\coreclr\vm\object.h @ 148]
01 (Inline Function) --------`--------     coreclr!SVR::CObjectHeader::SetMarked+0x4 [C:\runtime-2\src\coreclr\gc\gc.cpp @ 4562]
02 (Inline Function) --------`--------     coreclr!SVR::mark_queue_t::queue_mark+0x3f [C:\runtime-2\src\coreclr\gc\gc.cpp @ 24162]
03 (Inline Function) --------`--------     coreclr!SVR::mark_queue_t::queue_mark+0x79 [C:\runtime-2\src\coreclr\gc\gc.cpp @ 24182]
04 000000e8`8fb7efc0 00007fff`dff85695     coreclr!SVR::gc_heap::mark_object_simple1+0x3ac [C:\runtime-2\src\coreclr\gc\gc.cpp @ 24297]
05 000000e8`8fb7f070 00007fff`dff84f77     coreclr!SVR::gc_heap::mark_object_simple+0x395 [C:\runtime-2\src\coreclr\gc\gc.cpp @ 24843]
06 (Inline Function) --------`--------     coreclr!SVR::gc_heap::mark_through_cards_helper+0x5a [C:\runtime-2\src\coreclr\gc\gc.cpp @ 37985]
...
```

Not surprisingly, the object was marked in the mark phase. At frame 4 we have this code:
```c++
go_through_object_cl (method_table(oo), oo, s, ppslot,
{
    uint8_t* o = mark_queue.queue_mark(*ppslot, condemned_gen);
    if (o != nullptr)
    {
        if (full_p)
        {
            m_boundary_fullgc (o);
        }
        else
        {
            m_boundary (o);
        }
```

Since we are marking card, this cannot be a full GC, so we are calling `m_boundary`. Let's see what it does:

```txt
#define m_boundary(o) {if (mark_list_index <= mark_list_end) {*mark_list_index = o;mark_list_index++;} else {mark_list_index++;}}
```

So we are indeed putting it into the mark list, in fact we can easily check. After stepping for a few more instructions, it is in the mark list.

```txt
0:014> ?? this->mark_list[16]
unsigned char * 0x0000021c`21a70ed0
```

So we are sure, between `t = -4` and `t = -2`, there must be a `t = -3` where the object is silently dropped, but when? It turns out that we are doing quite a lot to the mark list between these two times, and for brevity for this post, I will just outline them.

- At the end of the `mark_phase`, we sort them. Since marking is a process that follow pointers, the mark list is not sorted, we want to sort it so that it can be used easily.
- After it is sorted, we knew that pointers belongs to the same region are adjacent to each other, so contigous sub array of the mark list correspond to the same region.
- But we have multiple mark lists by different heaps, so there could be multiple pieces of the same region, we need to merge them.

# Time -3: get_region_mark_list

The merging is done by `get_region_mark_list`. At that point, we are trying to find all the pieces and merge them into a single list so that the `find_next_marked` can use. The merging is done by appending pieces to an output buffer, here is the code:

```c++
void gc_heap::append_to_mark_list (uint8_t **start, uint8_t **end)
{
    size_t slots_needed = end - start;
    size_t slots_available = mark_list_end + 1 - mark_list_index;
    size_t slots_to_copy = min(slots_needed, slots_available);
    memcpy(mark_list_index, start, slots_to_copy*sizeof(*start));
    mark_list_index += slots_to_copy;
    dprintf (3, ("h%d: appended %zd slots to mark_list\n", heap_number, slots_to_copy));
}
```

But here is a problem, it looks like there is a possibility that the mark list is already full, and we silently drop the suffix of the input!

In fact, this is the bug. When we try to `append_to_mark_list` for the input range containing `0x0000021c21a70ed0`, the `mark_list` is already full at that point, and so we skipped it.

With a truncated `mark_list`, now it is clear why `find_next_marked` cannot find the object, and therefore we have the wrong brick and finally the wrong pointer!

# Fix
The fix is simple. The code already have provision to disable the mark list optimization by setting `use_mark_list` to `false`. We just need to make sure by the end of `get_region_mark_list`, we set it back to `false` in case the `mark_list` is overflowed. With that fix, we are able to run the repro without crashes! This conclude the investigation of this memory corruption bug.