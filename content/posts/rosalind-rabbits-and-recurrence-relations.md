---
title: "Rosalind - Rabbits and Recurrence Relations"
date: 2019-07-28T15:25:00.003-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/fib/).

**Analysis:**

It is long known to me that the Fibonacci numbers are the solution to the rabbit's problem. I never really bothered to understand why that is the case. It feels natural to me. In order to solve this problem, I need to understand it in more depth.

Here is the problem description in its own words:

The population begins in the first month with a pair of newborn rabbits.

Rabbits reach reproductive age after one month.

In any given month, every rabbit of reproductive age mates with another rabbit of reproductive age.

Exactly one month after two rabbits mate, they produce one male and one female rabbit.

Rabbits never die or stop reproducing.

The word month is used as a point in time (e.g. first month) and also used as a duration of time (e.g. after one month). This is confusing. Here is how I interpreted it:

At \\( t = 1 \\), we have one pair of newborn rabbit, no mature rabbit. Denote that as

\\( n_1 = 1 \\)

\\( m_1 = 0 \\)

At \\( t = T \\) where \\( T > 1 \\), newborn rabbits becomes mature and mature rabbits produces newborns. Therefore we can denote that as:

\\( n_T = m_{T - 1} \\)

\\( m_T = m_{T - 1} + n_{T - 1} \\)

This is looking at the history of just one time slot behind, therefore it doesn't look like the Fibonacci numbers. And there is just one Fibonacci sequence, but we have two sequences here that are cross-coupled.

Because we knew the answer is the Fibonacci number, now we can try to prove it.

\\( \begin{eqnarray*} & & (n_{T - 2} + m_{T - 2}) + (n_{T - 1} + m_{T - 1}) \\\\ &=& (n_{T - 2} + m_{T - 2}) + (m_T) \\\\ &=& (m_{T - 1}) + (m_T) \\\\ &=& n_T + m_T \end{eqnarray*} \\)

So there we go, we derived the Fibonacci recursion.

Without the apriori knowledge about the Fibonacci numbers, there isn't a mechanical way to reduce the two sequences into one. And ultimately we don't really need that, because ...

**Solution:**

... we will use the matrix solution. The formula above is well suited for implementing as matrix multiplications.

\\( \begin{eqnarray*} \left(\begin{array}{cc} 0 & k \\\\ 1 & 1\end{array}\right)\left(\begin{array}{c}n_{T-1} \\\\ m_{T-1}\end{array}\right) = \left(\begin{array}{c}n_T \\\\ m_T\end{array}\right) \end{eqnarray*} \\) 

Once we have the matrix formula, it is obvious that we can make it a matrix power problem and solve it using repeated squaring.

\\( \begin{eqnarray*} \left(\begin{array}{cc} 0 & k \\\\ 1 & 1\end{array}\right)^{T-1}\left(\begin{array}{c}1 \\\\ 0\end{array}\right) = \left(\begin{array}{c}n_T \\\\ m_T\end{array}\right) \end{eqnarray*} \\) 

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/fib.py">}}

