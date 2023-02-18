---
title: "Rosalind - Locating Restriction Sites"
date: 2019-09-01T16:15:00.001-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/revp/).

**Solution:**

A restriction site is basically a palindrome (with the complement twist). I modified the Manacher's algorithm to solve this problem.

First, when trying to expand to find palindrome half-length, I apply the complement rule.

Second, I skipped the '#' sign thing because we knew all palindromes will be of even length.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/revp.py">}} 

