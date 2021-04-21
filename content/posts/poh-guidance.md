---
title: "POH Guidance"
date: 2021-04-14T00:00:00-08:00
draft: false
---

# When and how to take advantage of the POH

.NET 5 introduced a new GC feature called POH (Pinned Object Heap). The POH was designed to improve current problematic pinning situations by reducing fragmentation and the amount of work that the GC has to do. This document explains how to recognize problematic situations and take advantage of the POH when you run into these situations. 

# Background

## Why pinning exists
The main reason why .NET provides pinning is to interop with native code. When native code is working with a managed object, that object needs to be pinned so GC does not move it while the native code is still using it. Pinning an object tells the GC to not move it. 

## How do I pin an object?
Before .NET 5, here are the 2 ways you could pin an object in user code.

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
We can also use the `fixed` statement in C#.

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

[MaoniS - you forgot to update the comment for this code example]

Check out the `fixed` statement [documentation](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/fixed-statement) for more information.

### Potential performance problems with pinning

When there's no GC happening, pinning is a no-op. IOW, when GC doesn't notice something is pinned, it makes no difference whether you pin it or not. When pinned objects are only observed in younger generations, it also doesn't cause as big a problem as in older generations as the free spaces inbetween pins are used sooner. If there are pins in gen0, the free spaces inbetween these pins are used right away by the user allocations. [MaoniS - you could put a picture here by either taking one out of my [pinning talk](https://github.com/Maoni0/mem-doc/blob/master/presentation/dotnetos2020-Pinning.pptx) or make one yourself]. 

The most stressful pinning scenario is when long lived pins are scattered on the heap. The GC heap is organized by segments - each segment is a large reserved virtual address range (for Server GC this could be in GB) and GC commits memory on it as needed. For example, if there's a pin basically at the end of the segment, it means GC would need to keep almost the whole segment commit. Even though GC can use the free spaces inbetween pins, under high memory load we do want to aggressively reduce the whole heap size and pinning in this fashion would prevent GC from doing so. Below is an illustration of how such a scenario came to be - [MaoniS - put some pictures here to illustrate when you use pinned handles to hold onto objects that survives into gen2 as GCs happen]

As you can see, the problem is when you mix long lived pinned objects with objects that aren't as long lived, the heap is expanded and fragmentation is created on the heap. If you know you will be creating these long lived pins it would be beneficial for them to live in their own area instead of mixing them with non pinned objects. This is the idea of POH which gives you such an area. Instead of pinning an existing object which could be anywhere on the heap, you put them on the POH portion of the heap so it groups them together instead of "stretching" the rest of the heap. And since this is for long lived pins, it's only collected during gen2 GCs. If you churn the POH a lot with temporary pins it could create significant fragmentation on POH itself which is also not desirable. 


### The AllocateArray API
Starting .NET 5, we introduced a new way to create pinned arrays with the `GC.AllocateArray` API as follow:

```c#
byte[] data = GC.AllocateArray<byte>(100, pinned: true);
```

Check out the `AllocateArray` API [documentation](https://docs.microsoft.com/en-us/dotnet/api/system.gc.allocatearray?view=net-5.0) for more information.

In this case, the array will stay pinned until it is no longer reachable and collected by the garbage collector. 

## Comparing pinning with pinned handles and using POH
This is an example of converting a scenario from using the pinned GC handle to using the POH. 

[MaoniS - insert code examples with perf analysis here - basically what you did with GCPerfSim. You can show how to use perfview to look at the perf since you are saying to monitor perf below]

# Guidelines

**Consider** understanding how the Pinned Object Heap solves the pinning problem.

**Don't** unnecessarily pin objects 

As we saw above, pinning is still not free. When possible, we should avoid pinning objects. Don't treat POH as "a separate heap area", it's still only for objects that are necessary to be pinned.

**Don't** allocate short lived object on POH

POH are collected only during a gen 2 GC. These short lived objects will occupy memory until the next Gen 2 GC, which is going to take a long while.

**Consider**  pooling frequent short lived pinned buffer needs.

From the GC's perspective, pooling convert many short lived objects into a few long lived objects, and it is much easier to manage a few long lived objects. [MaoniS - wouldn't this make it fall into the long lived pinning scenario? But you are saying this should not live on POH?]

**Consider** using frozen segments

For objects that don't have references and read-only, it is possible to allocate them on frozen segments. Most of the GC ignores about frozen segments so it is the most performant options when it is applicable. [MaoniS - do we have any documentation on frozen segments? If not it wouldn't really help to put this here - most people aren't familiar with it at all]

**Do** monitor performance.

There is only so much the theories tell us, in practice, your mileage might vary. Measure is the only truth.

**Consider** replace pinning need with stackalloc

If the life time of the pinned object can be made such that it coincide with a stack frame and the size is small enough, you could replace pinning with [stack allocation](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/stackalloc) which is not managed by the GC thus does not affect situations on the heap. 
