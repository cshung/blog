---
title: "Rosalind - Catalan Numbers and RNA Secondary Structures"
date: 2020-11-14T14:49:00.000-08:00
draft: false
tags: []
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/cat/).

**Analysis:**

In order to produce a non-crossing matching, we need to pair the first nucleotide with a matching one, and then we match the rest. In order to be non-crossing, the one covered within the pairing must be independent of the one outside of the covering, that produces to independent sub-problems. 

**Solution:**

That observation immediately suggests a dynamic programming approach where sub-problems are computed in a memoized fashion. For any subproblem, the first thing to check is whether or not the length is multiple of 2, this is actually an optimization that saves a significant amount of time as an odd lengthed substring would never be able to have any perfect matching. Next, we need to find matching pairs, and we can use a simple preprocessing to make this operation fast. In particular, all we needed to know is where is the next matching one, and once we found it, then the next one that is the same, until we reach the end.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/cat.py">}}

