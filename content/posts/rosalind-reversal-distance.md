---
title: "Rosalind - Reversal Distance"
date: 2020-09-26T16:23:00.003-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/rear/).

**Solution:**

This is a tough problem, I handled it using a few techniques.

The problem hinted on brute force, in order to make sure it runs within the allocated time, it is easy to just pre-compute the solutions.

The set of all solutions is daunting (10!) squared. I make one observation to make it simple - the symbol themselves does not matter, we could rename all the symbols consistently and we will get exactly the same reversal distance. Therefore, I reduced the problem of finding the distances between all pairs to the distance of all permutations to a canonical one. That makes the problem much smaller.

The first code consumed the pre-computed result and output distance as needed. The key variables are the parents. Parents represent the shortest path tree, following the parent pointers, it reaches the root in the shortest path. (The reason why we don't simply pre-compute the distances is to make it possible to solve the [next problem](../rosalind-sorting-by-reversals))

The parents are computed using BFS in the second code, that is just the standard BFS. A permutation is represented by the number and the representation is encoded by a number using the encode/decode functions. 

The encode and decode functions implements [Lehmer's code](https://en.wikipedia.org/wiki/Lehmer_code). That code maps the set of permutations into the range [0, n!), making it very convenient to use.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/rear.py">}}{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/reargen.py">}}{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/libperm.py">}}