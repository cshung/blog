---
title: "Rosalind - Speeding Up Motif Finding"
date: 2020-09-18T19:36:00.003-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/kmp/).

**Solution:**

The z array generated in the advanced version of KMP can be used to recover the standard failure array.

This analysis look a bit weird because this description is written way after the code is written. I have a significant debt of documenting my code.

Recall that the z array tell us the length of the z-box at the beginning of the z-box, but the failure array, wants us to report the length of a matching string at the end, therefore, we can use a simple walk to solve this problem.

Whenever we encounter a z-box while we are not already inside one, we set the state to be (1, z[i]), the idea is that this is the first character in the z-box, and we know where the z-box end.

Suppose we see another z-box start while inside a z-box, and suppose that z-box extend beyond the furthest known z-box end, then we can record at that point how many characters is already consumed, and the end of the new z-box there.

The rest is simple. If we see a case where we are inside a z-box, then just report the character consumed so far as the failure array value, and if we are also not at the end of the z-box, advance the state forward.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/kmp.py">}}{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/libkmp.py">}}