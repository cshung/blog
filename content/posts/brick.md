---
title: "Understanding the brick table"
date: 2021-04-17T00:00:00-08:00
draft: false
---

# Understanding the brick table

A brick table is a data structure that helps finding an object in the heap. To begin with the discussion, it makes sense to clarify what does finding an object mean.

## What is the find object problem?
There are various situation that we are given a random pointer inside the heap, and we wanted to find the pointer to the object that is using the address. 

**TODO** describe the situation where this is useful.

## The naive solution
A GC heap is arranged as a sequence of segments. The beginning of a segment is always an object, and object are closely packed (with certain alignment constraints). Given an object, we can determine its size, and therefore advance to the next one.

Therefore, to search for an object containing a random pointer, we can simply determine the segment and walk it from the beginning, we will eventually find that object.

## Idea to speed it up
To speed it up, we would like to start the search closer to the given random pointer. Suppose we know some object's begin address that is before and closer to the random pointer, we can simply start the search from there.

To do so, we split the heap range into bricks. Each brick has size 2048/4096 for 32/64 bits. We can easily convert an address to its corresponding brick and a brick to its corresponding *base* address as follow:

```c++
inline
size_t gc_heap::brick_of (uint8_t* add)
{
    return (size_t)(add - lowest_address) / brick_size;
}

inline
uint8_t* gc_heap::brick_address (size_t brick)
{
    return lowest_address + (brick_size * brick);
}
```

*The lowest address is simply the smallest address that the GC manages.*

Now we can store something to describe a brick. In particular, we wanted to know where does an object begin within a brick. Since a brick is small, we can simply store a within brick offset there, this has to be a non-negatve number less than the size of the brick, which can comfortably store in a 16-bits number (8-bits is apparently insufficient).

## Interesting cases

We have a couple issues here:

### What if a brick doesn't contain an object? 

In that case, we would like to search the earlier brick and see if there is one. But which earlier brick? The naive answer is always the previous one, but can we do better? Sometimes, we can. Suppose we knew there is a big object that spans a few bricks, we could store that information and have the search go faster. The number of blocks to go back in the search is stored as a negative number in the brick entry. 

### What if a brick contains multiple objects?

In that case, that could be any objects in the current brick. The key, however, is that we search starting from the previous brick of the random pointer, that allow us to be sure the object pointed by the brick is always before the random pointer. 

In fact, it has to be the plug tree root, but this is out of the scope of the finding object problem.

### What if it is the first brick?
There is no previous brick for the first brick, but in that case, we can simply start with the first object in the segment.

## Who maintain the brick table entries?

Now we know what the brick table entries are good for, there must be some code that produces and maintain these entries. Turn out there are a lot of call sites to `set_brick()` so we will not examine them all. The general theme is that we set the brick when:

1. During allocation, we know where the objects are placed, and
2. During compaction, we know where the objects are moving to.

I am sure this is not an exhaustive list, there are other cases too, but that is beyond my understanding for now.