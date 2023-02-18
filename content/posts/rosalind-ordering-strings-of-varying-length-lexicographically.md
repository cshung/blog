---
title: "Rosalind - Ordering Strings of Varying Length Lexicographically"
date: 2021-02-14T15:55:00.002-08:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/lexv/). 

**Solution:**

I wanted to implement a solution that is analogous to an odometer. The key thing that we wanted to understand is how we can advance from the current word to the next one. Suppose we have the next function, then we can just repeatedly use that function to produce the next one. The next function would probably not work on the last element, so that will naturally provide us with a stopping condition as well.

With that, here are a few rules for the next function that we can observe from the example.

**1. If the string is not currently full length, just append the least character 'D' at the end.**

Otherwise, the string must be full length, now we observe:

**2. If the last character is not the greatest character 'A', just advance to the next one.**

Otherwise, we could have a string of the greatest character 'A' at the end. So

**3. We eliminate all the trailing greatest character, and then advance the last character.**

The only tricky case here is whether or not we have the last character at all, because it could be 'AAA', so the eliminated string is empty and that is when we should stop.

The code simply implements all these rules.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/lexv.py">}}

