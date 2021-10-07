---
title: "Optimize Bricks"
date: 2021-10-06T16:04:13-07:00
draft: false
---

In this post, I am going to talk about an optimization I made in the GC to reduce tail latency.

# Scenario
One of our customers reported that occassionally, they are observing gen 1 GC taking longer time than usual. Usually, their gen 1 GC takes around 4 milliseconds, but once in a while it could be as long as 15 milliseconds, and this is getting into their tail latencies measurements.

# Customer analysis
By collecting profiles, they indicated that the method `find_first_object` is taking 70% of time when we hit a long gen 1 GC, indicating that we are spending a lot of time trying to find the objects given the interior pointers.

# Too many interior pointers?
My first reaction is that there could be two reasons, either we have a lot of interior pointers, or searching for interior pointers is time consuming in some cases. Since PerfView profiles does not provide call counts, I instrumented the GC to log the number of calls to `find_first_object`. 

Turn out in both the usual and the long gen 1 GC, we have more or less the same number of interior pointers, so we are not hitting a scenario where we have too many interior pointers. It has to be the case that processing interior pointers is slow in a particular case.

# Loop counts
There are two loops in the `find_first_object` function. The first loop walk backwards to find a brick, and the next loop walks forward to find the object. Apparently the second loop is going to spend much more time because a brick may contains many objects. So I instrumented the GC again to log two things - sum of the number of objects and sum of the number of object squared.

With the sums, we can compute the average and the standard deviation of the number of objects (across different `find_first_object` calls within the same GC). This time around the result is expected. In the long GC case, the average number of objects is high while it is low in the usual case. Surprisingly, we have a huge standard deviation in the long GC case (so big that the sum of square overflows a 64 bit integer). That high standard deviation indicates the distribution is very skewed. Maybe it is just one object with huge object count?

# The self healing property
Turn out when we `find_first_object`, the code can build the brick table if the brick is walked. So if we had a case where the bricks are not built, the brick will be built after 1 bad call, that explains why we have a high standard deviation. It is just that we have a case where the bricks are not available.

# The interesting case revealed
With some statistics available, we have some idea with the unusual number of iterations in the loop. So we can instrument the build again to capture that case. Logging indicates that it is always finding a gen 0 object, and the bricks for the whole gen 0 is not availble. That indicates to me that it might be the case where we cleared the gen 0 bricks.

In the code, there is a convenient way to clear the gen 0 bricks by setting `gen0_bricks_cleared` to false. So the next question is why did we set it.

# Who cleared my bricks?
There are only a few places where the brick are cleared, so it is relatively straightforward to record the reason by setting an integer variable and log them. The logging indicates that we cleared the bricks before we conclude the `background_mark_phase` and then once again after the `background_mark_phase` completes. In the former case, the code clears the bricks right away, but in the latter case, `background_ephemeral_sweep` is going to build the bricks right away, but not until we hit the next `find_first_object` call then we will clear them, so it looks weird to me why we would want to build the bricks just to clear it later.

Turn out that was not needed, and therefore I eliminated the second clearing.

# Experiment results
Before the change, we used to hit the long gen 1 GC within first 20 minutes of running the performance test. After the change, we are not observing it in 6 hours. This gives us some good confidence that this optimization fixed the issue.

# Conclusion
This conclude my experience with optimizing the GC for a particular problem. Instrumentation played an important role in nailing this issue. This is possible only because we can reproduce the issue with instrumented build. Special thanks to our customer who worked on arranging this.