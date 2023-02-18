---
title: "Rosalind - Finding a Protein Motif"
date: 2019-08-11T10:18:00.001-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/mprt/).

**Solution:**

I attempted to create a solution that processes each character exactly once, and I succeed, here is the state machine that works.

![](https://1.bp.blogspot.com/-NQHyqiPsygE/XVBNmaGB4GI/AAAAAAAADKw/c0G5WSSKHeMOtGh2zr9u8M0lxy9ofSFXQCLcBGAs/s1600/Screen%2BShot%2B2019-08-11%2Bat%2B10.16.59%2BAM.png)

The diagram should be self-evident - the code is a just a faithful implementation of the diagram.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/mprt.py">}}

