---
title: "Rosalind - Finding a Shared Motif"
date: 2019-08-05T23:00:00.000-07:00
draft: false
tags: [rosalind]
---

**Problem:**

Please find the problem [here](http://rosalind.info/problems/lcsm/).

**Solution:**

This is a complicated solution. I spent almost a week on it. It is based on a suffix tree. Suffix tree itself is a complicated beast. I described it earlier in [this](http://andrew-algorithm.blogspot.com/search/label/Suffix%20Tree) series of posts, please check it out if you have no idea what is a suffix tree.

In the sequel, I assumed the suffix tree is built on a string built on the DNAs concatenated and separated by unique characters. Because the separation characters are unique, the substring that contains a separator cannot be repeated, so we can sure if there is a repeated substring, it must not contain the separator character.

Any internal node in the suffix tree can be interpreted in two ways. We can think of that as a repeated substring because it is a prefix of more than one suffixes. We can also interpret it as a collection of suffixes because all leaf descendants of that internal nodes are suffixes.

With the two interpretations, the solution is clear. If there is an internal node that represents suffixes with at least one prefix starting from each string, then the prefix it represents is a common substring. This is not unlike the lowest common ancestor problem, which we solved [here](../leetcode-lowest-common-ancestor-of-a-binary-tree). The differences are we are asking for the lowest common ancestor of more than two nodes with a tree with more than two children, but fundamentally it is the same approach.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/rosalind/lcsm.py">}}

