---
title: "Git Merge Squash"
date: 2021-04-21T12:57:39-07:00
draft: false
---

# Git Merge Squash
This is a short article documenting my process of reusing someone else's change. It is not particularly easy and therefore I would like a document to remember how I did it.

## The problem?
Imagine my partner and I were working on the same repository. I am working on a feature and he is working on a fix that I need. However, for whatever reasons, my partner couldn't merge the fix in, but I would really like to apply his change in my code base so that my scenario work (at least locally), how should I do it?

1. If my partner's fix is simply a single commit. That would be easy, I can just `cherry-pick` his commit and we are done.
2. If my partner's fix is contiguous sequence of commits. I could use `git rebase -i` to squash the commits so that it becomes one, and then I can `cherry-pick`. 
3. If my partner's fix is interleaved with merge, that can be tricky. 

This document will talk about how do we handle the last case.

## Merge or Rebase
To get started, we create a feature branch, we write code. But once in a while we wanted to make sure the feature branch stay in sync with the shared upstream. We could either merge or rebase. In both case, we fetch the commit from upstream first:

`git fetch upstream`

### Merge
We can issue the `git merge upstream/main` command. This will create a merge commit that make sure the local environment looks like the changes are made on top of `upstream/main`.

### Rebase
Alternatively, we can use `git rebase upstream/main`. This will make sure all the local commits are made on top of the ones coming from `upstream/main`.

The difference of these are best illustrated by an example. Suppose the feature branch started when we have 2 commits:

```txt
v2 <- fork/main
v1
```

Now we are working on the feature, and created a few commits

```txt
f2 <- fork/feature
f1
v2 <- fork/main
v1  
```

At this time, the upstream moved forward:

```txt
v4 <- upstream/main
v3
v2 <- fork/main
v1  
```

and we wanted to stay in sync, we could have done a merge like this

```txt
f3 <- fork/feature
f2
f1
v2 <- fork/main
v1
```

The `f3` is a merge commit. It contains all changes from `v3`, `v4`, and perhaps potential changes required to due with conflicts. `f3` will have two parents, pointing to both `v4` and `f2`.

We could also do a rebase:

```txt
f2
f1
v4
v3
v2
v1
```

That explains the name of rebase, because we are changing the base of `fork/feature`. In case we have conflicts, they will be inside `f1` or `f2`, depending on which change caused the conflict and how would we change things.

My personal preference is rebase. This make reusing commits very easy, the feature commits are always in contigous order so we can easily squash and cherry-pick this changes to anywhere I want.

But I cannot make my partner use `git rebase`. What if he used merge? Imagine he piled on another `f4` on top of `f3`. Now we have a problem. I want to precisely take `f1`, `f2` and `f4`. 

I could have cherry-picked the 3 of them, but when the list of commits get long, it will be tedious and error prone. Fortunately, git has a solution for everything.

## Squashing a branch
In GitHub, there is a squash and merge button that can turn a PR into a single commit and merge into the upstream. This is something we can do locally as follow:

```txt
git checkout main
git checkout -b temp1
git checkout feature
git checkout -b temp2
git merge temp1
git checkout temp1
git merge --squash temp2
git commit -m "Feature!"
```

Now we can use the commit, `cherry-pick` to where we needed it.

This needs some explanation. We assume `main` is a branch that is more recent then when `feature` is developed upon. To ensure we are not changing either `main` or `feature`, we created new branches `temp1` and `temp2` for them.

The fifth command makes sure `temp2` can be interpreted as a change on top of `temp1`. If we knew when exactly is the last merge for `feature`, we could have skip this step. In practice, it is difficult to figure that out, and it is much easier to just give the branch a new base.

The magic command is the seventh command. On the temp1 branch, we wanted to create a single commit that represents the change to `temp1` overall from `temp2`. The merge command itself won't commit, it will just put it in the staging area, and therefore we need the last command to create a commit for it.

Just remember to remove the temp branches when they are done.
```txt
git branch -D temp1
git branch -D temp2
```