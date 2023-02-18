---
title: "Rosalind - Complementing a Strand of DNA"
date: 2019-07-28T14:53:00.001-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/revc/).

**Solution:**

This problem has an in-place solution. We can walk the string from two ways and swap and map at the same time. However, Python strings are immutable. We have to create new strings.

To build a string, I was thinking about something like a StringBuilder, but there is no such thing in Python. It is [reported](https://waymoot.org/home/python_string/) that doing repeated string concatenation is the fastest way. So I simply constructed the new string using repeated string concatenation.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/revc.py">}}

