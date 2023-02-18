---
title: "Rosalind - RNA Splicing"
date: 2019-09-01T16:19:00.001-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/splc/).

**Solution:**

I am lazy with this one. I could try my hands on the rope data structure, but I didn't. I simply reconstructed the string again and again on the splicing.

To implement this properly with the rope data structure is going to be challenging. In particular, I need to implement my own version of fast substring search on the rope data structure.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/splc.py">}}

