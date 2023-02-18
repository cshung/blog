---
title: "Rosalind - Calculating Expected Offspring"
date: 2019-08-10T11:29:00.000-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/iev/).

**Solution:**

The probability for AA-AA to produce an offspring with a dominant phenotype offspring is 1.

The probability for AA-Aa to produce an offspring with a dominant phenotype offspring is 1.

The probability for AA-aa to produce an offspring with a dominant phenotype offspring is 1.

The probability for Aa-Aa to produce an offspring with a dominant phenotype offspring is 0.75.

The probability for Aa-aa to produce an offspring with a dominant phenotype offspring is 0.25.

The probability for aa-aa to produce an offspring with a dominant phenotype offspring is 0.

The code is simply an implementation of the numbers above, the number is easily obtained by listing all possible offsprings.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/iev.py">}}

