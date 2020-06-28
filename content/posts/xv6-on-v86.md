---
title: "Xv6 on v86"
date: 2020-06-28T09:27:30-07:00
draft: false
---

# Xv6 on v86
In this post, I will talk about my journey to get [Xv6](https://github.com/mit-pdos/xv6-public) running on [v86](https://copy.sh/v86/). Xv6 is a unix-like teaching OS developed by [MIT](https://web.mit.edu/). v86 is an x86 virtual machine running on the browser developed by [Fabian](https://github.com/copy).

The key characteristic of these two systems is that they are **simple** compared to their industrial-strength counterparts (compared to the Linux kernel or the QEMU emulator). Being simple, it is easy to **understand**. My goal is to build an understanding of the operating systems and virtual machine internals, and they give us an excellent opportunity.

Unfortunately, as is, they don't work together. I spent about a couple of weeks of my personal free time to get them to work together and learn a lot as a result. Looking backward, the fact that they don't work together was an opportunity! Watch [here](https://www.youtube.com/watch?v=bCFhfRemzOw) to see it is working in action.

The rest of this post will talk about the various things I did to get it to work. It was an eye-opener for me.

# Dead on arrival - triple fault on QEMU
My first step is simply running `xv6` using QEMU. Unfortunate, we have a dead on arrival, this is what is shown:
```
qemu-system-i386 -serial mon:stdio -drive file=fs.img,index=1,media=disk,format=raw -drive file=xv6.img,index=0,media=disk,format=raw -smp 2 -m 512 
EAX=80010011 EBX=00010094 ECX=00000000 EDX=000001f0
ESI=00010094 EDI=00000000 EBP=00007bf8 ESP=00007bdc
EIP=00100028 EFL=00000086 [--S--P-] CPL=0 II=0 A20=1 SMM=0 HLT=0
ES =0010 00000000 ffffffff 00cf9300 DPL=0 DS   [-WA]
CS =0008 00000000 ffffffff 00cf9a00 DPL=0 CS32 [-R-]
SS =0010 00000000 ffffffff 00cf9300 DPL=0 DS   [-WA]
DS =0010 00000000 ffffffff 00cf9300 DPL=0 DS   [-WA]
FS =0000 00000000 00000000 00000000
GS =0000 00000000 00000000 00000000
LDT=0000 00000000 0000ffff 00008200 DPL=0 LDT
TR =0000 00000000 0000ffff 00008b00 DPL=0 TSS32-busy
GDT=     00007c60 00000017
IDT=     00000000 000003ff
CR0=80010011 CR2=00000040 CR3=00109000 CR4=00000010
DR0=00000000 DR1=00000000 DR2=00000000 DR3=00000000 
DR6=ffff0ff0 DR7=00000400
EFER=0000000000000000
Triple fault.  Halting for inspection via QEMU monitor.
```
It isn't even `v86`, this is obviously not intended, and I learn more about [triple fault](https://en.wikipedia.org/wiki/Triple_fault) here.

It doesn't take me long to figure out this issue is already fixed. [James Houghton](https://github.com/jamesthoughton) from [University of Virginia](https://www.virginia.edu/) already provided a [fix](https://github.com/mit-pdos/xv6-public/pull/115), all I did is to take his fix.

Even the author of the fix doesn't really know what has gone wrong. As far as we can tell, there is a regression in the `binutils` so that the generated file is different. I could have spent some time to debug the related tools, but I decided to move on since understanding those tools was not my goal.

# Unimplemented sse
The next thing I did is to run the generated image under `v86`. Before my fix, it fails with this JavaScript exception.

```js
Error
    at eval (eval at dbg_assert_failed (http://localhost:8000/src/log.js:1:1), <anonymous>:1:1)
    at dbg_assert_failed (http://localhost:8000/src/log.js:118:5)
    at dbg_assert (http://localhost:8000/src/log.js:111:9)
    at CPU.unimplemented_sse (http://localhost:8000/src/cpu.js:3294:5)
    at Array.t.<computed> (http://localhost:8000/src/instructions.js:1933:24)
    at Array.t32.<computed> (http://localhost:8000/src/instructions.js:45:36)
    at CPU.run_prefix_instruction (http://localhost:8000/src/cpu.js:1246:39)
    at Array.t.<computed> (http://localhost:8000/src/instructions.js:1253:9)
    at CPU.cycle_internal (http://localhost:8000/src/cpu.js:1205:23)
    at CPU.do_many_cycles_unsafe (http://localhost:8000/src/cpu.js:1148:14)"
    ...
```

When I first see this error. I thought maybe the image contained some SSE instructions and the emulator does not support it well. Especially I read this on the front page of `v86` GitHub:

```
An x86 compatible CPU. The instruction set is around Pentium 1 level. Some features are missing, more specifically:
...
- MMX, SSE
...
```

I would like to avoid it. At the very least, I would like to understand what SSE instruction is being used and see if we can do something about it. Navigating down the stack I saw the `CPU.cycle_internal` frame and that gave me some hints.

```js
CPU.prototype.cycle_internal = function()
{
    this.previous_ip = this.instruction_pointer;
    ...
    // call the instruction
    this.table[opcode](this);
    ...
}
```

The debugger nicely gave us the `previous_ip` is `0x7d49`. This is not familiar, normal instruction pointers are not that small. Recalling that during boot, the bootloader is loaded at address `0x7c00`. That is a good reason for me to believe that `0x7d49` is part of the bootloader, and indeed it is. Looking at the `bootblock.asm` generated at the build we see that is an `endbr32` instruction.

```s
    7d48:       c3                      ret

00007d49 <bootmain>:
{
    7d49:       f3 0f 1e fb             endbr32
    7d4d:       55                      push   %ebp
```

This is interesting, what is `endbr32`? [This](https://stackoverflow.com/questions/56120231/how-do-old-cpus-execute-the-new-endbr64-and-endbr32-instructions) link provided me with an answer.

It is part of the [Intel Control-flow Enforcement Technology](https://software.intel.com/content/www/us/en/develop/articles/technical-look-control-flow-enforcement-technology.html). Usually, instructions that come with new technology may not be available in low-end machines, and compilers usually require extra flags to leverage them. Why does `gcc` generate a `CET` instruction in this case? [This](https://gcc.gnu.org/bugzilla/show_bug.cgi?id=83087) nice discussion summarizes it. The key idea is that:

- These instructions are supposed to run correctly on older machines, and
- We wanted them to be enabled by default so that we are secure-by-default.

These are all sensible design choices. The first point is very interesting because it implies the instruction is designed to work on low-end machines. Looking up the manuals, it looks like the instruction does nothing unless there is a hack, which means we can simply tell `v86` to ignore it. To that end, I submitted a [PR](https://github.com/copy/v86/pull/344) to `v86` and it is accepted.

With the fix, now the boot moves on further, but we are stuck with ... 

# Infinite loop
Opening the JavaScript console, we see these messages

```
...
log.js:13 Previous message repeated 2048 times
log.js:13 17:05:54+347 [IO  ] Read from unmapped memory space, addr=0xFEE00300
log.js:13 Previous message repeated 2048 times
...
```

It is always the same address, very suspicious to be an infinite loop. This time around, the VM does not stop, so let's make it stop. Somewhere in the JavaScript code, there must be a `console.log`, so I find that one out (in log.js) and put a breakpoint there.

With that I extract a stack trace of what is going on:
```
"Error
    at eval (eval at do_the_log (http://localhost:8000/src/log.js:13:9), <anonymous>:1:1)
    at do_the_log (http://localhost:8000/src/log.js:13:9)
    at dbg_log_ (http://localhost:8000/src/log.js:76:21)
    at Array.<anonymous> (http://localhost:8000/src/io.js:43:13)
    at CPU.mmap_read32 (http://localhost:8000/src/memory.js:72:48)
    at CPU.read32s (http://localhost:8000/src/memory.js:152:21)
    at CPU.safe_read32s (http://localhost:8000/src/cpu.js:1581:21)
    at CPU.read_e32s (http://localhost:8000/src/cpu.js:3396:21)
    at Array.t32.<computed> (http://localhost:8000/src/instructions.js:400:20)
    at CPU.cycle_internal (http://localhost:8000/src/cpu.js:1205:23)"
```
Again, going back to the `CPU.cycle_internal` frame, I obtained the instruction pointer to be `-2146424736`, a curious negative number, which is `0x80102860` in hex. This looks interesting. Usually, we reserve the top 2 gigabytes of the address space to the kernel, so this is mostly like some code in the kernel.

Indeed, I found it in kernel.asm. Again, this is generated disassembly:

```s
  while(lapic[ICRLO] & DELIVS)
80102860:       8b 90 00 03 00 00       mov    0x300(%eax),%edx
80102866:       80 e6 10                and    $0x10,%dh
80102869:       75 f5                   jne    80102860 <lapicinit+0xc0>
  lapic[index] = value;
8010286b:       c7 80 80 00 00 00 00    movl   $0x0,0x80(%eax)
80102872:       00 00 00
  lapic[ID];  // wait for write to finish, by reading
80102875:       8b 40 20                mov    0x20(%eax),%eax
    ;
```

It looks like to loop just never ends. Reading through lapic.c, I figure this is called the local advanced programmable interrupt controller. And it appears that this is not described in this list of emulated hardware in the `v86` page, leading me to believe it won't work. I had email conservation with Fabian and confirmed that is the case.

# The lying BIOS

It is unclear here, but running into that infinite loop is odd. We should have exited early here:

```c
void
lapicinit(void)
{
  if(!lapic)
    return;
  ...
  while(lapic[ICRLO] & DELIVS)
}
```

This leads me to track down where does the variable `lapic` got set. The code nicely tells me where that is:

```c
volatile uint *lapic;  // Initialized in mp.c
```

and here it is in mp.c:

```c
  ...
  if((conf = mpconfig(&mp)) == 0)
    panic("Expect to run on an SMP");
  ismp = 1;
  lapic = (uint*)conf->lapicaddr;
  ...
```

Reading through the `mpconfig` function, it searches for the MP configuration table and performs some validation. The search must have succeeded, otherwise, we would have hit the `panic`. Why would such a configuration table exist in memory?

Since I didn't get to the panic line, I suspect conf is not NULL. Let's prove that. I changed the `== 0` to `!= 0`. Here is what I get:

```
SeaBIOS (version rel-1.10.0-39-g3fdabae-dirty-20170530_143849-nyu)
Booting from Floppy
Boot failed: could not read the boot disk

Booting from Hard Disk...
lapicid 0: panic: Expect to run on an SMP
  801032a9 8010306f 0 0 0 0 0 0 0 0_
```

So it proved my hypothesis, `conf` is indeed not NULL. But something else is more interesting, we are able to output something to the screen, which is a great debugging aid. Let's take a look at how panic is implemented.

```c
void
panic(char *s)
{
  int i;
  uint pcs[10];

  cli();
  cons.locking = 0;
  // use lapiccpunum so that we can call panic from mycpu()
  cprintf("lapicid %d: panic: ", lapicid());
  cprintf(s);
  cprintf("\n");
  getcallerpcs(&s, pcs);
  for(i=0; i<10; i++)
    cprintf(" %p", pcs[i]);
  panicked = 1; // freeze other CPU
  for(;;)
    ;
}
```

Now we know, there is a function named `cprintf()` that can print something to the screen. Now, let's use that to figure out what is `conf`. I changed it so that it prints the value of `conf`.

From:
```c
  ...
  if((conf = mpconfig(&mp)) == 0)
     panic("Expect to run on an SMP");
  ...
```

To:
```c
  ...
  conf = mpconfig(&mp);
  cprintf("conf = %p\n", conf);
  if(conf == 0)
     panic("Expect to run on an SMP");
  ...
```

Now we see this, the address is always `800f6ef0`. Now we can investigate what populated that address.
```
SeaBIOS (version rel-1.10.0-39-g3fdabae-dirty-20170530_143849-nyu)
Booting from Floppy
Boot failed: could not read the boot disk

Booting from Hard Disk...
conf = 800f6ef0
```

Ideally, we would like to have a data breakpoint, but obviously we don't. With a virtual machine, we could simply add a check in the 'memory' access routines.

The stack above already told us one of them: `CPU.read32s`. Reading through the code, it looks like all memory access eventually funnels through these two debug routines:

- `CPU.prototype.debug_read`
- `CPU.prototype.debug_write`

Unfortunately the argument to these functions use physical address, and `800f6ef0` is obviously a virtual address. I just guessed the physical address is `f6ef0` and added these instrumentation to the `debug_read` and `debug_write` method as follow:

```
    if (addr <= 0xf6ef0 && 0xf6ef0 < addr + size)
    {
        console.log("Hit");
    }
```
and then I put a breakpoint on the `console.log` line. Now it hits multiple times, the key ones being:

```js
CPU.debug_write (memory.js:19)
CPU.write_blob (memory.js:292)
CPU.load_bios (cpu.js:1052)
CPU.init (cpu.js:648)
v86.init (main.js:76)
...
```

and

```js
CPU.debug_read (memory.js:40)
CPU.read8 (memory.js:101)
CPU.safe_read8 (cpu.js:1553)
CPU.read_e8 (cpu.js:3363)
t32.<computed> (instructions.js:3813)
t32.<computed> (instructions.js:45)
CPU.cycle_internal (cpu.js:1200)
...
```

The first stack indicates the signature is hardcoded in the ROM BIOS. In the second stack, the `cycle_internal` frame indicates the `previous_ip` is -2146422112 (0x801032a0), that correspond to the `mpinit` code in kernel.asm.

```s
void
mpinit(void)
{
801031f0:	f3 0f 1e fb          	endbr32 
...
80103299:	8d b4 26 00 00 00 00 	lea    0x0(%esi,%eiz,1),%esi
    sum += addr[i];
801032a0:	0f b6 88 00 00 00 80 	movzbl -0x80000000(%eax),%ecx
```

We can also inspect the register values by invoking `this.debug.dumpregs()`:

```
this.debug.dump_regs()
log.js:13 16:08:00+268 [CPU ] eax=0x000F6EF0 ecx=0x0000000A edx=0x00000F16 ebx=0x000F6F78   ds=0x0010 es=0x0010 fs=0x0000
log.js:13 16:08:00+269 [CPU ] esp=0x8010B570 ebp=0x8010B598 esi=0x000F6E90 edi=0x800F6E80   gs=0x0000 cs=0x0008 ss=0x0010
```
Now we are certain that the virtual address is indeed 0x80000000 + (eax=0x000F6EF0) = 0x800F6EF0, and it was loaded by the BIOS.

So we proved that it is the BIOS that tricked us to believe the virtual machine has SMP support, but in reality, it doesn't.

In retrospect, this is to be expected. The BIOS was dated 2017. The fact that it assumes the underlying hardware has SMP is not far fetched at all.

# Moving forward
The virtual machine is missing what we needed. There are 2 potential solutions:

1. Implement SMP in v86.
2. Make xv6 work without SMP

Solution 1 sounds like a lot of work, and so is solution 2. At this point, I was about to declare failure. But then I stumbled upon this [commit](https://github.com/cshung/xv6-public/commit/4f14d8d1e594bdf45e36a035f6c3fd4ca959711e) on the xv6 repository.

That was just luck, xv6 used to work without SMP. All I needed to do is to undo that change and bring back the single processor support. This is done in this [commit](https://github.com/cshung/xv6-public/commit/33016524d7b905010c6ba768e869f50e0e769e1b) Of course, the code would also handle the lie BIOS told us, and that is handled in this [commit](https://github.com/cshung/xv6-public/commit/1a229ce3d039b7e82ad00287eced46117bd05a10)

# Unmapped memory
With the fix, now we are running into another infinite loop with this output:

```
...
log.js:13 16:49:49+855 [IO  ] Write to unmapped memory space, addr=0x0802BDD0 value=0x01010101
log.js:13 16:49:49+856 [IO  ] Write to unmapped memory space, addr=0x0802BDD4 value=0x01010101
...
```

Just like before, I set a breakpoint on console.log, and here is the stack.
```js
do_the_log (log.js:13)
dbg_log_ (log.js:82)
(anonymous) (io.js:47)
CPU.mmap_write32 (memory.js:79)
CPU.write_aligned32 (memory.js:269)
stosd (string.js:594)
t32.<computed> (instructions.js:671)
CPU.run_prefix_instruction (cpu.js:1241)
t.<computed> (instructions.js:1253)
CPU.cycle_internal (cpu.js:1200)
...
```

The `previous_ip` is -2146417291 = 0x80104575, that correspond to a `memset` function:

```s
void*
memset(void *dst, int c, uint n)
{
80104540:	f3 0f 1e fb          	endbr32 
...
80104575:	f3 ab                	rep stos %eax,%es:(%edi)
...
```

Unlike earlier, we are now stuck. The code is a generic function that doesn't give many contexts. Normally, we would like the debugger to give us a stack trace to see which function called it. Here we illustrate how we could do something similar manually. 

The current stack pointer can be found using `this.debug.dump_regs()`.

```
this.debug.dump_regs()
log.js:13 17:12:36+136 [CPU ] eax=0x01010101 ecx=0x000000B6 edx=0x8802B000 ebx=0x00010000   ds=0x0010 es=0x0010 fs=0x0000
log.js:13 17:12:36+137 [CPU ] esp=0x8010B550 ebp=0x8010B558 esi=0x8E000000 edi=0x8802BD28   gs=0x0000 cs=0x0008 ss=0x0010
```

So we can look into the stack slot (`mem8` is indexed using physical memory)
```js
for (var temp = 0; temp < 20; temp++) {
    temp_a = this.mem8[0x10B550 + temp * 4 + 0]
    temp_b = this.mem8[0x10B550 + temp * 4 + 1]
    temp_c = this.mem8[0x10B550 + temp * 4 + 2]
    temp_d = this.mem8[0x10B550 + temp * 4 + 3]
    temp_e = temp_d * 256 * 256 * 256 + temp_c * 256 * 256 + temp_b * 256 + temp_a;
    console.log(temp +" :  " + temp_e.toString(16));
}

0 :  8802b000
1 :  0
2 :  8010b578
3 :  801024eb
4 :  8802b000
5 :  1
6 :  1000
7 :  80107866
8 :  0
9 :  8802d000
10 :  8010b598
11 :  8010264d
12 :  8802b000
13 :  0
14 :  8010b5a8
15 :  8010576d
16 :  80112830
17 :  100b4
18 :  8010b5b8
19 :  801031b2
```
We could have done it the right way, inspect the code, figure out where is the return address. But it is just much easier to dump all dwords in the stack and match with the source code.
Now we have some context, 0x801024EB, which is `kfree`. Further ahead of time, it is 0x8010264d, which is `kinit2`. Note that `kinit2` didn't directly call `kfree`, it must be inlined. That is why we shouldn't waste time trying to manually stack walk accurately.

Reading through `kinit2`, it is just freeing the memory by filling it with patterns. The real problem is that the range is defined by a symbolic constant `PHYSTOP` which is hardcoded to 0xE000000. This is suspicious, how could the kernel know how much memory there is? The answer is actually written on the manual, on page 37 (or the last page of chapter 2, whatever version you are reading)

```
Xv6 should determine the actual RAM configuration, instead of assuming 240MB
```

Now the mystery is solved. All we need to get around that infinite loop is to set the memory limit to 240 MB

# HaveDisk1
I promise this is the last hurdle. With the memory configuration set right. The code runs further an give this error:

```
SeaBIOS (version rel-1.10.0-39-g3fdabae-dirty-20170530_143849-nyu)
Booting from Floppy
Boot failed: could not read the boot disk

Booting from Hard Disk...
cpu0: starting 0
lapicid 0: panic: iderw: ide disk 1 not present
 80102374 80100191 80101529 801015a7 8010376c 8010578a 0 0 0 0_
```

This is an obvious error, that we need a separate disk. It could be a hard task, but turn out the code is mostly already available on v86. All I did in [this commit](https://github.com/copy/v86/commit/6ab0fa85a985743ac35c2b470dc4ecafdad6889e) is to configure it and surface it to the web frontend. With the slave hard disk connected to `fs.img`, the system booted successfully!.
