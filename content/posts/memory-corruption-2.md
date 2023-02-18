---
title: "Memory Corruption (2)"
date: 2021-07-02T15:01:10-07:00
draft: false
---

# Another memory corruption bug
In this post, we will talk about another memory corruption bug I found and fix. Check out my [previous post](/posts/memory-corruption-1) for more context and examples.

# The symptom
We are hitting an access violation at this location:

```c++
inline size_t my_get_size (Object* ob)
{
    MethodTable* mT = header(ob)->GetMethodTable();

    return (mT->GetBaseSize() +
            (mT->HasComponentSize() ?
             ((size_t)((CObjectHeader*)ob)->GetNumComponents() * mT->RawGetComponentSize()) : 0));
}
```

When the access violation happens, `mT` is a nullptr and therefore it cannot be dereferenced. The method table pointer `mT` is obtained by dereferencing the object `ob`, so something is wrong with `ob`. Looking upstream, `ob` is obtained by walking a region, we do not know why the heap is corrupted like that yet.

# Analyzing
Looking around the memory close to `ob`, it just a pile of 0, nothing interesting there.

```txt
0:015> ?? ob
class Object * 0x00000192`dcffff78
   +0x000 m_pMethTab       : (null) 
0:015> dq 0x00000192`dcffff78
00000192`dcffff78  00000000`00000000 00000000`00000000
00000192`dcffff88  00000000`00000000 00000000`00000000
00000192`dcffff98  00000000`00000000 00000000`00000000
00000192`dcffffa8  00000000`00000000 00000000`00000000
00000192`dcffffb8  00000000`00000000 00000000`00000000
00000192`dcffffc8  00000000`00000000 00000000`00000000
00000192`dcffffd8  00000000`00000000 00000000`00000000
00000192`dcffffe8  00000000`00000000 00000000`00000000
```

Without further hint, I used the StressLogAnalyzer to search for the address, and we found something interesting:

```txt
>r-v:0x00000192dcffff78
 4a0  10.646502000 : `GC l=4`             -192dcffff78-
 4a0  10.646500800 : `GC l=3`             (a8)[192dcffe020->192dcffff78, NA: [19293086980(1240954528), 192930888d8[: 1f58(1), x: 192dcffff78 (C)
 4a0  10.646500100 : `GC l=3`             192dcffff78[(1f58)
 4a0  10.626726200 : `GC l=3`             Fixing allocation context 1d314d07fa8: ptr: 192dcffff78, limit: 192dcffffd0
```

Most of the other lines seems to be just a pile of address values, but the last line is interesting. The wrong address turn out is a `ptr` of an `allocation_context`. To understand what this means, we need to understand the concept of an allocation_context.

# Allocation context explained
We wanted to make allocation really fast, and nothing is faster than just adding a pointer. To achieve that, we have an allocation context per thread as a field on the Thread object associated with the thread, and the Thread object itself is available as a thread static field. The allocation context is basically a contiguous range of memory that the GC give out in the last allocation call. `ptr` represents how much we already allocated to user code, and `limit` represents how much the allocation context can still allocate.

A picture worth a thousand words, here is how the allocation context looks like:

```txt
|user object 1|...| last user object |000 ............... 000|
                                     ^                       ^
                                     ptr                     limit
```

So at any time when the user program allocates, it simply advance the ptr. Only if the advanced ptr run out of limit then we need to ask the GC for more memory. By being thread static, there is no need to synchronize.

# Fixing the allocation contexts
By default, memory given out by the GC is zero filled, that means between `ptr` and `limit`, we see a pile of zero. If we were to perform a heap walk right now, we would want to compute the size of the object at `ptr`, and it will fail because `ptr` is pointing to 0, which is not a valid method table pointer. To see why we will compute the size of the object at `ptr`, check out my [previous post](/posts/gc-objects-1) on size computation.

To remedy that, we have a call called `fix_allocation_contexts` that is supposed to solve the problem. The function enumerates all the allocation contexts and 'fix' them using `fix_allocation_context`. `fix_allocation_context` does one of the following things:

1. If it happens to be case that the allocation context at the end of the ephemeral heap segment, then adjust the alloc_allocated variable so that we know we will not walk beyond that address, or
2. Fill the remaining space between `ptr` and `limit` with a free object, so that the walk will be fine.

An interesting special case is that what if the remaining space between `ptr` and `limit` is less than the size of a free object? That would be bad. To remedy that, during allocation, we always leave some space (exactly the size of a minimal sized free object) before giving it out as `limit`, so there are always space to create that free object.

# Back to the problem
We are currently at the `ptr` of an allocation context and we see zero, so there must be something wrong with the `fix_allocation_context` not doing its job. Indeed, there is something wrong in that area of code, and this [PR](https://github.com/dotnet/runtime/pull/54931) fixed it. The key issue is that with regions, there could be multiple linked regions representing the old to new objects, and in general the region addresses are not sorted, so we need to check explicitly that we are at the end of the `ephemeral_heap_segment` or not when trying to decide which case we should do to fix the allocation context.