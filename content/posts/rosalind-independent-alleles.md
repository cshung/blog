---
title: "Rosalind - Independent Alleles"
date: 2019-08-10T13:05:00.000-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/lia/).

**Analysis:**

The code is deceptively simple. Let's not read the code and understand the problem first. Because A and B are independent, we can consider them separately.

Tom's offspring has the following distribution:

```text
AA - 0.25

Aa - 0.5

aa - 0.25
```

To consider the next generation, let's generalize and consider the next generation of an arbitrary distribution. Let's say the current distribution is

```text
AA - a

Aa - b

aa - c
```

The next generation would be

```text
AA - 0.5  a + 0.25 b

Aa - 0.5  a + 0.5  b + 0.5 c

aa -          0.25 b + 0.5 c
```

It can be written in a matrix form:

\\( \begin{eqnarray*}   \left(\begin{array}{c}a'\\\\b'\\\\c'\end{array}\right) = \left(\begin{array}{ccc}0.5 & 0.25 & 0 \\\\ 0.5 & 0.5 & 0.5 \\\\ 0 & 0.25 & 0.5\end{array}\right)\left(\begin{array}{c}a\\\\b\\\\c\end{array}\right)  \end{eqnarray*} \\) 

Calculating the distribution for the second generation, we have an interesting finding!

\\( \begin{eqnarray*}   \left(\begin{array}{c}0.25\\\\0.5\\\\0.25\end{array}\right) = \left(\begin{array}{ccc}0.5 & 0.25 & 0 \\\\ 0.5 & 0.5 & 0.5 \\\\ 0 & 0.25 & 0.5\end{array}\right)\left(\begin{array}{c}0.25\\\\0.5\\\\0.25\end{array}\right)  \end{eqnarray*} \\)

So we accidentally found \\( \left(\begin{array}{c}0.25\\\\0.5\\\\0.25\end{array}\right) \\) is an eigenvector with eigenvalue 1. In other words, the probability of Aa will always be 0.5, regardless of generation.

Because A and B are independent, now we can use independence to conclude the probability of an offspring to be AaBb is 0.25. To calculate what is the probability of at least N offsprings are AaBb. We calculate the probability that at most N - 1 offsprings are not AaBb. This is simply the binomial distribution, and we are asking for the cumulative distribution function of it.

**Solution:**

The rest is simply a smart loop for computing the cumulative distribution function of the binomial distribution. The binomial coefficient can be expressed as

\\( \begin{eqnarray*}   \left(\begin{array}{c}n\\\\r\end{array}\right) = \frac{n!}{r!(n - r)!} = \frac{n \times (n - 1) \times \cdots \times (r + 1)}{(n - r)!} \end{eqnarray*} \\) 

Therefore the loop is simply computing the quantity. Note that the division is integer division, and it is okay because whenever the division by \\( k \\) happens, the numerator has accumulated \\( k \\) consecutive value, meaning it must be a multiple of \\( k \\).

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/lia.py">}}

