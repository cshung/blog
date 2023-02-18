---
title: "Rosalind - Open Reading Frames"
date: 2020-08-20T13:20:00.002-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/orf/).

**Solution:**

There are two parts to this problem. We need to find all the start and stop codons, and we need to pair them up so that we know where to start and stop decoding.

The former is performed using the Aho Corasick algorithm, by using a trie, we can match all codons in a single pass.

Imagine we are walking from left to right, whenever we see a stop codon, we want all the start codon before it that is after the last stop codon. That structure perfectly mirror a stack, that is what I used.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/orf.py">}}

