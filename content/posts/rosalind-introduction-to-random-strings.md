---
title: "Rosalind - Introduction to Random Strings"
date: 2019-09-08T13:28:00.001-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/prob/).

**Solution:**

As we were told in the description, the probability of getting 'A' or 'T' is \\( \frac{1-x}{2} \\) and the probability of getting 'G' or 'C' is \\( \frac{x}{2} \\). Assuming independence (sadly the problem does not mention that), the probability of the whole string is the product of these probabilities. 

To make it numerically stable, instead of computing the product and then compute the logarithm, it is better to compute the logarithm and sum them up.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/prob.py">}}

