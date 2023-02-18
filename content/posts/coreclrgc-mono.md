---
title: "Debugging help for CoreCLR GC - Mono"
date: 2020-12-04T11:52:15-08:00
draft: false
---
I was invited to help with debugging an issue in the CoreCLR GC - Mono project. In this blog entry, I am planning to describe the debugging process so that it can be repeated to move the project forward.

## What is CoreCLR GC - Mono?
CoreCLR GC - Mono is an attempt to use CoreCLR's GC to replace `sgen`, a simple generational garbage collector for Mono. The major goal of the project is to improve performance.

## Where are we?
We have a [PR branch](https://github.com/dotnet/runtime/pull/43512) from [Nathan Ricci](https://github.com/naricc) that compiles successfully on OSX. I have rebased the branch so that it is based on a more recent version of the runtime repo. [Here](https://github.com/cshung/runtime/tree/private/coreclrgc-mono) is mine. The branch contains all the original commits from Nathan and a commit from me that fixes some problems.

## How to use the branch?
The branch can be built using this command at the `<REPO-ROOT>`:

`./build.sh --subset mono+libs /p:MonoCoreGC=true`

Once the repo is built, we can test the Hello World scenario as follow:

```bash
cd ./src/mono/netcore/sample/HelloWorld
# To run the project, or
make run
# To debug the project
make debug
```

As of now, the run will crash on the induced GC due to this stack. The rest of the blog is to explain why we have a crash and suggests some fixes for it.

## Symptoms

Here is the stack of the crash.
```txt
* thread #1, name = 'tid_307', queue = 'com.apple.main-thread', stop reason = signal SIGSEGV
  * frame #0: 0x00000001031c0270 libcoreclr.dylib`MethodTable::GetBaseSize(this=0x0000000000000010) at gcenv.mono.h:73:72
    frame #1: 0x00000001031c0125 libcoreclr.dylib`WKS::my_get_size(ob=0x000000010376f8a0) at gc.cpp:9707:26
    frame #2: 0x00000001031de98e libcoreclr.dylib`WKS::gc_heap::find_first_object(start="p?\x01", first_object="`\x11V\x03\x01") at gc.cpp:30529:33
    frame #3: 0x00000001031de655 libcoreclr.dylib`WKS::gc_heap::find_object(interior="p?\x01") at gc.cpp:19250:26
    frame #4: 0x00000001031e4a19 libcoreclr.dylib`WKS::GCHeap::Promote(ppObject=0x00007ffeefbf8d08, sc=0x00007ffeefbf7a78, flags=3) at gc.cpp:37497:18
    frame #5: 0x00000001031b456b libcoreclr.dylib`GCToEEInterface::GcScanRoots(fn=(libcoreclr.dylib`WKS::GCHeap::Promote(Object**, ScanContext*, unsigned int) at gc.cpp:37467), condemned=0, max_gen=2, sc=0x00007ffeefbf7a78)(Object**, ScanContext*, unsigned int), int, int, ScanContext*) at coregc-mono.cpp:1540:4
    frame #6: 0x0000000103213eb9 libcoreclr.dylib`GCScan::GcScanRoots(fn=(libcoreclr.dylib`WKS::GCHeap::Promote(Object**, ScanContext*, unsigned int) at gc.cpp:37467), condemned=0, max_gen=2, sc=0x00007ffeefbf7a78)(Object**, ScanContext*, unsigned int), int, int, ScanContext*) at gcscan.cpp:154:5
    frame #7: 0x00000001031d6b44 libcoreclr.dylib`WKS::gc_heap::mark_phase(condemned_gen_number=0, mark_only_p=NO) at gc.cpp:21592:9
    frame #8: 0x00000001031d398a libcoreclr.dylib`WKS::gc_heap::gc1() at gc.cpp:17531:13
    frame #9: 0x00000001031ddd57 libcoreclr.dylib`WKS::gc_heap::garbage_collect(n=0) at gc.cpp:19116:9
    frame #10: 0x00000001031ca80d libcoreclr.dylib`WKS::GCHeap::GarbageCollectGeneration(this=0x0000000100207b70, gen=0, reason=reason_induced) at gc.cpp:38922:9
    frame #11: 0x0000000103201595 libcoreclr.dylib`WKS::GCHeap::GarbageCollectTry(this=0x0000000100207b70, generation=0, low_memory_p=NO, mode=2) at gc.cpp:38180:12
    frame #12: 0x0000000103201417 libcoreclr.dylib`WKS::GCHeap::GarbageCollect(this=0x0000000100207b70, generation=0, low_memory_p=false, mode=2) at gc.cpp:38114:30
```

Note that on the top frame, the `this` pointer does not look like a pointer at all.

## Analysis
The `GetBaseSize` and `my_get_size` functions seems to be pretty simple. The interest question seems to be why would `find_first_object` gives a bad pointer to `my_get_size` that is not an object. To understand that, let us take a look at the code for `find_first_object`.

The crashing call to `find_first_object` was made on line `19250` of `gc.cpp`. The call was given `interior` as the first parameter and `heap_segment_mem (seg)` as the second parameter. The first parameter is meant to be `start`, and the second parameter is meant to be `first_object`. What do they mean?

The first parameter is meant to be a pointer pointing into the interior of an object, let's call that `X`. The second parameter is meant to be a pointer to a valid object on or before the pointer to `X`. The function should be able to find the pointer to the `X`.

The implementation of `find_first_object` is fairly complicated related to the handling of the bricks. But since it is the first GC, the `gen0_bricks_cleared` flag was false, so before `find_first_object`, `find_object` already cleared all the bricks. So the bricks are just not there. We are falling back to the loop on line 30250-30542 as follow:

```c++
    while (next_o <= start)
    {
        do
        {
#ifdef TRACE_GC
            n_o++;
#endif //TRACE_GC
            o = next_o;
            assert (Align (size (o)) >= Align (min_obj_size));
            next_o = o + Align (size (o));
            Prefetch (next_o);
        }while (next_o < next_b);

        if (((size_t)next_o / brick_size) != curr_cl)
        {
            if (curr_cl >= min_cl)
            {
                fix_brick_to_highest (o, next_o);
            }
            curr_cl = (size_t) next_o / brick_size;
        }
        next_b = min (align_lower_brick (next_o) + brick_size, start+1);
    }
```

Inside the inner loop, we are advancing `o` by keep moving forward by `Align (size (o))`, the idea is that the memory is packed with objects. Each object can tell its own size, with some alignment constraints, we can expect the next object is placed right at the next location. 

To make this happen, the caller of GC is expected, whenever some memory is allocated, it must be populated with an object that describes its size that that match the allocated size. If that's not the case, this code (and many others that expect the same structure) would be reading into wrong memory.

### Sizes
There are two sources of truth about an object's size. On the one hand, mono is allocating through `GCHeap::Alloc()` with a `size` parameter that is computed upstream (on line `607` of `coregc-mono.cpp`), and the GC is assuming the object size to be the result of `my_get_size()`, which is computed using a simple formula `BaseSize + NumComponents * ComponentSize`. If these two doesn't match, then we have a problem. And indeed, they do not match.

To figure out this fact, I added some debug printf to the code. They're at line `619` of `coregc-mono.cpp` and line `9711`-`9715` of `gc.cpp`. All of these printf are prefixed with `Andrew` so it is easy to grep for in a log.

The printf can be correlated with the object address, in particular, this pair is interesting because they reported inconsistent sizes.

```txt

...

Andrew: (220) 0x102a79778 should have size 312

...

Andrew: gc said 0x102a79778 has size 296
gcenv.mono.h: GetBaseSize (based 10): 40
Andrew: mt->GetBaseSize() = 40
Andrew: mt->HasComponentSize() = 1
Andrew: ob->GetNumComponents() = 32
Andrew: mt->RawGetComponentSize() = 8

...

```

That doesn't look good, the same object is reported with different sizes when the size is queried through different parts.

We can look deeper into the issue by setting a breakpoint on `andrew_debug()`. The method is conditioned to break on the 220th allocation (which we know from the log the size is inconsistent, here is a stack).

```txt
  * frame #0: 0x00000001029b2ab4 libcoreclr.dylib`::andrew_debug() at coregc-mono.cpp:588:2
    frame #1: 0x00000001029b2b81 libcoreclr.dylib`::mono_gc_alloc_obj(vtable=0x0000000100868090, size=312) at coregc-mono.cpp:617:3
    frame #2: 0x00000001029b2c25 libcoreclr.dylib`::mono_gc_alloc_array(vtable=0x0000000100868090, size=304, max_length=32, bounds_size=16) at coregc-mono.cpp:634:32
    frame #3: 0x00000001028fd2bf libcoreclr.dylib`mono_array_new_full_checked(domain=0x00000001003196d0, array_class=0x0000000100833bd0, lengths=0x00007ffeefbf2440, lower_bounds=0x0000000000000000, error=0x00007ffeefbf5db0) at object.c:6598:21
    frame #4: 0x0000000102ca1440 libcoreclr.dylib`ves_array_create(domain=0x00000001003196d0, klass=0x0000000100833bd0, param_count=2, values=0x00000001237000e0, error=0x00007ffeefbf5db0) at interp.c:1076:23
```

The implementation of `mono_array_new_full_checked` is in `object.c`. Line `6597`-`6600` is particularly suscipious. It looks like we have two different ways to allocating an array with and without a bound size, but we used the same `vtable`. Since we derived the `BaseSize` only through the vtable, so it is likely that if we share the same vtable for two type of objects with different `BaseSize`, then we have trouble.

# Final words
The implementation of `my_get_size()` forces objects with different `BaseSize` to have a different `MethodTable`. This seems to be too much a restriction. Maybe we should allow that to be overridable, this direction seems to be consistent with [this](https://github.com/dotnet/runtime/issues/12809) issue.

In various places, I attempted to change the `min_object_size` back to 3 pointers sizes. The idea is `min_object_size` is a requirement from the CoreCLR GC that the objects must be larger than this size. If it happens that mono objects have size at least 4 pointers is irrelevant, it just satisfy the constraint, we don't need to make the constraint more strict.

It is better to merge the code earlier than later - there are various changes to the build that is keep conflicting with this change. For example, I have a hard time to rebase these changes on top of [this](https://github.com/dotnet/runtime/commit/188a1ee22344f181224d38df91fa8d214b76a020) commit.

In order to merge the change, it is important to ensure that this change does not break other existing variants, for example, Windows build. In particular, various Mono only changes must be under `ifdef`, which is not always true.