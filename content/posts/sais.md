---
title: "SA-IS Algorithm"
date: 2021-12-26T10:52:35-08:00
draft: false
---

Today, I would like to write about my understanding of the SA-IS algorithm for constructing a suffix array. 

# What is a suffix array?
Consider the string "banana", this string has 6 suffixes.

|suffix|index|
|------|-----|
|banana|0    |
|anana |1    |
|nana  |2    |
|ana   |3    |
|na    |4    |
|a     |5    |

And if we sort them, we have these:

|suffix|index|
|------|-----|
|a     |5    |
|ana   |3    |
|anana |1    |
|banana|0    |
|na    |4    |
|nana  |2    |

And therefore the suffix array is `[5,3,1,0,4,2]`.

The suffix array can be used for many applications, for example, in text searching and compression. In this blog post, we are not going to dive deep into these application areas, and focus on the construction algorithm instead.

# A first look at the problem
At a first glance, sorting a bunch of strings is easy. We can simply create all the suffixes and then just sort it. The time required will be \\( O(n^2 \log n) \\) because the sorting performs \\( O(n \log n) \\) comparison and each comparison takes \\( O(n) \\)  time. Can we do any better? After all, the suffix array's size is only \\( O(n) \\).

Indeed we can. The SA-IS algorithm can construct the suffix array in \\( O(n) \\)  time, and therefore it is asympotically optimal.

# Observation
Note that the costly operation above is the repeated string comparison, can we reduce the cost there? Indeed, most suffix array construction algorithms focus on that. While string comparison is costly, most of the string comparison operations are overlapping. For example, if we decides the suffixes pair:

$$ \alpha < \beta $$

Then we also have

$$ \gamma\alpha < \gamma\beta $$

Can we somehow leverage this? As we will see, the SA-IS is leveraging this idea again and again.

# The S type and L type suffixes
To start with, we introduce the '$' character that is lexicographically smaller than any other alphabets, and append it to the end of the string. This is just to make it easier to start our induction.

Then we introduce the concept of 'S' type and 'L' type suffix as follow:

- The '$' is a S type suffix by definition.
- If a suffix is lexicographically smaller than its immediately right hand side suffix, then it is a S type suffix.
- Otherwise it is a L type suffix.

> Intuitively, the S stands for smaller, and L stands for larger. We will soon see this is a bit misleading ...

Following our banana example, the suffix types are as follow:

```txt
banana$
LSLSLLS
```

Computing the type is fairly easy. The key idea is to compute it from the right. The '$' sign is always type 'S'. How about the others?
Determining that 'a$' is L type is trivial, all we need to do is to check that 'a' > '$'. In fact, in 'banana', all characters can be checked this way.

The only tricky part is if the initial characters are the same, then we cannot just compare one character.

In this case, we claim that the type is simply the type of the one on its right hand side. Here is an example.

```txt
paab$
 ?SLS
```

To determine the type of `aab$`, we claim that since 'a' = 'a', therefore, the type of `aab$` is simply the same as the type of `ab$`, which is S.

If we think about it, this is exactly leveraging our observation that string comparisons are overlapping. The string comparison is simply comparing the first letter and then compare the rest, but the rest is already computed, so we just use what we already have!

# Why bother with L and S?
If we look at the suffix array with the L and S attached, we will find something interesting.

|suffix|index|type|
|------|-----|----|
|$     |6    |S   |
|a     |5    |L   |
|ana   |3    |S   |
|anana |1    |S   |
|banana|0    |L   |
|na    |4    |L   |
|nana  |2    |L   |

It isn't very obvious with this short string. With a longer string, we will find that the 'S' type and 'L' type seems to cluster very well. This is because of this property:

In a suffix array, for all suffixes starting with the same character, all L suffixes goes before all S suffixes.

This property can be easily argued from the type assignment algorithm above. All L type suffixes must start with a character, either encounter the same character, or something smaller than it. The same goes with the S type (except the '$'). Symbolically, the L type must be `cccb` while the S type must be `cccd`. That is why all the L type suffixes are always smaller than the S type suffixes if they start with the same character.

> Recall S stands for smaller, but in fact, for the same character, S is actually larger, counterinituitive!

This classification immediately lead us to a fairly fast algorithm for a partial sorting. Just do a bucket sort on the first character and its type. Of course, we haven't sorted the bucket yet, and we will deal with that later.

|initial character|L type   |S type     |
|-----------------|---------|-----------|
|$                |         |$          |
|a                |a$       |           |
|a                |         |anana$,ana$|
|b                |banana$  |           |
|n                |nana$,na$|           |

Although we display that as a table, in code we will simply represent it as an array together with an auxillary array that tell us where the bucket starts and ends.

# Sorting the L type suffixes given the S types are sorted
Suppose we have the S type suffixes sorted (i.e. they are placed in the right positions in the suffix array), we can easily get the fully sorted suffix array using this surprising algorithm.

```txt
Scan the array from left to right
If the current suffix is not the full string
  If the suffix to the left of that suffix is L type
    Insert that into the end of the corresponding L type bucket
```

This is a bit abstract, so we will trace an example:

Step 1: Here are the initial buckets, note that the S types are already sorted and the L types buckets are emptied. The parenthesis represents the empty buckets, the suffixes outside the parenthesis are still in the array but is not part of the bucket.

|initial character|L type     |S type     |
|-----------------|-----------|-----------|
|$                |           |$          |
|a                |a$()       |           |
|a                |           |anana$,ana$|
|b                |banana$()  |           |
|n                |nana$,na$()|           |

Step 2: The first one we find is '$'. The left suffix is 'a$', it is L type, so we put it into the right bucket.

|initial character|L type     |S type     |
|-----------------|-----------|-----------|
|$                |           |$          |
|a                |(a$)       |           |
|a                |           |ana$,anana$|
|b                |banana$()  |           |
|n                |nana$,na$()|           |

Step 3: The next one we find is 'a$'. The left suffix is 'na$', it is L type, so we put it into the right bucket.

|initial character|L type     |S type     |
|-----------------|-----------|-----------|
|$                |           |$          |
|a                |(a$)       |           |
|a                |           |ana$,anana$|
|b                |banana$()  |           |
|n                |nana$,(na$)|           |

Step 4: The next one we find is 'ana$'. The left suffix is 'nana$', it is L type, so we put it into the right bucket.

|initial character|L type     |S type     |
|-----------------|-----------|-----------|
|$                |           |$          |
|a                |(a$)       |           |
|a                |           |ana$,anana$|
|b                |banana$()  |           |
|n                |(na$,nana$)|           |

...

and so on, eventually, we will get the fully sorted suffix array.

The algorithm works for this example, and it will generally work for any cases, but it is unclear why this algorithm works. The name of this algorithm is called inductive sort, and it's name might give us a clue on how does it work.

Focusing on just the L bucket starting with `n`. We inserted `na$` in step 3 before `nana$` in step 4, and that is right order of insertion. The reason why we insert `na$` before `nana$` is because we saw `ana$` before `anana$`. This is why this algorithm is called inductive sort. The relative order to the L suffixes are induced by the relative order of the S suffixes.

The above is meant to give an intuitive idea, but it is not rigorous. You might wonder, what if we are talking about the full string, so that there is nothing on its right, or we are thinking about the right hand side of the L suffix is also an L suffix? To really convince us that the algorithm works, we need a proof, and we will prove this by induction.

1. The statement we wanted to prove is after the algorithm scanned a certain position, all positions on or to the left of it has the correct elements in it.

2. In the very first step, it is true because we will find '$', and '$' is obviously in the right position.

3. Now suppose the first `k-1` steps are good, considering the `k` step, and suppose we encounter a wrong suffix in the position.

4. Let the wrong suffix be \\( c \beta \\), and the correct one is \\(c \alpha\\). These are both L suffixes because the algorithm only move L suffixes within its own bucket.

5. \\( \alpha \\) and \\( \beta \\) cannot be empty string because a L suffix must have at least 2 characters long.

6. We have \\( \alpha \\) < \\( \beta \\) and also \\(c \alpha\\) > \\(\alpha\\), \\(c \beta\\) > \\(\beta\\) 

7. Since the correct suffix is \\(c \alpha\\), we must have seen \\( \alpha \\) earlier, so we must have inserted \\(c \alpha\\) already. 

8. But we see \\( c \beta \\) before we see \\(c \alpha\\), so it must be the case that we have also seen \\( \beta \\) earlier then \\( \alpha \\).

9. So both \\( \alpha \\) and \\( \beta \\) were seen, but in the wrong order. That is a contradiction because we assumed that order we seen earlier was correct.

# Relaxing the assumptions
Can we sort fewer S suffixes and still get the L suffixes correct? 

Looking at the proof, we can see how we could relax the assumption. We note that the proof does not use the fact that the S suffixes are sorted in a really cruical way. Here is a revised version of it, attempting to prove less with less given condition.

1. The statement we wanted to prove is after the algorithm scanned a certain position, all **L** positions on or to the left of it has the correct elements in it.

2. In the very first step, it is true because we will find '$', and it does not matter because we only cared about **L** positions.

3. Now suppose the first `k-1` steps are good, considering the `k` step, and suppose we encounter a wrong suffix in the position.

4. Let the wrong suffix be \\( c \beta \\), and the correct one is \\(c \alpha\\). These are both L suffixes because the algorithm only move L suffixes within its own bucket.

5. \\( \alpha \\) and \\( \beta \\) cannot be empty string because a L suffix must have at least 2 characters long.

6. We have \\( \alpha \\) < \\( \beta \\) and also \\(c \alpha\\) > \\(\alpha\\), \\(c \beta\\) > \\(\beta\\) 

7. Since the correct suffix is \\(c \alpha\\), we must have seen \\( \alpha \\) earlier, so we must have inserted \\(c \alpha\\) already. 

8. But we see \\( c \beta \\) before we see \\(c \alpha\\), so it must be the case that we have also seen \\( \beta \\) earlier then \\( \alpha \\).

9. So both \\( \alpha \\) and \\( \beta \\) were seen, but in the wrong order. 

In order to proceed with step 9, if it happens that both \\( \alpha \\) and \\( \beta \\) are type L, we can still use the induction hypothesis. If exactly one of \\( \alpha \\) and \\( \beta \\) are type S, then we can still argue that the original order should not mess up the relative ordering of L type and S type. The last case, that both \\( \alpha \\) and \\( \beta \\) are type S, is the true problem. We do need to make sure they were ordered properly before the algorithm starts.

It is just too good to be true if we can assume nothing and get the L type suffix sorted, turn out we still must sort some S type suffixes, but not all of them. It matters only if they can be \\( \alpha \\) or \\( \beta \\) above. This type of S suffixes has a special property that they are the immediate right hand side of a L type suffix. In other words, they are the left most S type suffixes, or LMS for short.

Suppose we can get the LMS suffixes sorted, then we can use the inductive sort to get the L type suffixes sorted. Now the problem how about the S type suffixes?

# Sorting the S type suffixes as well
Intuition tells us L and S are symmetric, can we run the inductive sort in reverse and sort the S type suffixes given the L type suffixes. Yes we can.

```txt
Scan the array from right to left
If the current suffix is not the full string
  If the suffix to the left that suffix is S type
    Insert that into the beginning of the corresponding bucket
```

And the proof is similar.

1. The statement we wanted to prove is after the algorithm scanned a certain position, all positions on or to the right of it has the correct elements in it.

2. In the very first step, it is true because we will find the bucket with the largest character, and that string has to be of L type, which we assumed to be correct.

3. Now suppose the first `k-1` steps are good, considering the `k` step, and suppose we encounter a wrong suffix in the position.

4. Let the wrong suffix be \\( c \alpha \\), and the correct one is \\(c \beta \\). These are both S suffixes because the algorithm only move S suffixes within its own bucket.

5. Beside the obviously correct '$', all S suffixes have length at least 3, so \\( \alpha \\) and \\( \beta \\) cannot be empty string.

6. We have \\( \alpha \\) < \\( \beta \\) and also \\(c \alpha\\) < \\(\alpha\\), \\(c \beta\\) < \\(\beta\\) 

7. Since the correct suffix is \\(c \beta\\), we must have seen \\( \beta \\) earlier, so we must have inserted \\(c \beta\\) already. 

8. But we see \\( c \alpha \\) before we see \\(c \beta\\), so it must be the case that we have also seen \\( \alpha \\) earlier then \\( \beta \\).

9. So both \\( \alpha \\) and \\( \beta \\) were seen, but in the wrong order. That is a contradiction because we assumed that order we seen earlier was correct.

See, this is completely symmetric.

# Sorting the LMS suffixes

The problem of producing the suffix array is now reduced to sorting the LMS suffixes. We have a good news, because a LMS suffix must have a L right next to a S, at most half of the suffixes can be LMS.

Suppose we can recursively solve the problem of sorting the LMS suffixes, then we are done. Because \\( T(n) = T(n/2) + O(n) \implies T(n) = O(n) \\).

The only problem is that sorting the LMS suffixes is not the same problem as creating a suffix array, so it is unclear how we would recursively solve a different problem. Unless we turn it into one. Looking at our banana again:

```txt
banana$
LSLSLLS
```

The LMS suffixes are 
```txt
anana$
ana$
$
```

Wouldn't it be cool if we could rename the substrings into an alphabet this way:

```txt
pq$
q$
$
```

And therefore, what we are really solving is the suffix array problem for the string `pq$`.

To do this trick, we need to know the substring `an` should map to `p` and the substring `ana` should map to `q`. But how exactly do we find this renaming scheme?

# Renaming the strings
The strings to be renamed has a very special property. It starts with a LMS character, and ends before the next. We will call these strings LMS substrings. Finding the set of all LMS substrings is not difficult, but what should we rename them to?

The naive approach is to map each LMS substring to a different name, but it won't work since we basically forgot the original problem and solved the trivial problem of finding the suffix array of `123456$` with all distinct names. That won't solve our problem.

This tell us the renaming must respect the original problem. In particular, it must be mapped to alphabets where it respect the comparison of the LMS substrings.

If we could sort the LMS substrings and do the obvious renaming, then we can build the new string for the recursive call!

# Sorting the LMS substrings
Now we are back to square 1. If we were to sort the LMS substrings, we are still going to have \\( O(n^2 \log n) \\) time, we could have at most \\( \frac{n}{2} \\) LMS substrings!

Finally we are getting to the crux of the SA-IS paper. The author used the induction sort again for this purpose!

To start with, we put all the LMS suffixes into their corresponding S buckets.
Sort the L suffixes
Sort the S suffixes

And the claim is that this will sort the LMS substrings. This is just amazing. How exactly does that work?

# LMS Prefix
Instead of thinking about sorting the suffixes, we can also think of the problem as sorting the LMS prefixes. Earlier, when we were thinking about sorting the suffix array. The suffix array content is an integer that give us the index of where the suffix start, and the suffix always ends at the end of the string. Instead of that, we can think of that string ends at the next earliest LMS character. We call these strings the LMS prefixes.

Note that LMS prefixes are different from LMS substrings. LMS prefixes are required to end at the LMS character, but it can start anywhere on or after the previous LMS character. So we can still have L type LMS prefix or S type LMS prefix depending on the first character. 

On the other hand, LMS Substring must start from the LMS character, and must stop exactly before the next LMS character.

The set of LMS suffixes are all the type S LMS prefixes of length 1.

Now we wanted to prove, in the L pass, we will sort all the L type LMS prefixes. Bring back our good old proof:

1. The statement we wanted to prove is after the algorithm scanned a certain position, all **L** positions on or to the left of it has the correct elements in it.

2. In the very first step, it is true because we will find '$', and '$' is obviously in the right position.

3. Now suppose the first `k-1` steps are good, considering the `k` step, and suppose we encounter a wrong suffix in the position.

4. Let the wrong prefix be \\( c \beta \\), and the correct one is \\(c \alpha\\). These are both L prefixes because the algorithm only move L prefixes within its own bucket.

5. The L type LMS prefix must have at least length 2, so \\( \alpha \\) and \\( \beta \\) cannot be empty string.

6. We have \\( \alpha \\) < \\( \beta \\) and also \\(c \alpha\\) > \\(\alpha\\), \\(c \beta\\) > \\(\beta\\) 

7. Since the correct prefix is \\(c \alpha\\), we must have seen \\( \alpha \\) earlier, so we must have inserted \\(c \alpha\\) already. 

8. But we see \\( c \beta \\) before we see \\(c \alpha\\), so it must be the case that we have also seen \\( \beta \\) earlier then \\( \alpha \\).

9. So both \\( \alpha \\) and \\( \beta \\) were seen, but in the wrong order. 

If both \\( \alpha \\) and \\( \beta \\) are L type prefixes, this contradicts with the inductive hypothesis.
If exactly one of \\( \alpha \\) or \\( \beta \\) is S type prefix, then we argue just by moving the L type prefix cannot mess up the relative ordering between S type and L type.
If both \\( \alpha \\) and \\( \beta \\) are S type prefixes, then being a S type prefix that is a suffix, it can only be length 1. (Otherwise it wouldn't be LMS prefix anymore).

In the backward pass, 

1. The statement we wanted to prove is after the algorithm scanned a certain position, all positions on or to the right of it has the correct elements in it.

2. In the very first step, we will find a L prefix and that is correct already.

3. Now suppose the first `k-1` steps are good, considering the `k` step, and suppose we encounter a wrong suffix in the position.

4. Let the wrong prefix be \\( c \alpha \\), and the correct one is \\(c \beta\\). These are both S prefixes because the algorithm only move S prefixes within its own bucket.

5. Step 5 is tricky here.

6. We have \\( \alpha \\) < \\( \beta \\) and also \\(c \alpha\\) < \\(\alpha\\), \\(c \beta\\) < \\(\beta\\) 

7. Since the correct prefix is \\(c \beta\\), we must have seen \\( \beta \\) earlier, so we must have inserted \\(c \beta\\) already. 

8. But we see \\( c \alpha \\) before we see \\(c \beta\\), so it must be the case that we have also seen \\( \alpha \\) earlier then \\( \beta \\).

9. So both \\( \alpha \\) and \\( \beta \\) were seen, but in the wrong order. That is a contradiction because we assumed that order we seen earlier was correct.

Step 5 is getting in our way, in general, we cannot claim \\( \alpha \\) or \\( \beta \\) are not empty string, and that is not great for the proof, so I relaxed the goal of the proof. I would like to show that all length > 1 LMS prefixes are sorted. Now step 5 is obvious, if it is length 1 so that \\( \alpha \\) or \\( \beta \\) are empty strings, we don't care that case.

# LMS Substrings again
Now we have the LMS prefixes sorted except the length 1 ones. It turns out to be okay because the LMS substrings are those LMS prefix with one less character (in particular, the LMS substring do not contain the last LMS character). So we can regard this as a sorted list of LMS substrings. A linear scan of this list can be used to determine the name of the substrings.

This concludes the theory of the SA-IS algorithm.