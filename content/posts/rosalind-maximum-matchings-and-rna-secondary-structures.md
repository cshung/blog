---
title: "Rosalind - Maximum Matchings and RNA Secondary Structures"
date: 2020-12-16T10:27:00.005-08:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/mmch/). 

**Solution:**

Suppose we have 3 A and 2 U, the first U can choose from 3 A, the second choose from 2 A, therefore, the number of matches for these 3 A and 2 U is 3 x 2 = 6. In general, the answer is the falling factorial. 

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/mmch.py">}}

