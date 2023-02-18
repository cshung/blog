---
title: "Rosalind - Mortal Fibonacci Rabbits"
date: 2019-08-05T22:45:00.002-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/fibd/).

**Solution:**

The matrix solution I used in my [previous solution](../rosalind-rabbits-and-recurrence-relations) is still applicable, but it is cumbersome to implement. It is time to actually use dynamic programming.

Conceptually, it is easy. We know rabbits can have life from \\( 0 \\) to \\( m - 1 \\) month, so we keep an array to see how many pairs of rabbits are there at that age.

With that, every time we need to update the whole array, and most array entries are just shifted. To reduce the time spent, I maintained a variable named offset. The idea is that instead of actually moving the array entry, we can simply move the way we interpreted the array. That way, I reduced the cost from \\( O(nm) \\) to \\( O(n + m) \\).

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/fibd.py">}}

