---
title: "Rosalind - Enumerating k-mers Lexicographically"
date: 2019-09-01T16:26:00.001-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/lexf/).

**Solution:**

The key to do this problem is to interpret these k-mers as an integer with the number of alphabets as a base. That way enumerating them lexicographically is simply looping.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/lexf.py">}}

