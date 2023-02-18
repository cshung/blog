---
title: "POH Tuning (Part 1 - What is my pinned object heap size?)"
date: 2021-01-15T11:18:17-08:00
draft: false
---

In this series, I am going to talk about my work to tune the [pinned object heap](https://github.com/dotnet/runtime/blob/master/docs/design/features/PinnedHeap.md). The first step to tuning is to understand how it performs now. Unfortunately, the tools for analyzing the performance is incomplete. 

# Pinned Object Heap size (and the event tracing mechanisms)
With the tools available in .NET 5 time frame, we did not know the size of the pinned object heap. We had the information in the trace, but we did not have the parser to understand them. I fixed it in this [PR](https://github.com/microsoft/perfview/pull/1295). In this post, I will talk about the event tracing infrastructure.

## Overall architecture

The performance tracing infrastructure starts with the runtime emitting a trace. The trace is sent through a transport to a event listener. The transport could be the [EventPipe](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/eventpipe) on all platforms or [Event Tracing for Windows](https://docs.microsoft.com/en-us/windows-hardware/test/wpt/event-tracing-for-windows) on Windows only. Regardless of the transport, eventually, these traces ends up in a trace file of various formats. A component called [TraceEvent](https://github.com/microsoft/perfview/tree/master/src/TraceEvent) is then used to parse the content of the file. Using TraceEvent, these trace files turns into a collection of structured objects. Tools like [PerfView](https://github.com/Microsoft/perfview) can then be used to present these data in a user friendly manner. An example would look like this:

{{<mermaid>}}
graph TD;
    A[Runtime]
    B[Trace File]
    C[GCHeapStat]
    D[GCStats]
    A -->|EventPipe| B
    B -->|TraceEvent| C
    C -->|PerfView| D
    style A width:100px
    style B width:100px
    style C width:100px
    style D width:100px
{{</mermaid>}}

We will take a look at these components one by one, having a focus on the on-the-wire format of the event.

## Runtime Event Emission
To understand how the runtime emits events, we would like to get the debugger to stop at the point where it is serializing the event. The magic function we wanted to stop is `EventPipeWriteEventGCHeapStats_V2`. If we run an application with tracing turned on using EventPipe, we should see this breakpoint got hit with the following call stack:

```txt
CoreCLR!EventPipeWriteEventGCHeapStats_V2
CoreCLR!FireEtwGCHeapStats_V2
CoreCLR!GCToCLREventSink::FireGCHeapStats_V2
CoreCLR!GCEventFireGCHeapStats_V2
CoreCLR!WKS::GCHeap::UpdatePostGCCounters
...
```

### Some metaprogramming tricks
You will never find the implementation of the first two frames in our code because it is code-generated during the build process. This is nothing new, serializers are typically code generated at compile time. The third frame is just a forwarder to avoid GC being coupled to the generated code. The most interesting frame is actually frame 5, where the event originates. It uses a `FIRE_EVENT` macro, which is defined as follow in gceventstatus.h line 267:

```txt
#define FIRE_EVENT(name, ...) GCEventFire##name(__VA_ARGS__)
```

For those who are unfamiliar, the `##` is the [token pasting operator](https://docs.microsoft.com/en-us/cpp/preprocessor/token-pasting-operator-hash-hash?view=msvc-160). It allows the macro preprocessor to translate `FIRE_EVENT(A, b, c)` to `GCEventFireA(b, c)`.

The `GCEventFireGCHeapStats_V2` is actually defined just a few lines above. Note that the code defined a `KNOWN_EVENT` macro and then included `gcevents.h`. `gcevents.h` invokes the `KNOWN_EVENT` macro many times, once for each type of event. That turns into a code generator that generates all the `GCEventFire*` functions!

### Actual serialization
At the core of `EventPipeWriteEventGCHeapStats_V2`, the core of the code look like this:

```c++
    bool success = true;
    success &= WriteToBuffer(GenerationSize0, buffer, offset, size, fixedBuffer);
    success &= WriteToBuffer(TotalPromotedSize0, buffer, offset, size, fixedBuffer);
    success &= WriteToBuffer(GenerationSize1, buffer, offset, size, fixedBuffer);
```

Although it looks like we are always writing to the same offset, actually, offset is passed by reference and is advanced by the size of the argument. So in reality, this code simply put all of them in sequential order.

```c++
template <typename T>
bool WriteToBuffer(const T &value, char *&buffer, size_t& offset, size_t& size, bool &fixedBuffer)
{
    if (sizeof(T) + offset > size)
    {
        if (!ResizeBuffer(buffer, size, offset, size + sizeof(T), fixedBuffer))
            return false;
    }

    memcpy(buffer + offset, (char *)&value, sizeof(T));
    offset += sizeof(T);
    return true;
}

```
This concludes the serialization of the event into a sequence of bytes. Subsequent code will pack them up and write them into the `nettrace` file.

## TraceEvent Deserialization
The general idea of TraceEvent is that it works on callbacks. It is easiest to understand that by starting from `PerfViewGCData.WriteHtmlBody()`. The code looks like this:

```c#
protected override void WriteHtmlBody(TraceLog dataFile, TextWriter writer, string fileName, TextWriter log)
{
    using (var source = dataFile.Events.GetSource())
    {
        m_gcStats = new Dictionary<int, Microsoft.Diagnostics.Tracing.Analysis.TraceProcess>();
        Microsoft.Diagnostics.Tracing.Analysis.TraceLoadedDotNetRuntimeExtensions.NeedLoadedDotNetRuntimes(source);
        // ...
        source.Process();
        // ...
    }
}
```

To begin with, we setup a `source`. During the call to `NeedLoadedDotNetRuntimes`, we will call `TraceLoadedDotNetRuntime.SetupCallbacks`. This function registers a callback as follow:

```c#
source.Clr.GCHeapStats += delegate (GCHeapStatsTraceData data)
{
    var process = data.Process();
    var stats = currentManagedProcess(data);
    TraceGC _gc = TraceGarbageCollector.GetCurrentGC(stats);

    var sizeAfterMB = (data.GenerationSize1 + data.GenerationSize2 + data.GenerationSize3 + data.GenerationSize4) / 1000000.0;
    if (_gc != null)
    {
        _gc.HeapStats = new GCHeapStats()
        {
            // ...
        }
    }
}
```
The `data` is an instance of `GCHeapStatsTraceData`, which is constructed only twice during the setup. Those instances are the templates. When we call `source.Process()`, trace event will use these template objects to issue callbacks. These callbacks will use the properties on the `data`. Here is how these properties are implemented:

```c#
public long GenerationSize0 { get { return GetInt64At(0); } }
public long TotalPromotedSize0 { get { return GetInt64At(8); } }
public long GenerationSize1 { get { return GetInt64At(16); } }
...
public long GenerationSize4 { get { if (Version >= 2) { return GetInt64At(94); } return 0; } }
public long TotalPromotedSize4 { get { if (Version >= 2) { return GetInt64At(102); } return 0; } }
```

The idea is that the underlying data is shifted to read different events, therefore the call to the `GenerationSize0` property will read the 0th byte to the 7th byte of the underlying event object and reinterpret that as a `long`. This correlates with the serialization code above.

The last two lines were added by me to fix the parsing of the pinned object heap size. Notice that I have a version check there to make sure when we read old traces, we won't be reading things that do not exist there. In general, this is how event versioning work, as we introduce new fields into the event, we just put it at the very end so that the old trace reader will just ignore that. The fact that the pinned object heap size is unknown is exactly this. Before the change, it just read up to version 1.

# Consumption of these new properties
This will introduce a new property called GenerationSize4 in `GCHeapStat` class, but it is not useful if we just leave it there. Various places in PerfView consuming `GenerationSize3` is now modified to also consume `GenerationSize4` in a reasonable way. That concludes the whole fix to the tooling to support pinned object heap size.