---
title: "NativeAOT Development"
date: 2023-05-30T12:19:41-07:00
draft: false
---

This post is written to document the process of development runtime or library features for NativeAOT. This is *NOT* meant for NativeAOT compiling your application.

# Hello Native AOT
To begin with, we will start with building a HelloWorld application and then NativeAOT compile it. The application itself is unimportant, we can simply use `dotnet new console` to create a HelloWorld application. 

Next, we add the following MSBuild property to the project file:

```xml
<PublishAot>true</PublishAot>
```

This will enable the NativeAOT compilation during publish. We can then publish the application using the following command:

```bash
dotnet publish
```

This will generate a native executable in the publish folder, the problem is that it will be using whatever version of .NET you are targetting and installed.

For our purpose, we would like to use our own build of .NET. To do that, we can add a specific reference to the ILCompiler nuget package on the dotnet 8 transport feed as follow:

```xml
<ItemGroup>
    <PackageReference Include="Microsoft.DotNet.ILCompiler" Version="*-*"/>
</ItemGroup>
```

And also make the build system aware of the `dotnet8-transport` feed through the `nuget.config` file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <!--To inherit the global NuGet package sources remove the <clear/> line below -->
    <clear />
    <add key="dotnet8" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet8/nuget/v3/index.json" />
    <add key="nuget" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

Now the build will try to use the latest build it can find on the transport feed and hopefully this will be recent enough for the development.

# Determine the build
The next step is to determine the build we are using, this is to synchronize our development so that the change is based on top of the right commit.

To do that, clean the `bin` and `obj` folder to force native compilation to happen. Then publish the app with the logging option as follow:

```bash
dotnet publish -bl
```

This will generate a file named `msbuild.binlog`, then we can inspect the log using the [MSBuild Binary and Structured Log Viewer](https://msbuildlog.com/). In particular, search for the `IlcCompile` target and look for the `CommandLineArguments` of the `Exec` task. That should show you the path to the `Ilc` executable, while should include the version number.

# Determining the commit hash
The version number is not enough, we need the commit hash so that we can synchronize the build. The easiest way to determine the commit hash is to look at the `Ilc` executable itself. On Windows, we can use the file explorer and right click to see the detail tab to find it. For Linux, we can use the `strings` command on the executable to find it.

# Synchronize the build
Once we have the commit hash, we can now synchronize our runtime repo clone to that commit hash, apply the desired changes, and build the runtime repo as usual. NativeAOT binaries are built as part of the CoreCLR subset.

# Use the build
As we can see, the `Exec` task of the `IlcCompile` target contains the Ilc command line. It references a `rsp` file. We can change the RSP file to point to our locally built binaries. Similarly, the `Exec` task of the `LinkNative` target contains the Link command line, and we can just as easily change that target. With that, we can run those command lines and generate the final binary using our locally build NativeAOT binaries as we needed.