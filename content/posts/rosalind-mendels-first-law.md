---
title: "Rosalind - Mendel's First Law"
date: 2019-08-03T20:47:00.003-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/iprb/).

**Solution:**

When two heterozygous organisms mate, the offspring have \\( \frac{1}{4} \\) chance to be homozygous recessive.

When a heterozygous organism and a homozygous organism mate, the offspring have \\( \frac{1}{2} \\) chance to be homozygous recessive.

When two homozygous recessive organisms mate, their offspring must be homozygous recessive.

There are no other ways to produce a homozygous recessive offspring.

The probability of getting a homozygous recessive is therefore:

\\( \frac{m}{k + m + n} \times \frac{m-1}{k + m + n - 1} \times \frac{1}{4} + \frac{m}{k + m + n} \times \frac{n}{k + m + n - 1} \times \frac{1}{2} + \frac{n}{k + m + n} \times \frac{m}{k + m + n - 1} \times \frac{1}{2} + \frac{n}{k + m + n} \times \frac{n - 1}{k + m + n - 1}\\)

Notice the middle terms looks duplicated but they are not. There are two ways we can have a heterozygous organism and a homozygous organism mate. Either the first one is heterozygous or the second one is.

Note that we need the probability of possessing a dominant allele, all we need to subtract it by 1.

The following code simply implements the formula above. Care is taken to avoid duplicated arithmetic operations. Not that it matters, but that save typing and reading :)

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/iprb.py">}}

