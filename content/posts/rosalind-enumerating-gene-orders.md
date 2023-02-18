---
title: "Rosalind - Enumerating Gene Orders"
date: 2019-08-10T14:28:00.001-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/perm/).

**Solution:**

A simple recursive solution is to pick one out of the list, and compute the permutation of the rest. The problem is, how do we represent 'the rest'? My approach is that we make sure the suffix of the list is 'the rest', and therefore we can simply use an integer to represent the rest.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/perm.py">}}

