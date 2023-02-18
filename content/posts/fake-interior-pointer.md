---
title: "Fake Interior Pointer"
date: 2021-09-10T09:50:21-07:00
draft: false
---

# GC reporting basics

When CoreCLR performs a garbage collection, the GC asks the VM where the roots are. The runtime will report to the GC the pointers to the pointers of the objects. This process is called GC reporting.

Suppose we have a pointer on the stack pointing to an object on the GC heap, it will look like this:

```txt
Stack:                            Heap
0x00001000: ..........            0x00F00000:
..........: ..........            ..........: ..........
..........: ..........            0X00C21212: Object Method Table
0x0000C040: 0X00C21212            ..........: ..........
..........: ..........            ..........: ..........
..........: ..........            ..........: ..........
0x0000F000: ..........            0x00100000:
```

In this case, the VM will report `0x0000C040` as the pointer to the pointer of the object to the GC.

Because the GC might move the object at `0X00C21212` during a compaction, therefore it must know the address storing that address so that it can relocate that address as follow:

```txt
Stack:                            Heap
0x00001000: ..........            0x00F00000:
..........: ..........            ..........: ..........
..........: ..........            ..........: ..........
0x0000C040: 0X00D21212            ..........: ..........
..........: ..........            0X00D21212: Object Method Table
..........: ..........            ..........: ..........
0x0000F000: ..........            0x00100000:
```

# Interior pointers

In C#, we can write the following code.

```c#
using System;

namespace Ref
{
    public class Program
    {
        private int field;

        public static void Main(string[] args)
        {
            new Program().Run();
        }

        private void Run()
        {
            int var = 0;
            Increment(ref field);
            Increment(ref var);
            Console.WriteLine(var);
            Console.WriteLine(field);
        }

        public void Increment(ref int counter)
        {
            counter++;
        }
    }
}
```

Consider the case where we are performing a GC during the first `Increment`. What would the stack look like at that point of time? In the `Increment` frame, that should be a parameter that is a pointer to `field`. It is on the heap, but it is not exactly a pointer to the beginning of the object. In that case, the runtime will report it as an interior pointer. The GC will need to figure out where the object is in order to mark it, and that is done through the [brick table](/posts/brick) mechanism.

# The Fake interior pointer!

In the second `Increment` call, we have a pointer to `var`, which is just an integer on the stack. Guess what? The runtime is going to report it as an interior pointer. By the time `Program.Increment` is jitted, is wouldn't know what would the caller gives.

In that case, I call that a fake interior pointer. The runtime reported a location containing a pointer that is not pointing to the heap at all. The GC is ready to ignore that, but I was not. So I am surprised when I saw that, and now we know that is a possibility.