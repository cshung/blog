---
title: "Rosalind - Genome Assembly as Shortest Superstring"
date: 2019-09-05T23:12:00.002-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/long/).

**Solution:**

The condition that there exists a unique path in the overlap graph is super important. Once we have the overlap graph, the topological sort would be the right answer, because the path would guarantee there is a single answer.

Unlike the earlier problem [Overlap Graphs](../rosalind-overlap-graphs), we do not know how long is the overlap. Therefore we need to be smart. We could try the overlap value, but that would lead to a quadratic algorithm. My key idea by modifying the [Knuth–Morris–Pratt algorithm](https://en.wikipedia.org/wiki/Knuth%E2%80%93Morris%E2%80%93Pratt_algorithm), we can return the alignment when the string search just ends. That way we know what is the maximum overlap value.

Of course, that means I need to implement the algorithm, and I did.

Code:

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/libkmp.py">}}

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/long.py">}}

