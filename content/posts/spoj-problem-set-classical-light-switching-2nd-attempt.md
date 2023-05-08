---
title: "SPOJ Problem Set (classical) - Light switching (2nd attempt)"
date: 2014-10-26T08:38:00.003-07:00
draft: false
tags: [segment tree,divide and conquer,spoj]
---

**Problem:**

Please find the problem [here](http://www.spoj.com/problems/LITE/).

**Solution:**

After doing some optimization - the previous solution still cannot be accepted - need to try something else. Previously I was worried about memory consumption creating a full blown segment tree by virtualizing the segments. What if I just create it? Again, we consider this simple example of inserting the segment [1, 5] and [3, 7]

Initially the tree is completely empty.

<table border=1><tbody><tr><td colspan=4>0</td><td colspan=4>0</td></tr><tr><td colspan=2>0</td><td colspan=2>0</td><td colspan=2>0</td><td colspan=2>0</td></tr><tr><td>0</td><td>0</td><td>0</td><td>0</td><td>0</td><td>0</td><td>0</td><td>0</td></tr><tr><td></td><td></td><td></td><td></td><td></td><td></td><td></td><td></td></tr></tbody></table>

Now we insert [1, 5]

<table border=1><tbody><tr><td colspan=4 bgcolor=red>4</td><td colspan=4>1</td></tr><tr><td colspan=2>0</td><td colspan=2>0 </td><td colspan=2>1</td><td colspan=2>0 </td></tr><tr><td>0</td><td>0 </td><td>0 </td><td>0 </td><td>1</td><td>0 </td><td>0 </td><td>0 </td></tr><tr><td></td><td></td><td></td><td></td><td></td><td></td><td></td><td></td></tr></tbody></table>

The highlighted cell is the key idea - I don't want to update all the leaves cells. If I do then that would be slow, and I would just brute force instead.

Now we insert [3, 7], it involve a cell that we lazily not propagating the changes down, so now we are forced to do, but let's do it just one level now.

<table border=1><tbody><tr><td colspan=4>4</td><td colspan=4>1</td></tr><tr><td colspan=2 bgcolor=red>2</td><td colspan=2 bgcolor=red>2</td><td colspan=2>1</td><td colspan=2>0 </td></tr><tr><td>0</td><td>0 </td><td>0 </td><td>0 </td><td>1 </td><td>0 </td><td>0 </td><td>0 </td></tr><tr><td></td><td></td><td></td><td></td><td></td><td></td><td></td><td></td></tr></tbody></table>

Now we are ready to go. The interval [3, 7] is splitted into [3, 4] and [4, 7]. Let's handle [3, 4] first. On the left hand side, all we need to do is to flip that lazy segment back and nothing further need to be done.

<table border=1><tbody><tr><td colspan=4>2</td><td colspan=4>1</td></tr><tr><td colspan=2 bgcolor=red>2</td><td colspan=2>0</td><td colspan=2>1</td><td colspan=2>0 </td></tr><tr><td>0</td><td>0 </td><td>0 </td><td>0 </td><td>1 </td><td>0 </td><td>0 </td><td>0 </td></tr></tbody></table>For the right hand side, it is similarly done splitted into [4, 6] and [7, 7]. For [4, 6], it covers the whole interval so we can also be lazy there too.

<table border=1><tbody><tr><td colspan=4>2</td><td colspan=4>2</td></tr><tr><td colspan=2 bgcolor=red>2</td><td colspan=2>0</td><td colspan=2 bgcolor=red>1</td><td colspan=2>1 </td></tr><tr><td>0</td><td>0 </td><td>0 </td><td>0 </td><td>1 </td><td>0 </td><td>1</td><td>0 </td></tr></tbody></table>

Last, consider a query from [2, 6], we follow the normal segment tree query except that we do the work when we hit a lazy node, and that's it. Turn out both lazy nodes will need to be done for this case.

Note that for updates or query, neighboring intervals are doubling in size, so worst case is just processing \\( O (\log n) \\) intervals. Working on the lazy node is constant work at a time so no need to worry about it for complexity sake. We get an \\( O (m \log n) \\) algorithm, this time \\( n \\) is the number of lights. This is fast enough and get accepted. Compare with the previous algorithm, the previous algorithm is independent of \\( n \\), so in theory one could feed it billions of lights without worrying about time, but for small number of lights this is superior.

Another great advantage of this approach is simplicity, it took me no more than two hours to get this done. The simplicity will also allow us to evolve this approach to do harder problems.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/Competition/SPOJ_LITE_2.cpp">}}