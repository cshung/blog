---
title: "POH Tuning (Part 3 - Other statistical properties)"
date: 2021-03-01T20:55:26-08:00
draft: false
---

In the last post, we discussed what the benchmark does and how the weights are computed. In this post, we will talk about some other interesting statistical properties. The lesson learned here is that we know how objects behave in the benchmark, and we can use this to judge whether or not the benchmark actually matches with real-life use cases. 

# Object lifetime
In this previous post, we know about an object's life cycle. In a single iteration, we pick a victim in the array to free and add one back into the array. If we pick the `allocType` to be `simple`. The victim is simply chosen in a uniformly random manner. We are interested in the lifetime of the object. 

To be more concrete, let the size of the array be \\( N \\). A particular object being chosen to be freed has probability \\( \frac{1}{N} \\). If an object is not chosen to be freed, its lifetime extends by an iteration. If an object has a lifetime \\( T \\), then it must have survived \\( T \\) times and failed to survive in the last time. This corresponds to the probability \\( \left(\frac{N-1}{N}\right)^{T}\frac{1}{N} \\). 

This is known as the [geometric distribution](https://en.wikipedia.org/wiki/Geometric_distribution). It's property is well studied. In particular, the expected object lifetime is \\( N \\). In a typical run. \\( N \\) is approximately 1,000,000 and the number of iteration is approximately 10,000,000 and takes time around 10 seconds. This translates to around 1,000,000 iterations per second, and the object's lifetime is around 1 second.

For the `allocType` to be `reference` case, things get more complicated and I did not perform the analysis there. The key issue is that we always free the list's head. I will talk more about the lists in the next section.

# List lengths
In the `allocType` being `reference` case, we build lists. A `ReferenceItemWithSize` object has a field `next` that points to another instance of it. This allows us to build a linked list. A linked list is interesting because in the garbage collector, when we mark the objects, we need to follow pointers, and the longer the list, the more following needs to happen, and that will lead to interesting performance characteristics.

Abstractly, we can think of the simulator maintain an array of lists. In each iteration, we are still doing the same thing. Pick a random victim, and add a new object. The difference is that we are picking a random list, and we are always freeing the head of the list, and when we add a new item, we either create a new list of length 1 (with a tunable probability \\( p \\) or we add it to the tail of an existing list. 

The reason behind this design choice is that we observed, in the last section, that the object lifetime is short. And we wanted to lengthen the lifetime, we better able to make it less probable to be chosen to free, and we can do that by making the young objects in the list tail and free only the list head.

This design leads us to an interesting question - how long will the lists be? At any time, there will be multiple lists of different lengths. I attempted to analyze it, but it is difficult. I asked around (for example, on math stack exchange [here](https://math.stackexchange.com/questions/4005189/random-list-lengths/4012037#4012037)). Here I present a 'solution' from an anonymous who helped me with the analysis. I put the 'solution' in quotes because it isn't exactly rigorous, but it is approximately right and does match with experimentation.

Let \\( a_i \\) be the number of lists of length \\( i \\). Let \\( A \\) be the total number of lists.

In this language, the probability of destroying a list of length 1 is \\( \frac{a_1}{A} \\), and the probability of creating a list of length 1 is simply \\( p \\). The probability of destroying a list of length \\( k \\) is \\( \frac{a_{k}}{A} \\), and the probability of creating a list of length \\( k \\) is \\((1 - p)\frac{a_{k-1}}{A} \\).

Assuming we reached a steady-state, then the probability of creating a list of length \\( x \\) is equal to the probability of destroying a list of the same length \\( x \\). That leads to these recursive equations that we could use to solve:

$$
\begin{eqnarray}
\frac{a_1}{A} &=& p \\\\
\frac{a_k}{A} &=& (1-p)\frac{a_{k-1}}{A} \\\\
\end{eqnarray}
$$

It is not hard to see that \\( a_{k} = p(1-p)^{k - 1}A \\). Again, we see the geometric distribution pattern. The longer the list, the less probable it is. This matches experimental observation pretty well.

As a caveat, we know this is not a rigorous solution because there isn't a steady-state. We know that the number of lists is changing every single iteration, so all these values are not constants at all. But that's okay, we are not looking for mathematical rigor here, we are just trying to reason about what the system looks like. 