---
title: "Memory Corruption (5)"
date: 2026-05-10T10:01:16-07:00
draft: false
---

# Diagnosing GC Heap Corruption with Large Pages
In this post, I will talk about yet another memory corruption bug we found and fixed. Check out my [previous post](/posts/memory-corruption-4) for more context and examples.

## Backstory: Part 1 — dotnet/runtime#126903

A customer ([dotnet/runtime#126903](https://github.com/dotnet/runtime/issues/126903)) reported heap corruption when using `GCLargePages=1` with `GCCollectionMode.Aggressive`. The crashes manifested as `NullReferenceException` and `AccessViolationException` inside `ConcurrentDictionary` internals, with multiple threads faulting simultaneously — characteristic of a single GC event corrupting a heap region.

The root cause was that when large pages are enabled, `VirtualDecommit` is a no-op (huge pages cannot be partially decommitted), but the GC's bookkeeping still updated committed/used pointers as if memory had been returned to the OS. When regions were later reused, stale data survived because the OS never had a chance to zero the pages.

The initial fix ([PR #126929](https://github.com/dotnet/runtime/pull/126929)) added `memclr` in `virtual_decommit` for large pages. I then wrote a more comprehensive follow-up ([PR #127290](https://github.com/dotnet/runtime/pull/127290)) that skipped decommit entirely for large pages in `distribute_free_regions` (the aggressive tail-region decommit) and `decommit_heap_segment` (whole-segment decommit for hoarding and BGC deletion). For `decommit_region`, instead of skipping it, the fix bypassed `virtual_decommit` and called `reduce_committed_bytes` directly — letting `decommit_region` continue to handle memory clearing itself. The PR also added a `GCLargePages=2` fake large pages test mode to enable CI testing without requiring OS large page setup.

The customer deployed the patched GC — and several weeks later, the corruption came back in production.

## Part 2: dotnet/runtime#127892

### The Problem

The customer reported ([dotnet/runtime#127892](https://github.com/dotnet/runtime/issues/127892)) that even with the Part 1 fixes applied, heap corruption still occurred — this time without `GCCollectionMode.Aggressive`. The crashes happened during normal GC operation under memory pressure, with the same symptoms: corrupted managed objects, `ConcurrentDictionary+Node` with invalid `_next` pointers, intermittent timing anywhere from 30 seconds to 12 minutes into the workload.

### Reproduction

The customer provided a [minimal reproduction project](https://github.com/dotnet/runtime/issues/127892) with a Docker-based environment that combined several ingredients:

- **Real large pages**: 2048 huge pages (4GB) pre-allocated on the host via `vm.nr_hugepages`, container running with `--privileged`
- **Memory pressure**: Container cgroup limited to ~2.9GB, below the huge pages pool, with `GCHighMemPercent=0x26` (38%) to force aggressive region recycling
- **Allocation churn**: A pure safe C# application that continuously grows and replaces `ConcurrentDictionary` instances with multiple reader and writer threads, driving GC activity

The reproduction was a pure managed application with no unsafe code — ruling out application-level memory corruption.

A key practical technique was overlaying a custom-built `libcoreclr.so` into the container without rebuilding the entire runtime image. I copied the Checked-build binary and used the container's entrypoint script to overlay it at startup. This gave a fast iteration cycle: edit GC source → incremental build (~50 seconds) → copy binary → restart container.

### Phase 1: Where Is the Corruption Consumed?

After the Part 1 fixes were deployed, Ben ([comment on the issue](https://github.com/dotnet/runtime/issues/127892)) noticed that changing `decommit_region` to clear the entire committed range instead of just up to `used` made the corruption disappear. His patch survived a 4-hour test run on two machines. This was a strong signal: our assumption that memory between `used` and `committed` is clean was wrong.

To confirm, I added an assertion in `decommit_region` to verify that memory between `heap_segment_used` and `heap_segment_committed` was zero before clearing. The assertion fired immediately — the memory contained stale managed object data (UTF-16 strings), not zeros. I [replied to Ben on the issue](https://github.com/dotnet/runtime/issues/127892) confirming the same observation.

Now the question was: how does that stale data cause corruption? The GC allocation path in `adjust_limit_clr` has a critical optimization. When handing out memory to a thread's allocation context, it checks whether the memory is "fresh" (above `heap_segment_used`) or "recycled" (below `used`):

```cpp
if (clear_limit <= used)
    // Memory was previously used — clear everything
    memclr(clear_start, clear_limit - clear_start);
else
    // Memory is above used — trust it's already zero
    // Only clear [clear_start, old_used), skip [old_used, clear_limit)
    heap_segment_used = clear_limit;
    memclr(clear_start, used - clear_start);  // partial clear
```

The ELSE branch trusts that memory above `used` is zero. I knew we depended on this invariant for the allocator to give out clean memory, so I uncommented the existing verification code at the allocation path. It fired immediately — the allocator was indeed handing out dirty memory.

**Lesson: Start at the point where the invariant is consumed, not where you think the bug originates.**

### Phase 2: When Does the Data Become Stale?

Knowing WHERE the bad data is consumed doesn't tell us WHEN it got there. I used **temporal bracketing** — checking the same invariant at two points in time to narrow the window.

Initially, I checked whether all regions had clean memory between `used` and `committed`. The assertion fired almost immediately — but for frozen regions and gen 1 regions, not the ones I expected. This was a detour: not all regions need to maintain this invariant. Only allocating regions (the ephemeral segments where `adjust_limit_clr` runs) depend on the "above used is clean" property. I narrowed the scan accordingly.

I added a dirty-memory scan at both GC start and GC end, targeting only allocating regions:

- **GC-start check**: After `fix_allocation_contexts`, scan allocating regions. Is memory between `used` and `committed` clean?
- **GC-end check**: After `distribute_free_regions`, scan allocating regions. Clean?

Results:
- GC-START found dirty data ✓
- GC-END also found dirty data — but this was the same data persisting, not new

The key insight came from the next question: was the dirty region the ephemeral segment at the *previous* GC end? No — it was a different region.

**Lesson: Checking at two time points is more powerful than one. It narrows the window and reveals transitions.**

### Phase 3: Always-On State Logging

To understand what changed between GCs, I needed to see the normal state, not just the error state. I added a `GC-END-STATE` printf for every heap at every GC end, logging the ephemeral segment's `mem/allocated/used/committed`.

This created a timeline I could cross-reference:

```txt
GC-END-STATE: gc=286 heap=0 eph_seg=A mem=... used=... committed=...
GC-START-DIRTY: gc=287 heap=0 region=B ...
```

Region B (dirty) was not region A (the previous ephemeral segment). Region B was recycled from the free pool and became the new ephemeral segment between GCs.

**Lesson: Log normal state, not just errors. A baseline lets you spot transitions that error-only logging misses.**

### Phase 4: Checking the Transition Point

I knew the region arrived dirty from the free pool. But at what exact moment? I added a memory scan right at the point where a region becomes the ephemeral segment (`ephemeral_heap_segment = next_seg` in the allocation overflow path):

```txt
EPH-SWITCH alloc h0 gc=4 region=0x... used=0x... committed=0x... new_seg=1 dirty=1
```

`dirty=1` — the region was dirty the instant it left the free pool. Not corrupted by allocation, not corrupted by GC. It entered the free pool dirty and came out dirty.

**Lesson: Check invariants at transition boundaries. The boundary between "free pool" and "active region" is where the bug becomes visible.**

### Phase 5: Understanding the Invariant

At this point I had a mechanical understanding: regions come out of the free pool with stale data above `used`. But I needed to understand the *design intent* to know where the fix belongs.

The critical question: **Is "memory above `used` is clean" an invariant for all regions, or only for the ephemeral segment?**

This matters because it determines two different fixes:

- **Case 1 (invariant for all regions)**: Something violates the invariant during the region's active lifetime. Fix the violator.
- **Case 2 (invariant only for ephemeral)**: The invariant doesn't apply to free pool regions. Fix the transition point where a region becomes ephemeral.

I examined the code:

1. `adjust_limit_clr` depends on the invariant — but only runs on the ephemeral segment (the allocation segment). Only the consumer cares.

2. `decommit_region` for non-large-pages calls `virtual_decommit`, which returns pages to the OS. The OS zeros them on recommit. The invariant is maintained by the OS, not by GC code.

3. `decommit_region` for large pages can't decommit (huge pages can't be decommitted). So it manually `memclr`s — but only up to `used`, not `committed`.

4. `used` is only actively maintained as a high watermark on the ephemeral segment. After compaction, `plan_allocated` can be lower than the old `used`, and the compact code only advances `used` upward, never lowers it. But data was written up to the old high watermark.

5. Plan phase code (`plan_phase.cpp:7317`) already sets `used = committed` when creating a new ephemeral segment — but only in the non-USE_REGIONS path. Under USE_REGIONS, no such protection exists.

This confirmed **Case 2**: the GC never intended to maintain the invariant for all regions. The non-large-pages path just happened to work because the OS handles cleanup. The large pages `memclr` path was an optimization that assumed `used` was a reliable high watermark — but it's only reliable for the ephemeral segment.

**Lesson: Before fixing a bug, understand the design intent. The same symptom can have two very different correct fixes depending on which invariant was supposed to hold.**

### Phase 6: Git Archaeology

I traced the buggy conditional to its origin:

```bash
git log -S "use_large_pages_p ? heap_segment_used" -- src/coreclr/gc/gc.cpp
```

It was introduced in August 2021, PR #56314 ("Improve region free list handling"), which added `decommit_region` for the first time. The `used`-based clear limit was there from day one — not a regression from a later change.

The bug survived for nearly 5 years because it requires a specific combination: large pages + memory pressure + region recycling + the stale data landing in the ELSE branch of `adjust_limit_clr`. Without large pages, the OS zeroing on recommit masks the issue entirely.

**Lesson: `git log -S` (pickaxe search) finds when a specific expression was introduced. Understanding the original context explains why a bug survived.**

### The Root Cause

In `memory.cpp`, `decommit_region`:

```cpp
uint8_t* clear_end = use_large_pages_p
    ? heap_segment_used(region)      // BUG: used is not a reliable high watermark
    : heap_segment_committed(region);
```

For large pages, `memclr` only clears up to `used`. But `used` doesn't track all writes — it's only maintained for the ephemeral segment. After compaction or replanning, a region can have data above `used` from its previous role as an allocation target. When the region is recycled, that stale data survives into the free pool.

The fix: clear to `committed` instead of `used` for large pages.

### Diagnostic Techniques Summary

| Technique | What It Revealed |
|-----------|-----------------|
| Assertion at decommit point | Memory above `used` contains stale data |
| Verification at allocation path | Allocator hands out dirty memory |
| Temporal bracketing (GC start + end) | Dirty data exists before GC runs |
| Always-on state logging | Region identity changes between GCs |
| Dirty check at transition point | Region arrives dirty from free pool |
| Invariant analysis (code reading) | `used` is only reliable for ephemeral |
| Git archaeology (`git log -S`) | Bug introduced in 2021, masked by OS zeroing |

