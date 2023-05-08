---
title: "SPOJ Problem Set (classical) - Fishmonger"
date: 2014-11-06T06:00:00.000-08:00
draft: false
tags: [dynamic programming,graph,Floyd Warshall,spoj]
---

**Problem:**

Please find the problem [here](http://www.spoj.com/problems/FISHER/).

**Solution:**

I started the problem using the a simple complete search of all path. Of course, we are not going to be able to enumerate through all the paths. But fortunately we don't have to, once we have tried a path we can memoize the result as follow.

If I am at a certain node n<sub>1</sub> with certain an initial time constraint T<sub>1before</sub>, and with time t<sub>12</sub> I can get to node n<sub>2</sub>, recursively we know the best path to get to market from n<sub>2</sub> with budget T-t<sub>12</sub> take time T<sub>2after</sub> and has minimal cost C<sub>2after</sub>, then we have one best path candidate from n<sub>1</sub> to n<sub>2</sub> within time constraint T<sub>1before</sub> that actually spend time T<sub>2after</sub> + t<sub>12</sub> with cost C<sub>2after</sub> + c<sub>12</sub>. Try all candidate and find the solution.

The key to the above idea is memoization - we save the fact that if we are going from node n<sub>1</sub>, the budget given is between T<sub>1before</sub> and T<sub>2after</sub> + t<sub>12</sub>, then the best path has the be the best path found, no point to find a best path again.

I have coded the solution, unforunately it is **Wrong Answer**. Removed memoization, clearly **Time Limit Exceeded**.  Looking in retrospect, could be similar bug that I hit in the approach below.

Now I tried something else, based on Floyd Warshall. Floyd Warshall is that we can find the shortest path between every two nodes by allowing incrementally more intermediate nodes. I thought, if instead of the shortest path, what if we maintain the set of [non dominated](http://stackoverflow.com/questions/17010914/algorithm-for-maximum-non-dominated-set) paths? By non-dominated, I mean paths that has either lower cost higher time, or path with higher time lower cost, but not higher time, higher cost ones. We also constraint the time of those path within time budget. At the end of the algorithm, we will get, between all pairs of nodes, the set of all non-dominated paths, and therefore we can pick the one with least cost within time budget at the end of the algorithm.

For non-dominated filtering, I just used the simple \\( O(p^2) \\) algorithm, where \\( p \\) is the number of candidate paths. There exists \\( O(p \log p) \\) algorithm, and maybe even better, I just didn't do it since the time is good enough for the problem to accept.

All is fine, except one bug I just can't find myself. When I initialized the table, I put the initial distance and initial cost between two nodes into the table. I didn't check if those values are within time budget, as a result, if there is a graph with a low cost path within short time budget that reach the market directly, I would have returned that path - wrong.

If I got more time, I can try modifying Dijkstra's algorithm or Bellman Ford's algorithm, basically the same idea of keeping non-dominated paths. These single source shortest path algorithm should be faster than the all pair shortest path problem, I believe.

As an aside - thanks a lot for those who helped me to find out the tricky test case that I missed. I guess I need to be a better adversary of my own code to find that out. I also got the feedback that the code is hard to read, I tried to make my code as descriptive as it can be, but I guess everybody have a different taste of what does it mean by readable, or well, code are just not fun to read, wrapping it with sugar just doesn't cover the bitterness.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/Competition/SPOJ_FISHER.cpp">}}