---
title: "How CoreCLR GC understand objects (part 1)"
date: 2021-01-04T12:11:37-08:00
draft: false
---

In this series of posts, I am going to explain how the GC understand objects. In this part, we will focus on size. 

# Why does it matter?
The GC expects the heap to be tightly packed with objects. It scans the heap by starting at the beginning of the heap and walk the object one-by-one by this simple algorithm. This is real code excerpt from [`gc.cpp`](https://github.com/dotnet/runtime/blob/v5.0.1/src/coreclr/src/gc/`gc.cpp`) line 22972 to 23002, out of [dotnet/runtime repo, tag v5.0.1](https://github.com/dotnet/runtime/tree/v5.0.1).

```c++
uint8_t* xl = x;
while (/* some conditions ... */)
{
    /* code that does not change xl */
    xl = xl + Align (size (xl));
    /* more code that does not change xl */
}
```

At the first line, `x` points to an object. Using the `size` macro, it computes the size of the object, adjusted using the `Align` macro, we found the next object, and go on with the scan. Therefore we can visualize the heap as follow:

```
Small Address -> Large Address
|Object Space|Alignment Pad|Object Space|Alignment Pad|....
```

Therefore it is important that an object describes its size accurately.

# How size is computed?
The `size` macro is defined in `gc.cpp` line 9548.

```
#define size(i) my_get_size (header(i))
```

The `header` macro is defined in `gc.cpp` line 3794.
```
#define header(i) ((CObjectHeader*)(i))
```

And the `my_get_size` function is defined in `gc.cpp` line 9538 to 9545
```c++
inline size_t my_get_size (Object* ob)
{
    MethodTable* mT = header(ob)->GetMethodTable();

    return (mT->GetBaseSize() +
            (mT->HasComponentSize() ?
             ((size_t)((CObjectHeader*)ob)->GetNumComponents() * mT->RawGetComponentSize()) : 0));
}
```

The algorithm for computing the size is fairly straightfoward. We first note that the class `CObjectHeader` inheriting `Object` is defined in `gc.cpp` and it is just a pile of methods without any fields. This means after casting the pointer to a `CObjectHeader` using the header macro, it is passed unchanged to the `my_get_size` function.

The first thing `my_get_size` does is to get the method table. The method table is always the first pointer of the object. The method table gives us three things:

- The base size of the object
- Whether or not it has a component size, and if there is a component size
- The value of the component size.

The component size are designed for arrays. For a usual object, you don't have a component size. The base size is the size of the object. For an array, it has a base size, which is space used to describe the array itself (e.g. the length), and then we have a component size, which is the size for a single element. Note that the method table is used to get the component size, but the object itself is used to get the number of components. This allows arrays of different lengths to share the same method table.

They are a lot of variations in the type of objects that the .NET runtimes allows. For example, we can have multi-dimensional arrays. To the GC, there are only two types, either it is a fixed size object, or it is a variable sized object where its size fits this simple linear equation.

Last but not least, let's look at the `Align` function defined in `gcpriv.h` line 863 to line 869.

```c++
#define ALIGNCONST (DATA_ALIGNMENT-1)

inline
size_t Align (size_t nbytes, int alignment=ALIGNCONST)
{
    return (nbytes + alignment) & ~alignment;
}
```

where `DATA_ALIGNMENT` is defined in `gcenv.base.h` line 476.

```
#define DATA_ALIGNMENT sizeof(uintptr_t)
```

In 64 bits, the size of the `uintptr_t` would be 8. Therefore, `ALIGNCONST` is 7. By taking a bitwise negation, it effectively mask out the last 3 bits, which is the same as rounding down to last multiple of 8. However, by adding 7 first, this means it is round up to the next multiple of 8.

This really just mean objects are always point size aligned. 

This concludes why and how the GC get the size of an object.