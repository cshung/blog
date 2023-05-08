---
title: "SPOJ Problem Set (classical) - The Next Palindrome"
date: 2014-09-06T14:00:00.001-07:00
draft: false
tags: [spoj,adhoc]
---

**Problem:**

Please find the problem [here](http://www.spoj.com/problems/PALIN/).

**Solution:**

An adhoc problem. The key idea to solve this problem is to try to keep the left hand side as small as possible.

For example, if the input is 1921, we can assert the answer is 1991. To see why, decreasing any digit on the right hand side must also decrease a digit on the left hand side, and with we decrease any digit on the left hand side, the number is less than 1900!

The same apply the number with odd number of digits. Suppose the input is 19321, the same idea apply and the answer is 19391.

So we wanted to flip the left hand side to the right hand side. But unfortunately, sometimes, that doesn't work. Consider the input 1234, 1221 is not an answer, not even 1221 because the problem requires strictly greater. With this example, we also know flipping work if and only if reverse(left) > right.

So what if reverse(left) <= right? In that case we have no choice but to increase the value of the left hand side, minimally so we only plus one. For example, the answer of 1234 will be 1331. For number with even number of digits that is not all 9s, that is a correct solution. To see why, let the number be AB, where A is left part and B is the right part, we also know A reverse(A) <= AB, so the least possible palindrome is (A+1) reverse(A+1). That works if A is not all 9s. If A is all 9s, let say, the number is 9981, than the answer is NOT 100001, but just 10001. 

Last but not least, we also deal with the case with odd number of digits. This time we can increase the middle digit. Suppose the number is AmB, where A is the left part, m is the middle digit, and B is the right part, we also know Am reverse(A) <= AmB, so we have to increase m, if m is not 9. If m is 9, then we have no choice but to increase A, and if A is also all 9s, then we just fall back to the same solution as the all 9s case.

**Code:**

{{<github "https://raw.githubusercontent.com/cshung/Competition/main/Competition/SPOJ_PALIN.cpp">}}