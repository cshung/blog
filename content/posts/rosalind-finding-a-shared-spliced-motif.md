---
title: "Rosalind - Finding a Shared Spliced Motif"
date: 2019-09-08T13:59:00.001-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/lcsq/).

**Solution:**

This is the classic longest common subsequence problem, it can be solved using the Levenshtein edit distance algorithm.

If we disallow replacing characters, then the edit can be visualized as an alignment of two strings as follow:

|       |       |       |       |       |       |       |       |       |       |       |  
|-------|-------|-------|-------|-------|-------|-------|-------|-------|-------|-------|  
| **A** |       | **A** | **C** |   C   | **T** |       | **T** | **G** |   G   |       |  
| **A** |   C   | **A** | **C** |       | **T** |   G   | **T** | **G** |       |   A   |  

When the first string has a gap, this is an insertion operation. When the second string has a gap, this is a deletion operation.

To find the longest common subsequence is the same as trying the minimize the total gap length.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/liblevenshtein.py">}}

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/lcsq.py">}}

