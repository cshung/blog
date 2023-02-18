---
title: "Rosalind - Counting Subsets"
date: 2019-09-08T13:39:00.000-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/sset/).

**Solution:**

The number of subsets of a set of size \\( n \\) is \\( 2^n \\). The following code implements the repeated squaring algorithm. The numbers are computed modulo \\( 1000000 \\) as soon as we are done with multiplying. This is done to keep the number small, and so that the multiplications are quick.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/sset.py">}}

