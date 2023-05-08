---
title: "SPOJ Problem Set (classical) - Candy I"
date: 2018-09-02T18:49:00.001-07:00
draft: false
tags: [spoj]
---

**Problem:**

Please find the problem [here](https://www.spoj.com/problems/CANDY/).

**Solution:**

A necessary condition is that the total number of candies can be distributed evenly, therefore we check if sum % N == 0. In that case, all bags with less candies must get filled, and there must be one available. So we sum the total number of candies needed to fill and that's the answer.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/Competition/SPOJ_CANDY.cpp">}}