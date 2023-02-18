---
title: "Rosalind - Finding a Motif in DNA"
date: 2019-08-03T21:01:00.003-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/subs/).

**Solution:**

This feels like cheating. The problem is obviously asking for implementing a substring search, and I am using the Python builtin.

I have implemented [Boyer Moore](https://github.com/cshung/MiscLab/blob/master/BoyerMoore/BoyerMoore/BoyerMoore.cs) in C#, so I don't feel bad. In fact, I validated my implementation using the Rosalind test cases, and it passed there as well.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/subs.py">}}

