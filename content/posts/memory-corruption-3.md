---
title: "Memory Corruption (3)"
date: 2022-12-17T12:31:46-08:00
draft: false
---

# Another memory corruption bug
In this post, I will talk about another memory corruption bug we found and fix together with Mukund. Check out my [previous post](/posts/memory-corruption-2) for more context and examples. 

# Symptom
As reported in [this issue](https://github.com/dotnet/runtime/issues/76929), we hit an access violation while we are trying to compare objects for equality when running a test case that is doing some concurrent dictionary of buffers allocated on the POH.

# A first look
When the program crashes at the object equality check. Here is the point of failure:

```
...
  146 00007ffc`25e47c3e 4889542438      mov     qword ptr [rsp+38h],rdx
  146 00007ffc`25e47c43 488bca          mov     rcx,rdx
  146 00007ffc`25e47c46 498bd0          mov     rdx,r8
  146 00007ffc`25e47c49 488b442438      mov     rax,qword ptr [rsp+38h]
  146 00007ffc`25e47c4e 488b00          mov     rax,qword ptr [rax]
  146 00007ffc`25e47c51 488b4040        mov     rax,qword ptr [rax+40h]
  146 00007ffc`25e47c55 ff5010          call    qword ptr [rax+10h]       <<< rax is abababababababab, obviously bad
...
```

This is a typical virtual method call sequence. Initially, the stack slot `rsp+38h` contains a pointer to an object, then we load it into `rax`, then we load the method table to `rax`, and then we load the `40h` element into rax, and then we wanted to call the `10h` element in that table.

`rax` having a weird value basically means the source of `rax` is wrong, and therefore we inspect the object stored in `rdx`, and in fact, it is wrong. It is a free object, no wonder we cannot find its method table.

Something being used is freed is obviously problem. At some point, the GC must have freed it. Let's take a look as the stress log as usual.

# Marked and freed
According to the stress log, the object is marked and then freed in background GC. This is puzzling, because marked objects should not be freed. Could it be the case that the object was marked in earlier GC but not marked in the later GC, so it got freed?

Then I discovered another puzzling fact, somehow, we missed all the start background GC messages. Without them, we have no idea. What happened?

# A detour - stress log reliability
The missing of the background GC start message make me question about the reliability of the stress log. Do we always log all messages? Actually, the stress log analyzer is telling me it is not.

At the end of the stress log analyzer run, we have this message:

```
Used file size:  1.895 GB, still available: 30.105 GB, 10 threads total, 6 overwrote earlier messages
Number of messages examined: 38.964634 million, printed: 0
```

As a first glance, it looks like I have plenty of space left, so we should be fine, but the truth is that 6 *threads* already ran out of space and reused existing space to write log messages.

Turn out in the stress log mechanism, we have a per thread size limit. As the GC threads are logging way more information than the other managed threads, the GC thread reach the limit sooner, and therefore it wrapped around, despite we had a lot of space left.

As a remedy, I simply hack away the per-thread size limit, and now I can see all the messages as long as I have enough memory to use.

> Debugging Tips: Know your tools. The answer was right in front of me, I just didn't understand what does that mean.

# No dice - still mark and free
The modified stress log tells us that we are mark and free within the same background GC. That is even more puzzling. At this point, we are starting to wonder if it is missed message. If, for whatever reasons, that the GC reset the mark bit without writing a log. This could happen. As such, we read through the code such that everywhere touches `mark_array` are either.

- Logged with some messages, or
- Have a `FATAL_GC_ERROR` such that it will crash whenever that is run.

The second mode is really just a quick hack to avoid too extensive instrumentation. If the code path is not being executed in the repro, then there is no point spending time instrumenting it.

To make sure it is not just logged, but searchable, we also modified the stress log analyzer and made sure if we did log, the search by value will work even when the log message is outputting range.

# Building a mark array shadow
The modified stress log show nothing extra. Now it is puzzling even more. Now we are starting to question ourselves. Am I missing anything? Maybe we missed somewhere modifying the `mark_array`?

To check if I missed anything, I build a mark array shadow. The idea is that whenever I hit a place modifying the mark array, I update both the array and the shadow. If the shadow and the mark array stay consistent, then I didn't miss anything. Otherwise I do.

Checking the mark array shadow is the same as the mark array is quite a problem because they are big. For my purpose, I only check when I am freeing a POH object, knowing at that point, the POH object is freed with a mark, then it must be the case that mark is different.

To my surprise, even using the mark array shadow, I don't find inconsistency (Actually it occassionally does, but I wasn't noticing when I was debugging). I concluded that our logging is indeed complete.

> Debugging Tips: Using a shadow can be a effective way to make sure all modifications are captured.

# Force the bug to appear
It is at this moment I figured an idea. The log say the object is marked, but it isn't. Can I actually check if the bit stay marked all the time in between? If I log the mark word changes together with the mark word's address, then we can look at the mark word values and see what happened. If the mark word ever change between the time when the mark bit is set and when the object is free, then we know what happened.

This is fruitful, and we have found these interesting log statements.

```
931c  38.090888200 : `GC l=10086`         Background cleared because 3: 000001EE99923C08 00000000F74CC91E 1 15555554 15555554
8d9c  36.840606300 : `GC l=10086`         Background marked: 000001EE99923DE8 00000000F74CC91E 40000000 45555555 45555555
9564  36.840606300 : `GC l=10086`         Background marked: 000001EE99923DC8 00000000F74CC91E 10000000 15555555 15555555
```

*You won't find these logging statement in the code, these are my new instrumentation.*

The marked and freed object is `000001EE99923DE8`, the associated mark array address is `00000000F74CC91E` and the bit mask associated with the mark bit is `40000000`. It is obvious that the mark bit was set during the time we say it is background marked, but it is disappearing right away. Right 'before' that moment, we have another thread trying to set a mark bit on that same word and lead to some other value. 

The interesting observation is that they have identical time stamp! Identical time stamp indicate race, and in fact we have one. Since the setting of the mark bit is not atomic (in case of work station GC), two threads might be trying to set the mark bit of different objects on the same word at the same time, leading to the race!

> Debugging Tips: Can I force the bug to appear? That might be a good question to ask when debugging.

> Debugging Tips: Identical (or very close) time stamp is a good hint for race conditions.

# Solution
Now that we have found the issue, fixing it quickly is trivial. We changed the setting and resetting of the mark bit to an interlocked atomic operation, and it worked great. The customer scenario ran without a crash overnight, meaning the fix is solid. Having to use an interlocked atomic operation is a bit unfortunate because it impacts the whole background mark phase, we will try to see if there is a better solution to it.