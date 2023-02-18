---
title: "Rosalind - Enumerating Oriented Gene Orderings"
date: 2020-07-24T12:41:00.001-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/sign/).

**Solution:**

Like the [previous problem](../rosalind-enumerating-gene-orders), we use a recursive routine to generate all permutations. For each permutation, we will use another recursion to give each element a sign.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/sign.py">}}

