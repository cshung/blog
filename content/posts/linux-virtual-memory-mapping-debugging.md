---
title: "Linux Virtual Memory Mapping Debugging"
date: 2024-06-05T13:33:14-07:00
draft: false
---

In this post, I will talk an issue related to Linux virtual memory mapping exhaustion. The issue involve understanding the Linux memory management subsystem, and so it is quite interesting to look at.

For the impatient, this blog entry described some concepts for Linux virtual memory, described the debugging process, and proposed the next steps.

# What is the problem?
In [this](https://github.com/dotnet/runtime/issues/97316) GitHub issue, customers are hitting this error.

```text
Fatal error. Failed to create RW mapping for RX memory. This can be caused by insufficient memory or hitting the limit of memory mappings on Linux (vm.map_max_count).
```
It is relatively easy to reproduce this locally, in order to avoid running a huge application, all we really needed to is to scale the limit down. For example, we can run our GCPerfSim with these arguments:

```sh
GCPerfSim.dll -tc 6 -tagb 100.0 -tlgb 0.1 -lohar 0 -pohar 0 -sohsi 10 -lohsi 0 -pohsi 0 -sohsr 100-4000 -lohsr 102400-204800 -pohsr 100-4000 -sohp
i 10 -lohpi 0 -sohfi 0 -lohfi 0 -pohfi 0 -allocType reference -testKind time
```

with a reduced number of mappings

```sh
sudo sysctl -w vm.max_map_count=600
```

This will reproduce the issue all the time, there is just no need to run a big application at all.

# Preliminary analysis

Apparently, this has something to do with memory mappings. To do that, we can inspect the virtual memory mappings. In Linux, we can inspect the `/proc/pid/maps` file to look at the mappings.

Larger heap size tends to create more mappings, which we can do with ServerGC and increase the `tlgb` value.

|tlgb |# GC/Total mappings |
|:---:|:------------------:|
|0.01 | 222/787            |
|0.1  | 476/1363           |
|1    | 35106/35825        |

It is quite obvious that the majority of the mappings comes from the GC heap even when the live data size is just 1G. The heap size, according to the trace, fluctuates. It can go as high as 24G but then come back down to 16G. Even when we take the heap size at its highest time, each range on average is just 71k. These tiny mappings are really wasteful.

Looking at some sample mappings, we can see that there are runs of mappings that are consecutive, having the same protection, very small, but are separate entries.

```txt
...
7f8bd6800000-7f8bd6801000 rw-p 00000000 00:00 0 
7f8bd6801000-7f8bd6830000 rw-p 00000000 00:00 0 
7f8bd6830000-7f8bd6832000 rw-p 00000000 00:00 0 
7f8bd6832000-7f8bd6862000 rw-p 00000000 00:00 0 
7f8bd6862000-7f8bd6864000 rw-p 00000000 00:00 0 
...
```

Something is going on in the underlying system.

# Background

## What is a Virtual Memory Area (VMA)?
Linux processes use virtual memory, all memory addresses that a Linux process access is a virtual address, which will be backed by something. That could be physical memory, swapped out content, or file. As such, the operating system needs to be able to store these information in a data structure so that when a page fault happens, it knows what to do to make sure the memory is available for accessing.

We only need a single object for a range of virtual address that share the same information, and therefore we have a `vm_area_struct` that represents a memory range, and it is being used to store the information related to that range.

## Merging as an optimization
Obviously, page fault handling needs to be fast, and therefore it is a goal to minimize the number of `vm_area_struct` object instances so that we can quickly find the `vm_area_struct` associated to a memory address for page fault processing. In principle, adjacent `vm_area_struct` that share the same information (e.g. protect flags) can be merged together.

But apparently, in our run, it doesn't. The goal of this investigation is to figure out why.

# Debugging kernel
With a friend's help, I am able to debug the kernel in two ways.

1. Adding `printk` statements so that we can output some logging information, and
2. Setting breakpoints in the kernel so that I can step.

These turn out to be very useful to understand the problem. 

Presented below is only a reasonable way to debug through this problem, in reality, debugging is messy, lot of guess work and leading to blind allies, those missteps are not documented at all, there is no point to.

## How VMAs are created?
To start with, I begin my understanding of the code starting from the beginning where the GC used `mmap` to reserve a range of addresses. Very soon, I get to this piece of code:

```c
unsigned long do_mmap(struct file *file, unsigned long addr,
...
	/* Too many mappings? */
	if (mm->map_count > sysctl_max_map_count)
		return -ENOMEM;
```

That looks familiar, we altered the `vm.map_max_count` before, this must be how we check the number of `VMA`. Therefore, it is relatively easy to just search for `sysctl_max_map_count` to find the other places where the number if being checked.

There are only a few:

1. `do_mmap`, as we have just found.
2. `split_vma`, as the name suggest, splitting an existing `VMA` so we will have an extra `VMA`, and
3. some other related to `unmap`, `remap`, which seems unrelated as of now.

## How split_vma is invoked?
We can easily answer this question with a breakpoint, here is a call stack:

```txt
#0  split_vma          at mm/mmap.c:2480
#1  vma_modify         at mm/mmap.c:2480
#2  vma_modify_flags   at mm/mprotect.c:635
#4  do_mprotect_pkey   at mm/mprotect.c:818
#5  __do_sys_mprotect  at mm/mprotect.c:839
#6  __se_sys_mprotect  at mm/mprotect.c:836
#7  __x64_sys_mprotect at mm/mprotect.c:836
#8  do_syscall_x64     at arch/x86/entry/common.c:52
#9  do_syscall_64      at arch/x86/entry/common.c:83
#10 entry_SYSCALL_64   at arch/x86/entry/entry_64.S:121
```

So basically the stack trace is telling us the kernel is executing a system call `mprotect`, it is trying to modify flags on a range, and so it found the `VMA` associated with the range, and discovered that because of a change of flags in a subrange of that range, it needs to split the vma.

Also, we see te implementation of `vma_modify` also call `vma_merge`, it only make sense that `mprotect` call can also potentially leads to merging `VMA`s.

## How vma_merge works?
`vma_merge` is a lot of code, but in essence it does the following.

1. It relies on it caller to provide a `prev`, so we know the `VMA` that comes before the current `VMA`.
2. It uses `curr->vm_end` to invoke `vma_lookup` to find the next.
3. Using some policy decision, check if it can merge with `prev` and `next`.

Of all policy decisions, this check seems to be the key check that fails.

```c
is_mergeable_anon_vma(prev->anon_vma, next->anon_vma, NULL)

static inline bool is_mergeable_anon_vma(struct anon_vma *anon_vma1,
		 struct anon_vma *anon_vma2, struct vm_area_struct *vma)
{
	/*
	 * The list_is_singular() test is to avoid merging VMA cloned from
	 * parents. This can improve scalability caused by anon_vma lock.
	 */
	if ((!anon_vma1 || !anon_vma2) && (!vma ||
		list_is_singular(&vma->anon_vma_chain)))
		return true;
	return anon_vma1 == anon_vma2;
}
```

For the code, either only one of `anon_vma` instances is non-null, or they are the same, otherwise this check fails.

## What is anon_vma?
First, `anon_vma` has the type `anon_vma`, which is not `vm_area_struct`, these aren't the virtual memory area that are subject to the system limitation. I was tricked by that at first. `anon_vma` is a field on the `vm_area_struct`. I know it is confusing, the kernel code used the `anon_vma` as both a variable, a type, and a field name, so we have to live with it.

Judging from the name `anon`, I can only guess this has to do with `anonymous`. Basically when we call `mmap`, the kernel has no idea what that mapping is for, so it just decide this is an anonymous mapping.

## How anon_vma are created?
Conveniently, `anon_vma` objects are constructed using `anon_vma_alloc`, and it is only called in a couple of places, the interesting call stack is here.

```txt
#0  anon_vma_alloc     at ../mm/rmap.c:204
#1  __anon_vma_prepare at ../mm/rmap.c:204
#2  anon_vma_prepare   at ../include/linux/rmap.h:169
#3  do_anonymous_page  at ../mm/memory.c:4433
#4  do_pte_missing     at ../mm/memory.c:3879
#5  handle_pte_fault   at ../mm/memory.c:5303
#6  __handle_mm_fault  at ../mm/memory.c:5444
#7  handle_mm_fault    at ../mm/memory.c:5610
#8  do_user_addr_fault at ../arch/x86/mm/fault.c:1382
#9  handle_page_fault  at ../arch/x86/mm/fault.c:1474
#10 exc_page_fault     at ../arch/x86/mm/fault.c:1532
#11 asm_exc_page_fault at ../arch/x86/include/asm/idtentry.h:623
```

What the callstack is telling us is that an `anon_vma` instance is created when we handle a page fault of an anonymous page, which make sense because the Linux documentation tells us physical pages are allocated lazily, which means the associated `anon_vma` object instance is also allocated that way.

## Hypothesis
Now I hypothesize that the `VMA`s are not merged because of this sequence:

1. Perform a `mmap` of a range of 3 pages to `PROT_NONE`, this will create a single VMA with no `anon_vma`.
2. Perform a `mprotect` the first page to `PROT_READ | PROT_WRITE`, this will `vma_split` into two `VMA`s, still no `anon_vma` on both.
3. Touch the first page, this will cause a page fault on the first page, and therefore create a `anon_vma` associated with the `VMA` of the first page.
4. Perform a `mprotect` the last page to `PROT_READ | PROT_WRITE`, this will `vma_split` the second `VMA` into two `VMA`s, still no `anon_vma` on all three of them.
5. Touch the last page, this will cause a page fault on the last page, and therefore create a `anon_vma` associated with the `VMA` of the last page. This is going to be a different `anon_vma` object instance from the first one.
6. Perform a `mprotect` the middle page to `PROT_READ | PROT_WRITE`, this will not create any new `VMA`, but merely changing the flags.
7. At this point, `mprotect` will attempt to `vma_merge`, but it will fail because the `prev` and `next` both have a different `anon_vma` instance.

## Result
Experiment confirms that this is that case. `printk` clearly show that it happened. The `/proc/pid/maps` also indicate the ranges are not merged.

Here is the sample program that I used, the threads are probably unimportant, they were there just because we thought thread could be the reason of the proliferation of `VMA`s.

```c
#define _GNU_SOURCE
#include <stdio.h>
#include <unistd.h>
#include <sys/mman.h>
#include <pthread.h>
#include <sched.h>
#include <stdint.h>

const size_t page = 4096;
char *reserved;
const int distance = 2;

void *worker(void *param)
{
    uint64_t id = (uint64_t )param;
    uint64_t  second = (3 - id) % 3 + 1;

    sleep((int)(second * 10));

    mprotect(reserved + id * distance * page, 4096 * distance, PROT_READ | PROT_WRITE);
    *(reserved + id * distance * page) = 'a';

    return NULL;
}

int main(int argc, char** argv)
{
    reserved = mmap(NULL, 3 * distance * page, PROT_NONE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
    mprotect(reserved + 5 * page, page, PROT_READ | PROT_WRITE);
    *(reserved + 5 * page) = 'a'; // let the kernel create a anon_vma
    mprotect(reserved + 5 * page, page, PROT_NONE);
    printf("Reserved memory from %p to %p\n", reserved, reserved + 3 * distance * page);
    pthread_t threads[3];
    for (uint64_t  i = 0; i < 3; i++)
    {
        pthread_attr_t attr;
        cpu_set_t mask;
        CPU_ZERO(&mask);
        CPU_SET(i, &mask);
        pthread_attr_init(&attr);
        pthread_attr_setaffinity_np(&attr, sizeof(cpu_set_t), &mask);
        pthread_create(&threads[i], &attr, worker, (void*)i);
    }

    for (int i = 0; i < 3; i++)
    {
        pthread_join(threads[i], NULL);
    }

    printf("Done\n");
    getchar();

    return 0;
}
```

The sleep is really just an experimental technique - by giving me 10 seconds between each operation, I can have a chance to break into the debugger, or I can correlate the `printk` statements definitely with the operation.

Adding `printk` to `vma_merge` as well, put this right before the `is_mergeable_anon_vma` check (but before the early return because `merge_prev == 0` and `merge_next == 0`) so that we get to see those cases.

```c
printk("Merge decision 1 merge_prev = %d, merge_next = %d, prev->anon_vma = %llx, next->anon_vma = %llx\n",
		merge_prev,
		merge_next,
		prev == NULL ? 0 : (uint64_t)prev->anon_vma, 
		next == NULL ? 0 : (uint64_t)next->anon_vma
	);
```

The log is obvious.

```txt
Reserved memory from 0x7fbe17c48000 to 0x7fbe17c4e000
[   20.862157] Merge decision 1 merge_prev = 0, merge_next = 0, prev->anon_vma = ffff888004c7d208, next->anon_vma = 0
[   30.870179] Merge decision 1 merge_prev = 0, merge_next = 0, prev->anon_vma = 0, next->anon_vma = ffff888004c7d1a0
[   40.865433] Merge decision 1 merge_prev = 1, merge_next = 1, prev->anon_vma = ffff888004175000, next->anon_vma = ffff888004c7d1a0
```

- When the first page is touch, both the `prev` and `next` are not the same type, so `merge_prev` and `merge_next` fails.
- Same happened with the last page, the `prev` is not writable, and the `next` is outside of the initial `mmap` call.
- When the last call happened, the log proved that while the flags are compatible with `merge_prev` and `merge_next`, the `anon_vma` are different and so we failed the merge.

The `/proc/pid/maps` also show the failure to merge.

```txt
7fbe17c48000-7fbe17c4c000 rw-p 00000000 00:00 0
7fbe17c4c000-7fbe17c4d000 rw-p 00000000 00:00 0
7fbe17c4d000-7fbe17c4e000 rw-p 00000000 00:00 0
```

## What can we do about it?
Thanks the Jan Vorlicek, we tried a hack to force the initial creation of an `anon_vma` as follows, changes:

```c
    reserved = mmap(NULL, 3 * distance * page, PROT_NONE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
```

to

```c
    reserved = mmap(NULL, 3 * distance * page, PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
    *reserved = 'a'; // let the kernel create a anon_vma
    mprotect(reserved, page, PROT_NONE);
```

The sequence of becomes the following:

1. Perform a `mmap` of a range of 3 pages to `PROT_READ | PROT_WRITE`, this will create a single VMA with no `anon_vma`.
3. Touch the first page, this will cause a page fault on the first page, and therefore create a `anon_vma` associated with the `VMA`.
2. Perform a `mprotect` the 3 pages to `PROT_NONE`, this will not create any new `VMA`, but merely changing the flags.
4. Perform a `mprotect` the first page to `PROT_READ | PROT_WRITE`, this will `vma_split` the second `VMA` into two `VMA`s, both `VMA`s will have the same `anon_vma`.
3. Touch the first page, this will cause a page fault on the first page, and we already have an `anon_vma` for it.
4. Perform a `mprotect` the last page to `PROT_READ | PROT_WRITE`, this will `vma_split` the second `VMA` into two `VMA`s, all three of them will have the same `anon_vma`.
3. Touch the last page, this will cause a page fault on the last page, and we already have an `anon_vma` for it.
6. Perform a `mprotect` the middle page to `PROT_READ | PROT_WRITE`, this will not create any new `VMA`, but merely changing the flags.
7. At this point, `mprotect` will attempt to `vma_merge`, and it will succeed because the `prev` and `next` both have the same `anon_vma` instance.

The fact that `vma_split` decides to just reference the same `anon_vma` is the key to this hack, it only works conditionally though, here are the conditions.

```c
if (!dst->anon_vma && src->anon_vma &&
            anon_vma->num_children < 2 &&
            anon_vma->num_active_vmas == 0)
            dst->anon_vma = anon_vma;
```

I won't pretend I understand what those conditions are, but the comments seems to indicate it has something to do with forks.

Unfortunately, this hack only work on this simple case. There are at least two hurdles on applying this to the CoreCLR context.

1. The initial `mmap` call with `PROT_READ | PROT_WRITE` call will fail because of the overcommit settings. We never really intend to use that much memory, we just wanted the `anon_vma` instance, and
2. Even when I reduced the initial region range to a size less than the physical memory where the `mmap` call won't fail, the proliferation of small `VMA`s is still there with reason that we are still unclear.

Even if it did work, it is programming to implementation, which is fragile. We will never know if Linux decides on some other memory management logic that will break it.

The fact that `is_mergeable_anon_vma` causing `VMA` proliferation is not new, Jakub already studied the problem and even proposed a patch [here](https://lwn.net/ml/linux-kernel/20220516125405.1675-6-matenajakub@gmail.com/). As the discussion goes on [here](https://lwn.net/ml/linux-kernel/408b79d9-e96a-b961-1565-93bf11e54909@suse.cz/), at least one unknown proprietary workload hit exactly the same problem. 

Jakub's study leads to his master thesis [here](https://dspace.cuni.cz/bitstream/handle/20.500.11956/176288/120426800.pdf), I haven't read it yet.

I think it is time for us to engage with the Linux memory management experts to see what we should do about it, we have done enough study on our part. 