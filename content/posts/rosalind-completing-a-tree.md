---
title: "Rosalind - Completing a Tree"
date: 2019-09-01T16:37:00.001-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/tree/).

**Solution:**

By iteratively eliminating leaves, it is easy to see that a tree of \\( n \\) nodes has \\( n - 1 \\) edges. Therefore the solution is simply computing \\( (n - 1) \\) - number of edges, which is also equal to \\( n \\) - number of lines. Remember the first line is the number of nodes :)

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/tree.py">}}

