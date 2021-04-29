---
title: "POH Tuning (Part 4 - Benchmark design and the performance infrastructure)"
date: 2021-03-05T19:55:26-08:00
draft: false
---
# Benchmark design
Armed with the knowledge about how the allocation ratios works in the [previous post](../poh-tuning-2/). Now we can design our benchmarks. My goal is to produce a pair of benchmarks so that I can compare pinning objects by using the old pinned handle, or by the new pinned object heap. 

In the pinned handle case, we can have a general design like this:

Out of 1,000 objects in the SOH, \\( a \\) of them survives and \\( b \\) of them are pinned.

The key idea is to create a matching pinned object heap variant, and here is how it could be done:

Out of \\( 1000 - b \\) objects in the SOH, \\( a - b \\) of them survives and none of them are pinned.
Out of \\( b \\) objects in the POH, \\( b \\) of them survives and \\( b \\) of them are pinned.

The benchmark is designed so that the total number of survived object would be \\( a - b + b = a \\) , and the total number of pinned object is \\( b \\), which means both cases implements the same scenario.

Using the knob that we can control, here are some constraints on \\( a \\) and \\( b \\):

- \\( b \le a \\)
- The first design can be realized if \\( a \\) is a factor of 1000 and \\( b \\) is a factor of \\( a \\).
- The second design can be realized if \\( a - b \\) is a factor of \\( 1000 - b \\).

As an example, we could pick \\( a \\) to be 100 and \\( b \\) to be 10. That would lead to:

In the pinning handle case:
```
sohsi=1000/100=10
sohpi=100/10=10
pohar=0
pohsi=0
```

And in the POH case
```
sohsi=(1000-10)/(100-10)=11
sohpi=0
pohar=10
pohsi=1
```

# The GC Performance infrastructure
To put the design into practice, we will use the GC performance infrastructure. It is basically a test harness. It takes in a benchmark description, run `GCPerfSim` as instructed with trace collection enabled. The tool also automates the parsing of the trace and provide the data to us for further analysis. It is much easier to figure what it is by looking at an example, here is what I wrote for the benchmarks above:

```
vary: coreclr
test_executables:
  defgcperfsim: C:\dev\performance\artifacts\bin\GCPerfSim\release\netcoreapp5.0\GCPerfSim.dll
coreclrs:
  a:
    core_root: c:\dev\runtime\artifacts\tests\coreclr\Windows.x64.Release\Tests\Core_Root
options:
  default_iteration_count: 1
  default_max_seconds: 300
common_config:
  complus_gcserver: true
  complus_gcconcurrent: false
  complus_gcheapcount: 6
benchmarks:
  2gb_pinning:
    arguments:
      tc: 6
      tagb: 100
      tlgb: 2
      lohar: 0
      pohar: 0
      sohsr: 100-4000
      pohsr: 100-4000
      sohsi: 10
      lohsi: 0
      pohsi: 0
      sohpi: 10
      lohpi: 0
      sohfi: 0
      lohfi: 0
      pohfi: 0
      allocType: reference
      testKind: time
  2gb_poh:
    arguments:
      tc: 6
      tagb: 100
      tlgb: 2
      lohar: 0
      pohar: 10
      sohsr: 100-4000
      pohsr: 100-4000
      sohsi: 11
      lohsi: 0
      pohsi: 1
      sohpi: 0
      lohpi: 0
      sohfi: 0
      lohfi: 0
      pohfi: 0
      allocType: reference
      testKind: time
scores:
  speed:
    FirstToLastGCSeconds:
      weight: 1
    PauseDurationMSec_95P:
      weight: 1
```

The file is pretty self explanatory on what we want to infrastructure to do. The detailed definition of these fields can be found in the documentation. The infrastructure will take this file as an input and run the specified benchmarks and collect the data for us.

The documentation is fairly clear about how to setup and run the benchmarks. In the following post, I will talk about analyzing the result of the run.