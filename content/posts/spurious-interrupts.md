---
title: "Spurious Interrupts"
date: 2021-10-23T16:32:21-07:00
draft: false
---
# Spurious interrupt

In this post, we are going to talk about the spurious interrupt that we see in the console when running Xv6 on v86. I think it is a reflection of a deep problem in Xv6. To get started, let first explain what is a spurious interrupt.

# The hardware side
The best way to think of spurious interrupts is to think like the programmable interrupt controller (PIC) between the devices (e.g. the clock) and the processor. On one hand, the device can raise or lower an interrupt request, on the other hand, the processor can postpone interrupt processing. Let's consider this initial state and possible sequences of events.

Initial state: The device is silent and is not raising a request, and the processor is accepting interrupts.

## Case 1:
1) The device raises an interrupt request to the PIC
2) The PIC raised an interrupt request to the CPU
3) The CPU acknowledges the interrupt request
4) The CPU tells the PIC the interrupt processing is done.
5) The CPU is processing the interrupt request

This is the ideal case, we love it. 

## Case 2:
1) The device raises an interrupt request to the PIC
2) The PIC raised an interrupt request to the CPU
3) The CPU acknowledges the interrupt request
4) The CPU is processing the interrupt request
5) The CPU tells the PIC the interrupt processing is done.

This case looks more normal than case 1, but it is actually less ideal. The issue is that the PIC will not raise another interrupt while it thinks the CPU is still processing it. So the interrupt requests that arrived during interrupt processing are postponed. 

The reason why one would want case 2 is that the code is easier to write - in case 1, all code must cater for the case where it could be interrupted (i.e. they must be reentrant). In this case, we can safely assume interrupt cannot happen within interrupt processing.

## Case 3:
1) The CPU pause interrupt processing
2) The device raises an interrupt request to the PIC
3) The PIC raised an interrupt request to the CPU
4) The CPU resume interrupt processing
5) The CPU acknowledges the interrupt request
6) The CPU tells the PIC the interrupt processing is done.
7) The CPU is processing the interrupt request

In this case, the CPU wanted to make interrupt processing as responsive as possible, but it must do some work that cannot be interrupted, so it temporarily pauses interrupt processing. Despite the latency issue, this worked just fine.

## Case 4:
1) The CPU pause interrupt processing
2) The device raises an interrupt request to the PIC
3) The PIC raised an interrupt request to the CPU
4) The device lower an interrupt request to the PIC
5) The CPU resume interrupt processing
6) The CPU acknowledges the interrupt request
7) The CPU tells the PIC the interrupt processing is done.
8) The CPU is processing, but what to process?

This is the spurious interrupt case, if the device no longer needs interrupt processing, then what should the CPU in this case? In this case, the PIC will tell the CPU this interrupt is spurious, and the CPU will just ignore it.

To "fix" the spurious interrupt issue, we can simply remove the cprintf in the trap function, this will eliminate the symptom (that we see those print out), and in general, the processing of this case is correct.

Before we finish up this session, let me also note that the most prevalent device is the programmable interval timer, which is triggering a periodic interrupt request at a rate much faster than the keyboard. The timer is basically a count down per tick, and the ticks are counted by `v86.microtick`, which asks for the real time.

Because I am running v86 is debug mode without optimization, it is quite possible that the timer is ticking way too fast with respect to instruction execution, and the experiment to simply divide the result of `v86.microtick` by 10 seems to reduce the probability of the spurious interrupt issue significantly. Reduce it further, however, significantly impact latency. As we are switching processes much less frequently, the shell no longer responds to keyboard events fast enough and it appears to lag.

The timing may not be the root cause of the issue. I tried to speed up the clock rate on QEMU (by altering `timer.c`), and even when I make the clock tick real fast on QEMU side, I still cannot observe any spurious interrupt on the QEMU side.

# The software side
The last section we studied the pure hardware level perspective of the problem - certainly hitting a raising and lowering while interrupt processing is masked is possible. But why does the processor mask the interrupt processing for such an extended duration of time so that this becomes so prevalent?

Now we switch our attention to the software side of things. CPU interrupt processing is paused using the `cli` instruction and resumed using the `sti` instruction. The most frequent uses of these instructions are in `pushcli` and the `popcli` functions, which is used by the `spinlock` (which is in turn used prevalently by `scheduler` function). Notice that interrupt processing is masked during the whole duration when the first acquire happened (we haven't even got the lock yet) and until the last release is done.

Suppose we have a contention situation, multiple threads are trying to acquire, they all masked the interrupt processing, the system will stay masked interrupt processing until all threads are done with the lock, I think that is why we have such an extended period of time when interrupt processing are masked off. I had a brief experiment to try to eliminate the `pushcli` and `popcli` pair in the spinlock, now with interrupt not masked, there could be a recursive situation where we are in the piece of code where we have already acquired the lock but got interrupted, and the service routine would like to take the lock again. What we really needed here is a recursive spinlock. I attempted to implement the recursive spinlock and lift the constraint that we cannot process any interrupt between `acquire` and `release`. The recursion is simple, but it appears that the rest of the system depends on we do not process interrupt between `acquire` and `release` (for example, `sched` expect `ncli` to be 1). That won't be an easy route.

Another thing worth noticing is that the trap function is calling the `lapiceoi` function which is currently a no-op because I switched to use pic instead of lapic. (Note: `eoi` stands for end of interrupt). Not doing anything after interrupt processing is completed is okay because in pic we set the `autoeoi` flag on, which means we are running in case 1, the CPU automatically declares the interrupt processing is completed. But I am really supicious that all interrupt processing code is reentrant, because in lapic we go through the trouble to make sure we run case 2. I tried to switch to manual eoi and it doesn't change the fact that we still have spurious interrupt. I don't even see a reduction in the probability of observing it.

# Looking back ...
Thinking a bit more, the fact that the `acquire` and `release` pair also masked interrupt makes it easy to argue about reentrancy correctness. Modification of any global state is safe from the other threads as well as the potential interrupt that could happen in between. Perhaps it is a wrong desire to try to lift that constraint.

Since none of my fixes completely eliminate the spurious interrupts, something else must be wrong, but I already covered too much, so I decided to conclude this post for now.