---
title: "POH Tuning (Part 2 - What am I running?)"
date: 2021-03-01T19:55:26-08:00
draft: false
---

In the [last post](http://cshung.github.io/posts/poh-tuning-1/), we talk about the event tracing infrastructure, it allows us to measure the metrics we wanted to study when we run the program. But what program do we want to run? The program that we will run is called a benchmark. In this post, I will look into the details of the benchmark program.

# Overview
The program that we will run is called `GCPerfSim`. As its name suggests, it simulates workload for the GC to understand its performance. We understand real-life workloads are very variable, therefore `GCPerfSim` has a lot of parameters that allow us to simulate different situations. In order to have some ideas of what these parameters mean, it is imperative for us to look into the program and see how it works and how the parameters impact its execution.

There are various modes in `GCPerfSim`, in this post, we will only focus on these parameters. They are the default from the `normal_server` benchmark coming from the performance infrastructure. We will cover the performance infrastructure itself in the next post.

```
-tc 6 -tagb 100.0 -tlgb 2.0 -lohar 0 -pohar 0 -sohsi 50 -lohsi 0 -pohsi 0 -sohsr 10-4000 -lohsr 102400-204800 -pohsr 100-204800 -sohpi 50 -lohpi 0 -sohfi 0 -lohfi 0 -pohfi 0 -allocType reference -testKind time
```

The most important parameter here is `testKind`. This tells us we will be running the `TimeTest` method, where it hosts the while loop to keep calling `MakeObjectAndMaybeSurvive`. The `allocType` parameter controls the type of object to allocate. For now, it will always be one of the subclasses of `ReferenceItemWithSize`.

The `MakeObjectAndMaybeSurvive` method can be divided into 3 phases:

- Determine what to do - this is driven by `bucketChooser.GetNextObjectSpec`, this returns a pseudo-random specification of what to do. Note that the method does some accounting of how many bytes are allocated, so it is important that the subsequent code allocates according to the spec exactly once.
- Create object - given the `ObjectSpec`, `JustMakeObject` will create the appropriate object(s) as instructed.
- Survive it - if the specification decides that this object should be survived, then `DoSurvive` will put it in an array that is strongly referenced to make sure the object survives. Note that the method also picks a random victim to discard. This is done to make sure the process doesn't run out of memory. The total number of elements in the array is determined by the `tlgb` argument.

# Object Specification generation
In view of the above, the key to the variation is the specification. Once we know the specification, the rest is pretty well known. The object specification generation is a random sampling process.

The first step to the sampling process is to choose a bucket. Each bucket has a weight, which is proportional to the probability of being chosen. These buckets correspond to each object heap in the GC. The weight values are actually computed from the `*ar` arguments to be explained later.

Once we have the bucket, the next thing is to decide the size, which should be a uniformly random number chosen within a range. These ranges are defined by the `*sr` arguments.

Last, we decide whether or not it is survived, pinned, or finalizable. These are decided by a simple nth element rule. For example, if we decided to survive every 100th object, then the tool will do exactly that. These numbers are defined by the `*i` arguments. Note that the code only pin survived objects, so the `*pi` value actually means how many survived objects should we see before we pin one.

# Weights
In the `GCPermSim` arguments, we have `lohar` and `pohar` to control how much of the allocation should happen on the large object heap and the pinned object heap, respectively. These are measured in the thousandth units. For example, when lohar = 150, it means out of 1000 bytes, 150 bytes are on the large object heap. This is obviously a rough number since the bucket choosing logic is based on random sampling.

In order to understand the weight computation, we need to understand the concept of overhead. Simply put, if we allocate an array for 100 bytes, it doesn't actually allocate exactly 100 bytes, it allocates for more. The runtime needs some space to store information such as the method table and the array size. For small objects, this overhead can be significant. Except for the array, GCPerfSim itself also needs to do some bookkeeping. For example, it needs space to store a pinning handle. That is why when the object spec dictates that we allocate `x` bytes in the LOH bucket, we actually allocate some of the overhead on the SOH (i.e. the size of the `ReferenceItemWithSize` object) and some of the overhead on the LOH (i.e. the array's overhead). To compensate for it, the size of the array is actually the requested size subtracted by the overhead so that the total allocates bytes exactly as required.

With the overhead part explained, now we are ready to discuss the weight computation. We need a bit of math. To discover the relation between the allocation ratio and the weights, suppose we knew the weights and compute the allocation ratios. Then we can solve these equations for the weight values since the allocation ratios are specified.

To do the math, we start with defining some symbols. Here is a table showing what the symbols mean, where \\( x \\) is a subscript for an object heap.

Symbol      | Meaning
----------- | -------------------------------------------
$$ A_{x} $$ | The number of bytes allocated in \\( x \\).
$$ S_{x} $$ | The size of an object in \\( x \\).
$$ W_{x} $$ | The weight of the bucket for \\( x \\).
$$ O $$     | The overhead that is allocated in the SOH 
$$ N $$     | The total number of allocations.
$$ R_{x} $$ | The allocation ratio for \\( x\\).

The total number of bytes allocated in a bucket is simply a sum of the individual allocations. These individual allocation sizes are independent and identically, uniformly distributed random numbers. By the linearity of expectation, the expected number of bytes allocated is simply the sum of the expected value of individual allocations.

This leads to these equations:

$$ E[A_{soh}] = \left(\frac{W_{soh}}{1000} \times (E[S_{soh}] - O) + O\right) \times N  $$
$$ E[A_{loh}] = \left(\frac{W_{loh}}{1000} \times (E[S_{loh}] - O)\right) \times N  $$
$$ E[A_{poh}] = \left(\frac{W_{poh}}{1000} \times (E[S_{poh}] - O)\right) \times N  $$
       
Since we assumed the weights, so everything is known, except \\( N \\), so we will cancel out this unknown, by computing a ratio as follow:

$$
\begin{eqnarray}
\frac{R_{soh}}{R_{loh}} &=& \frac{W_{soh}(E[S_{soh}] - O) + O}{W_{loh}(E[S_{loh}] - O)} \\\\
\frac{R_{soh}}{R_{poh}} &=& \frac{W_{soh}(E[S_{soh}] - O) + O}{W_{poh}(E[S_{poh}] - O)}
\end{eqnarray}
$$

Now we think of the weights as the unknowns. Note that some of them are in the denominators, which is difficult, so we cross multiply

$$ 
\begin{eqnarray}
R_{soh} (E[S_{loh}] - O) W_{loh} &=& R_{loh} (E[S_{soh}] - O) W_{soh} + R_{loh}O \\\\
R_{soh} (E[S_{poh}] - O) W_{poh} &=& R_{poh} (E[S_{soh}] - O) W_{soh} + R_{poh}O
\end{eqnarray}
$$

While these equations seem complicated, the only unknowns are the \\( W \\) values, therefore, these are just two linear equations. The third equation is simply

$$ 
\begin{eqnarray}
  W_{soh} + W_{loh} + W_{poh} = 1000
\end{eqnarray}
$$

With that, we can readily solve for the weights.

# Conclusion
This post explained how the `GCPerfSim` program works for the particular set of parameters used for the normal_server benchmark. 