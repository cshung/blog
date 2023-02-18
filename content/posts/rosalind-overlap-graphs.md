---
title: "Rosalind - Overlap Graphs"
date: 2019-08-10T11:11:00.001-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/grph/).

**Solution:**

To construct the graph, we go through every pair, excluding self-loops, and output an edge when the suffix matches the prefix. Care is taken to make sure the string actually has at least 3 characters.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/grph.py">}}

