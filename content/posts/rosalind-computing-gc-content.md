---
title: "Rosalind - Computing GC Content"
date: 2019-07-28T20:28:00.001-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/gc/).

**Solution:**

The only challenge for this problem is to parse the FASTA format. To make life easy, I added a '>' sign at the end of all the inputs - now every DNA is simply a '>' terminating string. Therefore a simple scan of the file parses all the input correctly.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/gc.py">}}

