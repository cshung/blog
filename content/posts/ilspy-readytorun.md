---
title: "ILSpy.ReadyToRun"
date: 2020-07-25T19:50:22-07:00
draft: false
---

# Introduction
[ILSpy.ReadyToRun](https://github.com/icsharpcode/ILSpy/wiki/ILSpy.ReadyToRun) is my side project. It is a plugin in ILSpy that allow us to inspect the precompiled code in a ReadyToRun image. My vision is to make all information available in a ready to run image to be displayed in a human readable manner.

# What can it do?
If you open a ReadyToRun image in ILSpy, it will show up exactly as it was. You can see all the types, the methods, and the decompiled C# code, as usual.

![ilspy-readytorun-01.png](/ilspy-readytorun/ilspy-readytorun-01.png "ilspy-readytorun-01.png")

Notice the highlighted combo box? With [ILSpy 6.0](https://github.com/icsharpcode/ILSpy/releases/tag/v6.0), now you can switch the language to ReadyToRun. Once you changed the language to ReadyToRun, it will start displaying ReadyToRun related information. For example, you can see the native code disassembled like this:

![ilspy-readytorun-02.png](/ilspy-readytorun/ilspy-readytorun-02.png "ilspy-readytorun-02.png")



The disassembly comes with some comments that describe it. The comments are derived from the various data structure available in the ready to run image. For now, it supports:

1. Debug bounds

The `; Prolog` line means it is the beginning of the prolog. The `; IL_0000` and `; IL_0001` means the block of assembly code between this two lines implements the IL instructions starting from `IL_0000` (inclusive) to `IL_0001` (exclusive).

2. Call sites

The native call instruction is encoded so that it calls into an indirection cell. Often, these indirection cells are described by the ready to run data so that we know where it will lead to. Therefore, these call sites are annotated with the callee's information (such as its name and it tokens). These annotations are links, if you click on them, it will lead you to the type, method or metadata.

3. Unwind info

Whenever we need to unwind the stack (e.g. exceptions), the native stack walker will need information to unwind the stack. You can turn on the unwind info option in `View -> Options` menu. This will show you the various unwind opcode associated with the prolog.

# What else could it do?
ILSpy.ReadyToRun is still a work in progress, there are more information that is available in the ReadyToRun format that are not currently displayed. For example, we have information to describe the relationship between various memory locations and what does they represent. Also, we have information to instruct how the GC should interpret these data as references. Contributions are welcomed. 

- [[ILSpy.ReadyToRun] Display ReadyToRun header information](https://github.com/icsharpcode/ILSpy/issues/1883)
- [[ILSpy.ReadyToRun] Decorate the disassembly with GCInfo](https://github.com/icsharpcode/ILSpy/issues/1885)
- [[ILSpy.ReadyToRun] Supporting other architectures](https://github.com/icsharpcode/ILSpy/issues/1887)
