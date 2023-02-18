---
title: "How CoreCLR GC understand objects (part 2)"
date: 2021-01-04T14:14:15-08:00
draft: false
---

In addition to size described in an earlier post, it is also important for the GC to know where the pointers are in an object. This is because in the mark phase, we need to traverse the object graph. Here is a function for the mark phase in `gc.cpp` line 19415-19452.

# Where is the code that perform the traversal?

```c++
//this method assumes that *po is in the [low. high[ range
void
gc_heap::mark_object_simple (uint8_t** po THREAD_NUMBER_DCL)
{
    uint8_t* o = *po;
    /* some code that does not change o or po */
    if (gc_mark1 (o))
    {
        /* some code that does not change o or po */
        {
            go_through_object_cl (method_table(o), o, s, poo,
                {
                    uint8_t* oo = *poo;
                    /* some code to recursively mark oo */
                }
            );
        }
    }
}
```

Imagine this is run during the mark phase, if we discover an unmarked object, then we traverse into its pointers and recursively mark the objects it points to. There are some complexity with respect to managing the stack, as the objects graph gets deep, we might ran out of stack space. In this post, we will ignore that and focus into how GC discovered the pointed objects. The magic ingredient here is obviously `go_through_object_cl` in `gc.cpp` line 18602-18627.

```txt
// 1 thing to note about this macro:
// 1) you can use *parm safely but in general you don't want to use parm
// because for the collectible types it's not an address on the managed heap.
#ifndef COLLECTIBLE_CLASS
#define go_through_object_cl(mt,o,size,parm,exp)                            \
{                                                                           \
    if (header(o)->ContainsPointers())                                      \
    {                                                                       \
        go_through_object_nostart(mt,o,size,parm,exp);                      \
    }                                                                       \
}
#else //COLLECTIBLE_CLASS
#define go_through_object_cl(mt,o,size,parm,exp)                            \
{                                                                           \
    if (header(o)->Collectible())                                           \
    {                                                                       \
        uint8_t* class_obj = get_class_object (o);                             \
        uint8_t** parm = &class_obj;                                           \
        do {exp} while (false);                                             \
    }                                                                       \
    if (header(o)->ContainsPointers())                                      \
    {                                                                       \
        go_through_object_nostart(mt,o,size,parm,exp);                      \
    }                                                                       \
}
#endif //COLLECTIBLE_CLASS
```

We have two cases, either we have `COLLECTIBLE_CLASS` defined or not. In the `COLLECTIBLE_CLASS` case, we simply have an extra object (i.e. the class object) to mark that is implicitly 'pointed' by the object, otherwise, we simply delegate to `go_through_object_nostart` in gc.cpp line 18600.

```txt
#define go_through_object_nostart(mt,o,size,parm,exp) {go_through_object(mt,o,size,parm,o,ignore_start,(o + size),exp); }
```

Again, it delegates to `go_through_object`. It has an interesting option that can take an extra `start` parameter, but in this case we are not using it. Here we reach the most interesting macro that does the work.

```txt
#define go_through_object(mt,o,size,parm,start,start_useful,limit,exp)      \
{                                                                           \
    CGCDesc* map = CGCDesc::GetCGCDescFromMT((MethodTable*)(mt));           \
    CGCDescSeries* cur = map->GetHighestSeries();                           \
    ptrdiff_t cnt = (ptrdiff_t) map->GetNumSeries();                        \
                                                                            \
    if (cnt >= 0)                                                           \
    {                                                                       \
        CGCDescSeries* last = map->GetLowestSeries();                       \
        uint8_t** parm = 0;                                                 \
        do                                                                  \
        {                                                                   \
            assert (parm <= (uint8_t**)((o) + cur->GetSeriesOffset()));     \
            parm = (uint8_t**)((o) + cur->GetSeriesOffset());               \
            uint8_t** ppstop =                                              \
                (uint8_t**)((uint8_t*)parm + cur->GetSeriesSize() + (size));\
            if (!start_useful || (uint8_t*)ppstop > (start))                \
            {                                                               \
                if (start_useful && (uint8_t*)parm < (start)) parm = (uint8_t**)(start);\
                while (parm < ppstop)                                       \
                {                                                           \
                   {exp}                                                    \
                   parm++;                                                  \
                }                                                           \
            }                                                               \
            cur--;                                                          \
                                                                            \
        } while (cur >= last);                                              \
    }                                                                       \
    else                                                                    \
    {                                                                       \
        /* Handle the repeating case - array of valuetypes */               \
        uint8_t** parm = (uint8_t**)((o) + cur->startoffset);               \
        if (start_useful && start > (uint8_t*)parm)                         \
        {                                                                   \
            ptrdiff_t cs = mt->RawGetComponentSize();                         \
            parm = (uint8_t**)((uint8_t*)parm + (((start) - (uint8_t*)parm)/cs)*cs); \
        }                                                                   \
        while ((uint8_t*)parm < ((o)+(size)-plug_skew))                     \
        {                                                                   \
            for (ptrdiff_t __i = 0; __i > cnt; __i--)                         \
            {                                                               \
                HALF_SIZE_T skip =  cur->val_serie[__i].skip;               \
                HALF_SIZE_T nptrs = cur->val_serie[__i].nptrs;              \
                uint8_t** ppstop = parm + nptrs;                            \
                if (!start_useful || (uint8_t*)ppstop > (start))            \
                {                                                           \
                    if (start_useful && (uint8_t*)parm < (start)) parm = (uint8_t**)(start);      \
                    do                                                      \
                    {                                                       \
                       {exp}                                                \
                       parm++;                                              \
                    } while (parm < ppstop);                                \
                }                                                           \
                parm = (uint8_t**)((uint8_t*)ppstop + skip);                \
            }                                                               \
        }                                                                   \
    }                                                                       \
}
```

So there we go, it is the code for performing the traversal.

# How does the traversal code actually work?

We have two cases. Let's focus on the first case first. Conceptually, the MethodTable has a `CGCDesc`. It contains a list of `CGCDescSeries`. Each series contains an offset and a size, which is used to compute `parm` and `ppstop`. The while loop will be used to run `exp` while `parm` is marching towards `ppstop` one pointer at a time. When we first read the code, let's assume `start_useful` is false. This basically means the first condition is always true and the second condition is always false. Here is an interesting gem in the code.

```c++
            parm = (uint8_t**)((o) + cur->GetSeriesOffset());               \
            uint8_t** ppstop =                                              \
                (uint8_t**)((uint8_t*)parm + cur->GetSeriesSize() + (size));\
```

It is easy to understand that we want parm to point into the object by series offset. The next line is weird, the ending position is the object plus the series size plus the object size. The last plus is unconventional, why?

The last plus is actually used to merge the reference array case with the object case. In case of a reference array, we already know the size of the array, so we can simply store 0 as the series size for any array, and it will always march to the end of the object. It works because every location inside reference array after the offset is a pointer. For normal objects, all we need to do is to subtract the conventional length of the length by the size of the object, that only need to be done once when the `CCGSeries` is constructed and its worth the effort to avoid a comparison here for every array during GC.

It is a very cool optimization, in my humble opinion.

That begs the question for the second case. In case of a value type array in which the value type contains some references. We need to do something different. First of all, we use negative `GetNumSeries` value to signal the fact that the `CGDesc` describes a value type array and therefore an alternative algorithm is needed. Conceptually, a `CCGDesc` for value type array has a single `offset` and a collection of `val_serie` indexed from `0` to `cnt + 1`. (Note that `cnt` is negative). A `val_serie` contains a `skip` and a `nptrs`. The idea is that we move the pointer by `offset`. Then it should point to a contiguous region of `val_serie[0].nptrs` pointers, then we skip for `val_serie[0].skip` pointers, and then we have a contiguous region of `val_serie[-1].nptrs` pointers, then we skip for `val_serie[-1].skip` pointers, and so on until we exhaust the `val_serie` up to cnt, and then the whole process repeats until the end of the object. This description allows us to describe an object with a periodic pattern of pointers.

# How is the data stored?

Last but not least, let's look into the physical representation of these data. First of all, `GetCGCDesc::CGCDescFromMT` is just a cast in `gcdesc.h` line 166 to line 175. That is, the `CGCDesc` is actually part of the method table.

```c++
    static PTR_CGCDesc GetCGCDescFromMT (MethodTable * pMT)
    {
        // If it doesn't contain pointers, there isn't a GCDesc
        PTR_MethodTable mt(pMT);

        _ASSERTE(mt->ContainsPointersOrCollectible());

        return PTR_CGCDesc(mt);
    }
```

We can see that in the `CGCDesc::GetNumSeries()` implementation in gcdesc.h line 176 to line 179. We are accessing the region before `this`. That is, the `CCGDesc` data is actually stored before the method table pointer.

```c++
    size_t GetNumSeries ()
    {
        return *(PTR_size_t(PTR_CGCDesc(this))-1);
    }
```

The various `GetSize()` methods in the same file probably describe the layout the best. 

```c++
// Size of the entire slot map.
size_t GetSize ()
{
    ptrdiff_t numSeries = (ptrdiff_t) GetNumSeries();
    if (numSeries < 0)
    {
        return ComputeSizeRepeating(-numSeries);
    }
    else
    {
        return ComputeSize(numSeries);
    }
}
```

```c++
static size_t ComputeSize (size_t NumSeries)
{
    _ASSERTE (ptrdiff_t(NumSeries) > 0);

    return sizeof(size_t) + NumSeries*sizeof(CGCDescSeries);
}

// For value type array
static size_t ComputeSizeRepeating (size_t NumSeries)
{
    _ASSERTE (ptrdiff_t(NumSeries) > 0);

    return sizeof(size_t) + sizeof(CGCDescSeries) +
            (NumSeries-1)*sizeof(val_serie_item);
}
```

In case of objects or reference array, we have a `CGCDescSeries` array growing backwards, and that's why the size is a pointer size times the number of `CGCDescSeries` times the size of the it. In the case of value type array, we have a single `CCGSeries`, but then the `val_serie` array has `NumSeries` elements. Note that in the computation we used `NumSeries - 1` because the size of `CCGSeries` already included one element from that array.

The also explains why the `val_serie` array is indexed using negative indexes. It is because it is growing backwards.

This concludes how the GC finds pointers in an object using the description.