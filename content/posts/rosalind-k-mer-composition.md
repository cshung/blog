---
title: "Rosalind - k-Mer Composition"
date: 2021-01-30T15:16:00.001-08:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/kmer/). 

**Solution:**

To produce the array, slide a window of width 4 from the leftmost to the rightmost of the string. When processing a window, we quickly compute the k-mer index as a base 4 number. 

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/kmer.py">}}

