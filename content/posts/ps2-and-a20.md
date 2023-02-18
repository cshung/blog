---
title: "PS/2 and A20"
date: 2021-10-22T20:15:34-07:00
draft: false
---

# PS/2 and A20
If the title of the post doesn't make any sense for you, you are just too young. Well, just kidding. These are really old technologies but still impacting our journey to host Xv6 on v86. In our [last post](/posts/xv6-on-v86/), we were able to run a scenario, but it isn't perfect, it looks like all keyboard inputs are ignored and one must input the command through the UART port. I fixed it, and I will share in this post how I fixed it.

Looking backward, again, the fact that it doesn't work was an opportunity. I get to understand how interrupts actually works.

# Where is my interrupt?
We know I/O in x86 system is interrupt-driven. Therefore, we expect when we hit the keyboard, the virtual machine should send an interrupt to the operating system, and the operating system will process it. Now the key question to answer is whether or not the virtual machine raised an interrupt or not?

To confirm that, ideally, one would like to set a breakpoint on the `kbdintr` function. To do so, we look up the assembly and figured out the function has virtual address `801027c0`. Therefore, we can capture the event in the `cycle_internal` function as follow:

```js
CPU.prototype.cycle_internal = function()
{
    this.previous_ip = this.instruction_pointer;

    if (this.previous_ip == -2146424896) // -2146424896 = 0x801027c0
    {
        debugger;
    }
    ...
}
```

We noticed the `debugger` statement is never hit, so we conclude the virtual machine is not raising the interrupt, why?

# How interrupt works
To figure out how interrupt works in v86, we set a breakpoint earlier in the `trap` function at `80105850`, which is called for any interrupt. It does, but the stack reveals nothing interesting. Similarly, for the even earlier entry point `alltraps` at `80105772`. We have a good reason to believe that the interrupt is processed by not leaving a frame on the stack. So we turn our attention to the log.

By turning on `CPU_LOG_VERBOSE` at the very beginning of the `cpu.js`, I see this log statement.

```txt
22:54:03+720 [CPU ] mode=prot/32 paging=1 iopl=0 cpl=0 if=0 cs:eip=0x0008:0x80105D8A cs_off=0x00000000 flgs=0x000092 (  a s    ) ss:esp=0x0010:0x8010B548 ssize=1 in int end
```

The `int end` caught my attention. Does it mean interrupt processing ends? Searching in the code show me that is exactly the case, the code emitting the log is right at the end of `call_interrupt_vector` method. Setting a breakpoint on this function reveals how interrupt is processed. The stack gives one example on how an interrupt is processed.

```txt
CPU.call_interrupt_vector (cpu.js:1801)
CPU.pic_call_irq (cpu.js:3701)
PIC.acknowledge_irq (pic.js:157)
CPU.handle_irqs (cpu.js:3719)
t.<computed> (instructions.js:1403)
CPU.cycle_internal (cpu.js:1200)
CPU.do_many_cycles_unsafe (cpu.js:1143)
CPU.do_many_cycles (cpu.js:1129)
CPU.do_run (cpu.js:1115)
...
```

The `t.<computed>` frame is evaluating the `sti` instruction.

So the flow is as follow:
1. The CPU executed a `cli` instruction earlier, causing the interrupt to be masked.
2. Eventually, the CPU execute a `sti` instruction.
3. The CPU checks if there were an interrupt happening earlier, and if so,
4. Acknowledge the interrupt request (IRQ), and then
5. Put the CPU in the right state to execute the interrupt handler.

This begs the question - what if an interrupt happened while the interrupt is not masked? We will address this question later. For now, we focus on `CPU.handle_irq`. Does the keyboard emulation raises an IRQ, or not? 

With a bit of code reading, we notice `acknowledge_irq` checks if `this.requested_irq` is -1. If that's the case, that means there is no interrupt requested and therefore will not lead to the `pic_call_irq` code path. Therefore we would like to know how `this.requested_irq` changes.

# Programmable Interrupt Controller
It isn't hard to figure out the `check_irq` function set the `this.requested_irq` value. Setting a breakpoint on that line yield a very common stack.

```txt
PIC.check_irqs (pic.js:117)
PIC.set_irq (pic.js:305)
CPU.device_raise_irq (cpu.js:3734)
PIT.timer (pit.js:116)
CPU.run_hardware_timers (cpu.js:1270)
CPU.do_run (cpu.js:1112)
...
```

So finally we are reaching the devices! The devices can raise an interrupt request, the CPU will delegate to the `PIC` to set the `this.request_irq` value. This mirrors how the computer actually works. The PIC is the programmable interrupt controller, it takes in interrupt requests from the devices, prioritize them, and eventually interrupt the processor. The processor, on interrupt requests, will run the interrupt service routines.

Notice that `do_run` call `run_hardware_timer`. Interrupts in v86 are always raised synchronously with respect to instruction execution.

# PS/2
By searching `device_raise_irq` over the code base, it is not hard to figure out the keyboard irq is raised by the `ps2.js`. PS/2 was a keyboard and mouse port designed in 1987, a very old technologies, but it is still supported in major OS. I set a breakpoint at `kbd_irq`, I notice the method is called a few times, but no longer get called even I type on the keyboard, so something interesting is going on.

It looks like the upstream function, `raise_irq` is called whenever the keyboard is pressed. The issue is that we have a check for `next_byte_is_ready` and it got filtered away there. So why `next_byte_is_ready`?

Ideally, we would like to know when is the last time `next_byte_is_ready` is set. By setting a breakpoint on all places where `next_byte_is_ready` is set. We got around 10 hits during the boot process, and the last hit was raised by this stack:

```txt
PS2.kbd_irq (ps2.js:225)
PS2.port60_write (ps2.js:637)
IO.port_write8 (io.js:343)
t.<computed> (instructions.js:1120)
CPU.cycle_internal (cpu.js:1200)
CPU.do_many_cycles_unsafe (cpu.js:1143)
CPU.do_many_cycles (cpu.js:1129)
CPU.do_run (cpu.js:1115)
...
```

And the `this.previous_ip` is `31771`, which correspond to this line in the boot loader.

```txt
  # Physical address line A20 is tied to zero so that the first PCs 
  # with 2 MB would run software that assumed 1 MB.  Undo that.
  ...
  movb    $0xdf,%al               # 0xdf -> port 0x60
  outb    %al,$0x60
```

# A20
Finally we see [A20](https://wiki.osdev.org/A20_Line). A20 is really old (It is meant for catering 8086, designed back in 1978). When the processor is boot, it is suppose to be capable to run 8086 applications. In 8086 days, we only have 20 bit of address bus, addressing up to 1MB of memory, therefore the processor simply ignored any bits outside of the least significant 20 bits when it boots up. Apparently, nobody do that anymore, but we are still required to turn that off. Again, for legacy reasons, the turning of that address bit involve talking to the PS2 controller. 

It turns out that the PS2 emulation software in v86 does not understand this command and just fall through a switch case, this is leading to the controller to believe there is a byte in the command buffer that the operating system is not accepting yet, leading to swallowing all the key stroke and never raise an interrupt request. 

Finally, [here](https://github.com/copy/v86/pull/540) is a fix for it. With this fix, Xv6 on v86 can handle the keyboard inputs now!