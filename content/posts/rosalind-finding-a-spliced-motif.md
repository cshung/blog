---
title: "Rosalind - Finding a Spliced Motif"
date: 2019-09-08T13:42:00.003-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/sseq/).

**Solution:**

To find the corresponding indexes, we initialize two pointers to the beginning of both strings. Walk the sequence pointer one by one, and walk the subsequence pointer only when there is a match. This way we will find all the indexes.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/sseq.py">}}

