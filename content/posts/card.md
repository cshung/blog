---
title: "Understanding the card table"
date: 2021-04-17T00:00:00-08:00
draft: false
---

# Understanding the card table

A card table is used in a generational GC to discover cross-generational pointers. Let's get started with the generation GC problem first.

## Generational GC

It is observed allocations typically divides into two classes. Either they are short-lived or they are long lived. To efficiently use the memory, we would like to be able to reclaim the space of the short lived object relatively fast without having to worry about the long lived ones. Ideally, we can have a GC that only collect the young generation. How do we do that? We do the usual mark and sweep. In the sweep phase, we can focus on the young generation only, and reclaim the memory into free lists as we see unmarked object.

But how do we make the `mark_phase` run faster? To truly determine if an object is free and subject to collection, we have to be sure that there is no reference to that object from **anywhere**. That includes the potential for long lived object to have a reference to the short lived ones. That is a sad news, because we cannot shrink the `mark_phase` to explore only young objects. 

Fortunately, there is a cure. Suppose we do young generation GC frequently, then the long lived objects don't have a chance to change too much. Suppose those changes are small relative to all the long lived objects, it make senses to just go through the changes.

## Recording changes
To record change, we need to be able to intercept writes. We do that by making sure all changes to references go through a function called the write barrier. Since this is happening for all writes, we would like the overhead to be as small as possible. To do that, we introduce the card table.

## Card table
Like the brick table, we divide the heap range into cards. Each card is represented by a single bit in the card table to indicate whether or not it *could* contain a cross generational pointer.

The logic of translating an address to a card is similar:

```c++
inline
uint8_t* gc_heap::card_address (size_t card)
{
    return  (uint8_t*) (card_size * card);
}

inline
size_t gc_heap::card_of ( uint8_t* object)
{
    return (size_t)(object) / card_size;
}
```

The key difference from the brick table is that we do not adjust for the lowest address, this is explained by the `translate_card` function.

```c++
uint32_t* translate_card_table (uint32_t* ct)
{
    return (uint32_t*)((uint8_t*)ct - card_word (gcard_of (card_table_lowest_address (ct))) * sizeof(uint32_t));
}
```

So instead of adding complexity in the address to card conversion routine, we simply move the base address of the card table itself instead so that the card table can be indexed using an unadjusted card number, that helps simplify the write barrier logic.

## Marking the cards
Now we know the card table tell use which card could contain the cross generational pointer. During `mark_phase`, we can go through the marked cards. Each marked card correspond to an address range, therefore we can find an object (through the brick table!) on that address range, and go through the pointers there. That make sure we cover all the references to the younger generation objects.

## Clearing the cards
If we do not clear the cards, they will eventually accumulate and the whole older generation could contain lower generation objects. That just defeats the purpose. Therefore we need to be aggressive with clearing the cards. A good opportunity to clear the card is once the card is marked, but there will be a problem as illustrated in this sequence of operations:

1. A new generation 0 object `New` is created,
2. An old generation 2 object `Old` is referencing `New`.
3. A generation 0 GC occurred.
4. A generation 1 GC occurred.

If we reset the card containing `Old` during the `mark_phase` of step 3. We will miss that cross generation pointer from `Old` to `New` during step 4. Turn out a cross generational pointer might be introduced in various ways. We could introduce it during promotion and demotion, we have to make sure the card table is updated in those cases as well.