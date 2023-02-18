---
title: "Rosalind - Counting Phylogenetic Ancestors"
date: 2020-11-27T12:10:00.001-08:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem here. http://rosalind.info/problems/inod/

**Solution:**

We knew that a rooted binary tree with n leaves has \\( n - 1 \\) internal nodes. An unrooted binary tree is simply a rooted binary tree with an extra leaf attached to the root. Therefore, the number of leaves increased by \\( 1 \\) but the number of internal nodes left unchanged. So an unrooted binary tree with \\( n + 1 \\) leaves has \\( n - 1 \\) internal nodes. In other words, an unrooted binary tree with \\( n \\) leaves has \\( n - 2 \\) internal nodes.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/inod.py">}}

