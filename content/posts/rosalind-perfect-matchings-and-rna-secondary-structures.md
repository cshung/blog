---
title: "Rosalind - Perfect Matchings and RNA Secondary Structures"
date: 2020-08-30T14:10:00.002-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/pmch/).

**Solution:**

Denote the number of 'A' to be x. Fixing a U, there are x choices. Once an A is used, it cannot be used anymore. Therefore, there are x! way to bond the A and U pairs. Similarly, we have y! way to bond the C and G pairs (where y is the number of 'C'). Therefore the total number of ways to bond these pairs is x!y!

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/pmch.py">}}

