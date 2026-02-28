---
title: "Here's a LibC"
date: 2023-09-02T17:39:02Z
draft: false
---

# Here's a LibC

Today is September 2, a long weekend in the U.S. With some time to spare, I want to see if I can tackle the third binary exploitation challenge.

Recently, I've been busy, so I haven't worked on this for two weeks. Now, I need to recap. I actually wanted to summarize and write a detailed explanation.

Starting with the problem, we have a `vuln`, a `libc.so.6`, a Makefile, and a netcat endpoint, and the task is to pwn.

Pwn means to own the computer, usually referring to gaining access to a shell.

First, we need to understand what the `vuln` does, so let's check it out using Ghidra.

The program primarily uses the unsafe `scanf` to take input, modifies it slightly, and then outputs it in a loop. Clearly, it exploits the danger of `scanf` to overflow the stack.

After a few attempts, I found that the stack return address can be controlled using the 136th character of the input. So, I started trying to place instructions after the return address and set the return address for testing.

Here’s a related question: how to send arbitrary bytes to stdin? The return address is 64 bits, and since it's little-endian encoding, we need to send 8 bytes in reverse order.

This `scanf` uses the format string `"%[^\n]"`, which is beneficial because it only cares about `\n` and ignores other characters. This allows us to send arbitrary non-0x10 bytes using `scanf`.

However, some bytes in the return address are clearly not characters that can be typed from a keyboard, so I can only generate them using the program. Since I'm just writing to it, I can create a file and pipe it in.

After many attempts, it crashed; I failed! Debugging revealed that the stack is marked as non-executable. The `-fno-stack-protector` in the Makefile and this stack protection are two different things!

The stack protector is a special value placed in the stack by the compiler. This value is random but will be checked before returning. If you overwrite this value with a stack overflow, the check will likely fail. The `-fno-stack-protector` flag disables these checks to facilitate stack overflow exploits.

However, a non-executable stack is another matter! When the OS allocates memory for the stack, it sets a restriction in the memory map to prevent execution. Therefore, even if you successfully overwrite the return address with a stack overflow, the OS will not allow you to execute code on the stack.

This is reasonable; allowing execution on the stack would be unusual. However, some older code does execute stack code, so this feature can be disabled, but this binary hasn’t done that.

You can check this flag using `checksec.sh`. You can download it [here].

If you really want to execute code on the stack, you can use `execstack` to turn it on. But even if you manage to make the stack executable locally, it won't help remotely, so I need to think of other methods.

To confirm that the stack is indeed non-executable, it's easy. You can find the permissions of a page using `cat /proc/pid/map`.

After researching how to bypass the NX restriction online, I found a technique called return-to-libc, which uses the stack return address to call a function in libc since libc is not on the stack and can be called.

The standard approach would be to call `system`, which would already solve the pwn.

However, following the return-to-libc tutorial closely, I first tried calling `exit`.

The advantage of `exit` is that it has no parameters (it does, but passing them randomly works the same; no one cares about the exit code).

I debugged the `exit` function, and it was always at different locations—oh, ASLR!

ASLR stands for Address Space Layout Randomization. Honestly, I'm not very clear about this feature, but it seems that every time a process is loaded, the location of libc changes.

At this point, I noticed there's an option in the Makefile called `-no-pie`. What does this flag mean?

PIE stands for Position Independent Executable, a compiler flag that instructs the compiler to generate code that can operate correctly when loaded at any position, i.e., it doesn’t assume any fixed address.

PIE is a prerequisite for ASLR. If the code is not PIE, it cannot be loaded at any address, meaning ASLR cannot be applied.

Since ASLR is not applicable, I looked for a way to disable it. I found that this command would work:

```text
setarch `uname -m` -R /bin/bash
```

This command opens a new bash shell, and all processes under it will not have ASLR enabled.

Sure enough, the address of `exit` does not change. Great, I can write an exploit that can exit the program.

The next step is to call `system`, which requires an argument. Many online resources mislead you, suggesting the argument should be on the stack; this is only true for 32-bit systems. For x64, the first argument is in `rdi`.

This seems problematic. We can manipulate the stack, but how do we get something into `rdi`?

After some research, I found the idea of gadgets, specifically looking for these two instructions:

```text
pop rdi
ret
```

If they exist, these two instructions will achieve the desired effect.

The plan is to place these instructions on the stack, then put the desired `rdi` value on the stack, followed by the address of `system`. The execution will return to these two instructions, popping the desired value into `rdi`, and then returning to `system`, allowing the call.

This technique is called Return-Oriented Programming (ROP), using returns to jump to the desired actions.

Where can we find these two instructions? There’s a tool called ROPgadget that can locate all gadgets in a binary.

https://github.com/JonathanSalwan/ROPgadget

Looking at the Ghidra disassembly, I couldn’t find `pop rdi`, but `ROPgadget` found it, which is quite tricky in this case.

The instruction is actually:

```text
        00400912 41 5f           POP        R15
        00400914 c3              RET
```

But if it points to 0x400913, it becomes:

```text
        00400913 5f              POP        RDI
        00400914 c3              RET
```

Great, I can call `system`.

Knowing how to call `system`, what argument should I provide? To keep it simple, I’ll directly launch a shell, which can do everything. Launching a shell has a convenient input: a hardcoded string in libc, `/bin/sh`.

Using the `strings` program makes it easy to find this string, but I don’t know the address. I’ll write a `test.c` to use `system` to run the program, then break on `system` in `gdb` to see the value of `rdi` when it calls `execve`, revealing the address of the string.

With ASLR disabled, the address of this string is constant every time. So, I can hardcode it onto the stack.

However, there's a problem: no matter how I call it, it crashes. The issue is at this instruction: SEGSEGV ⋯

```text
0x7ffff7a31396  movaps %xmm0,0x40(%rsp)
```

I’m not sure what it does, but `rsp + 0x40` is accessible in memory. After a while, I remembered that the `movaps` instruction requires inputs to be 16 bytes aligned, so I need to adjust the stack to be 16 bytes aligned first.

How do I do this? Just pop one more word; I already have a `pop` gadget available.

After completing this part, the only thing left is to pipe the commands to run the shell, but the final process crashes. This happens because when `system` returns, the return address is garbage, so it’s unclear what will happen. To avoid crashing, I just need to return to `exit`.

It works locally, but not remotely. What’s the issue?

One aspect worth questioning is ASLR. Since the binary used `-no-pie`, why do I still need to disable ASLR myself?

This is because ASLR is a kernel flag that defaults to being turned on. Even if a process uses `-no-pie`, the other shared libraries it uses will still have random load addresses, so you cannot assume ASLR is turned off.

This means my exploit works, but only with ASLR disabled. I need to create an exploit that works regardless of whether ASLR is on or off.

Challenge accepted! I googled how to defeat ASLR and found out about using plt/got.

No matter how libc randomizes, the program must call a libc function. This means the program will always interact with libc, so finding this point is crucial, specifically the plt/got.

When the program calls `puts`, it will invoke a function called `puts@plt`, which performs a redirect jump to read memory, and the address of that memory cell is fixed. Thus, if we know the location of `puts`, we can determine the address of the `puts` function. Fortunately, this location can be used as a string for printing, and it will have a `\0` at the end, perfect!

However, we don’t know the address of libc, so how do we call `puts`? We call `puts@plt` :)

The plt stands for Procedure Linkage Table, and the got is the Global Offset Table for addresses. This kind of redirect jump linking is quite common.

At this point, it’s clear that if `vuln` didn’t use `-no-pie`, we wouldn’t even have access to the `plt/got`, which is why `-no-pie` is important.

Having the location of `puts` is sufficient. It turns out all other addresses we depended on in libc have a fixed relative offset from `puts`, which resolves the ASLR issue.

Here, I glossed over an important point: the exploit needs to read `puts`'s output and send input, but the input/output is in binary bytes, which cannot use shell or pipe. Python is too cumbersome for this, so I’ll use C. I’ll open two pipes, use `dup` to link stdin and stdout, and then use `read` and `write` for reading and writing.

Finally, the netcat endpoint and local program differ slightly. Sometimes, netcat fails to read all the bytes, so I used a loop to read enough bytes based on the pattern to resolve this issue.