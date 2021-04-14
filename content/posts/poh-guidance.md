---
title: "POH Guidance"
date: 2021-04-14T00:00:00-08:00
draft: false
---

# POH Guidance Document

This document is written to document some recommendations on how to optimally use the pinned object heap. For the impatient ones who already know about pinning very well, feel free to skip to the guidance section directly.

# Background

## What is a pin object?
It is possible to restrict the .NET Garbage collector from moving an object. In this case, we call it pinning.

## How do I pin an object?
There are a few ways you could pin an object in .NET managed code. 

### GCHandle
You can create a `GCHandle` as follow:

```c#
byte[] data = new byte[100];
GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
// From now on, data cannot be moved
// ...
// Until we free the handle
handle.Free();
```

Check out the `GCHandle` [documentation](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.gchandle?view=net-5.0) for more information.

### The fixed statement
We can also use the `fixed` statement in C#. **TODO:** VB?

```c#
unsafe
{
    fixed (byte* data = new byte[100])
    {
        // From now on, data cannot be moved
        // ...
        // Until we free the handle
    }
}
```

Check out the `fixed` statement [documentation](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/fixed-statement) for more information.

### The stackalloc keyword
In case the object only need to live within a stack frame, We can also use the `stackalloc` statement in C#. **TODO:** VB?

```c#
unsafe
{
    byte* buffer = stackalloc byte[100];
}
```

Check out the `stackalloc` [keyword documentation](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/stackalloc) for more information.


### The AllocateArray API
Beginning .NET 5, we introduced a new way to create pinned buffers through the `GC.AllocateArray` call as follow:

```c#
byte[] data = GC.AllocateArray<byte>(100, pinned: true);
```

Check out the `AllocateArray` API [documentation](https://docs.microsoft.com/en-us/dotnet/api/system.gc.allocatearray?view=net-5.0) for more information.

In this case, the array will stay pinned until it is no longer reachable and be collected by the garbage collector.

## Why do we pin an object?
There are two main use cases for pinning an object. To interop with native code, and to use high performance pointer arithmetic.

### To interop with native code
Suppose we wanted to pass an array to the native code and it is not pinned, the garbage collector could move the array when the native code is executing, the result would be catastrophic. Therefore we need to make sure the garbage collector do not move the array when native code still depending on the array.

### To use high performance pointer arithmetic
Sometimes, we can use pointer to speed up array accesses. Pointers are not range checked, and advancing a pointer is faster than indexing the array again. In extreme cases, it might make sense to temporaily pin the array and use pointer to speed up computation.

There could be other use cases as well, but these two are the main ones.

## What is the pinned object heap?
In .NET 5, we introduced the [pinned object heap](https://github.com/dotnet/runtime/blob/main/docs/design/features/PinnedHeap.md). When we use `pinned: true` in the `AllocateArray` API call, the array will be allocated on the pinned object heap.

## Why do we have the pinned object heap?
Pinning object created constraints for the garbage collector. These constraints impede the garbage collector's ability to manage memory efficiently, in particular, it causes large pin plug and demotion issues. Let's take a look at these issues in some depth.

**TODO:** Have a picture to explain what they are.

# Guidelines

**Consider** understanding how the Pinned Object Heap solves the pinning problem.

The background section should 

**Don't** unnecessarily pin objects 

As we saw above, pinning is still not free. Whenever possible, we should avoid pinning objects.

**Don't** allocate short lived object on POH

POH are collected only during a gen 2 GC. These short lived objects will occupy memory until the next Gen 2 GC, which is going to take a long while.

**Consider**  pooling frequent short lived pinned buffer needs.

From the GC's perspective, pooling convert many short lived objects into a few long lived objects, and it is much easier to manage a few long lived objects.

**Consider** standardize buffer sizes to a power of 2.

Fixed size buffers are much more likely to be reusable, so are the free spaces.

**Consider** using frozen segments

For objects that don't have references and read-only, it is possible to allocate them on frozen segments. Most of the GC ignores about frozen segments so it is the most performant options when it is applicable.

**Do** monitor performance.

There is only so much the theories tell us, in practice, your mileage might vary. Measure is the only truth.

**Consider** replace pinning need with stackalloc

If the life time of the pinned object can be made such that it coincide with a stack frame, we could replace the pinning by stackalloc.

**Consider** apply what you have learnt from optimizing your performance for LOH

Implementation wise, POH inherited a lot of tuning parameters from LOH, it is well possible that advices for tuning application performance for LOH may apply for POH as well.