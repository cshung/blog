---
title: "A Story of One Byte"
date: 2026-05-15T00:00:00-08:00
draft: false
---

I spent a week deleting one byte.

It started with a debugger test failure. The test compares stack frames, and one frame was missing. No crash, no corruption — just one frame quietly absent from the call stack. Everything else looked fine.

The `callstack` command returns a managed stack trace, and that trace was one frame short. Stepping in, I found it was returning a cached value — an incorrect one. So the real question became: when was that cache populated with the wrong data?

Two things made this tractable: the bug was deterministic, and I had compilable source. Those two together open a door — a technique I reach for whenever I need to find the origin of a bad object.

The idea: give every stack trace object a birthday. Add a field initialized from a global counter:

```cpp
static int stack_trace::s_counter = 0;

class stack_trace {
public:
    stack_trace() {
        this->m_birthday = ++s_counter;
    }
private:
    int m_birthday;
    static int s_counter;
};
```

*(The actual code is closed source — this is illustrative, but the method is real.)*

First run: observe the birthday of the incorrect object. Second run: set a conditional breakpoint in the constructor, triggering only when the counter reaches that birthday. Now you're standing at the exact moment the wrong value is born.

The constructor fired during the **Exception Caught Debug Event**.

## Why catching an exception walks the stack

I needed to understand why catching an exception would trigger a stack walk at all.

When a debuggee catches an exception, it signals the debugger through an SEH exception — in .NET Native, this is [SendRawEvent](https://github.com/dotnet/corert/blob/81c8ab7520d425f45c69c12477c69fa920cf6982/src/Native/Runtime/DebugEventSource.cpp#L77). The debugger receives the event, and while it processes, the debuggee is frozen.

The debugger needs to place a hidden breakpoint at the catch handler so execution can resume there when the user single-steps. To find the catch handler, it must walk the stack. That's where my missing frame was being lost.

But knowing *why* a stack walk happens didn't explain why the walk was *wrong*. I had to follow the data down.

## One layer at a time

The managed stack comes through a chain of abstractions: `ICorDebugStackWalk` calls into `ICorDebugVirtualUnwinder`, which uses `IDiaStackWalker`, which reads exception unwind data from the binary's `.pdata` section.

I checked each layer. Every one was faithfully returning what the layer below gave it. The error wasn't in any of the interfaces — it was at the bottom, in how the unwind data was being interpreted.

## Virtual unwinding

Stack unwinding reverses the effect of function calls — restoring register state to what it was before the call. *Real* unwinding actually changes process state; exception handling uses it to jump execution back to a catch frame. *Virtual* unwinding just simulates that process, reading saved register values off the stack to reconstruct what the state *would* be. This is what debuggers do.

The key data structure is a `RegisterDisplay` — a struct of pointers that initially reference the current thread context, then shift to point at saved values on the stack as the virtual unwind progresses frame by frame.

Why are register values on the stack? Because the ABI requires it. Callee-save registers must be preserved across function calls, and compilers do this by pushing them onto the stack in the prologue.

## The dead end that taught me something

Looking at the virtual unwind implementation, one instruction's handling seemed wrong:

```asm
stp   x1, x2, [sp, #-16]!
```

`stp` is Store Pair — writes two registers to memory. But the unwinder was *modifying the stack pointer* while processing this instruction, which felt suspicious.

Turns out the unwinder was correct. The `!` at the end is ARM64 pre-index addressing mode: `sp` is decremented by 16 *before* the store happens. The instruction itself changes `sp`, so the unwinder is right to add 16 back.

Dead end. But now I understood exactly how each instruction's unwind was supposed to behave. Keep going.

## The frame that had no return address

With no clear suspect in the unwind logic, I tried a different approach: follow the frame pointer chain. ARM64 doesn't mandate frame pointers, but they're common enough. The chain led me to `RhpThrowHwEx` — and that's where the unwind went wrong.

`RhpThrowHwEx` is an assembly routine with an unusual property. When a hardware exception fires — division by zero, access violation — a Vectored Exception Handler intercepts it, sets `PC` to `RhpThrowHwEx`, and returns `EXCEPTION_CONTINUE_EXECUTION`. Execution jumps there without a conventional call. There's no return address on the stack for a stack walker to follow.

To handle this, the prologue uses a macro called `PROLOG_PUSH_MACHINE_FRAME`. It's a Windows construct that generates **no instructions** — only unwind opcodes. It tells the stack walker: "the return address and stack pointer are saved at known offsets in this frame." That's how the walker recovers `PC` and `SP` for frames that were entered without a call.

Now look at the prologue *before* my fix:

```asm
PROLOG_STACK_ALLOC 0x50
PROLOG_PUSH_MACHINE_FRAME
```

`PROLOG_STACK_ALLOC 0x50` does two things: it emits an instruction (`sub sp, sp, #0x50`) and an unwind opcode (`SP += 0x50`). But `PROLOG_PUSH_MACHINE_FRAME` already restores `SP` to its correct value as part of its own unwind. The `PROLOG_STACK_ALLOC` opcode then adds *another* `0x50` on top of the already-correct `SP` — pushing it too high by `0x50` bytes. That's enough to skip past one frame on the stack. One frame disappears.

## One byte

The fix: replace `PROLOG_STACK_ALLOC 0x50` with `PROLOG_NOP sub sp, sp, #0x50`. The machine instruction is identical — the stack still gets allocated. But `PROLOG_NOP` doesn't emit an unwind opcode. The redundant `SP += 0x50` is gone. The stack pointer unwinds to the right place. The missing frame comes back.

A week of chasing through debug events, stack walkers, register displays, unwind opcodes, and ARM64 addressing modes. The fix was deleting one unwind opcode — one byte.

This entire investigation depended on one luxury: determinism. The bug reproduced the same way every time, which made the birthday trick possible — run once to observe, run again to catch. Without determinism, none of that works. You can't set a conditional breakpoint for a birthday that changes between runs.

---

*Originally written in Cantonese for a forum in April 2018. Translated and adapted here for preservation.*

Looking back, this was comfortable ground. Deterministic bugs are hard, but they play fair — you can always rewind and try again. The real growth came later, when I started working on the garbage collector. Nothing was reproducible. Heap corruption surfaced in different places every run. The birthday trick was useless. Every technique I'd relied on had to be replaced with something that could cope with randomness. That transition — from deterministic debugging to non-deterministic heap corruption — was one of the hardest and most important leaps in my career. The [memory corruption series](/posts/memory-corruption-1) is about that world.
