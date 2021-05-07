---
title: "Hello Reflection"
date: 2020-12-05T14:53:55-08:00
draft: false
---

To get started using `ILCompiler.Reflection.ReadyToRun`, let's construct a simple HelloWorld project. If you don't know what it is, please take a look at the [previous post](https://cshung.github.io/posts/introduction-to-ilcompiler-reflection-readytorun/).

# Getting started
We will create a .NET Core console application. Create a folder named `HelloReflection` and invoke `dotnet new console`.

```
C:\dev>mkdir HelloReflection

C:\dev>cd HelloReflection

C:\dev\HelloReflection>dotnet new console
The template "Console Application" was created successfully.

Processing post-creation actions...
Running 'dotnet restore' on C:\dev\HelloReflection\HelloReflection.csproj...
  Determining projects to restore...
  Restored C:\dev\HelloReflection\HelloReflection.csproj (in 80 ms).
Restore succeeded.
```

# Using my personal feed
As of now, `ILCompiler.Reflection.ReadyToRun` is not part of .NET Core, therefore, we need to specify where we can find a copy of `ILCompiler.Reflection.ReadyToRun.dll` to use. To facilitate my collaboration with ILSpy, I created a personal feed that serves the binary. To use my feed, we can add this `Nuget.config` at the root directory of the project.

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="Nuget Official" value="https://api.nuget.org/v3/index.json" />
    <add key="cshung_public_development" value="https://pkgs.dev.azure.com/cshung/public/_packaging/development/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

This will allow us to add a package reference as follow:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ILCompiler.Reflection.ReadyToRun" Version="1.0.13-alpha" />
  </ItemGroup>

</Project>
```

Note the versioning scheme, it is still in alpha, meaning API is going to change from build to build. This is necessary for a still evoling library.

# Instantiating a ReadyToRunReader

`ReadyToRunReader` is the entry point to the functionality. We must first construct an instance of a `ReadyToRunReader`. Fortunately, there is a constructor that takes only two parameters. 

The second parameter is easy, it is just the full path to the binary that we wanted to read. The first one is an `IAssemblyResolver`, it looks like we need to provide an instance of it to resolve an assembly. What exactly does that mean?

It means that when the reader needs to find referenced assembly, it needs to be able to read it. To make things simple in HelloWorld, we will just provide a skeleton implementation that simply always fails.

```c#
    class MyAssemblyResolver : IAssemblyResolver
    {
        public IAssemblyMetadata FindAssembly(MetadataReader metadataReader, AssemblyReferenceHandle assemblyReferenceHandle, string parentFile)
        {
            throw new NotImplementedException();
        }

        public IAssemblyMetadata FindAssembly(string simpleName, string parentFile)
        {
            throw new NotImplementedException();
        }
    }
```

With that, now we can instantiate a `ReadyToRunReader` as follow:

```c#
ReadyToRunReader reader = new ReadyToRunReader(new MyAssemblyResolver(), @"C:\temp\System.Private.CoreLib.dll");
```

Note that we have to use `System.Private.CoreLib.dll`. This is the only library that is not referencing any other assemblies. If we were to use other ready to run binary instead, we would hit the `NotImplementedException` we just thrown.

# Using the ReadyToRunReader
There are a lot of properties on the `ReadyToRunReader`. The most important one is probably `Methods`. The property gives us all the methods that are ready to run compiled. As a first look, we can take a look at the method's signature string. Here is the code.

```c#
foreach (var method in reader.Methods)
{
    Console.WriteLine(method.SignatureString);
}
```

That will show us a long list of method signatures.

```
void Microsoft.CodeAnalysis.EmbeddedAttribute..ctor()
void System.Runtime.CompilerServices.IsUnmanagedAttribute..ctor()
void System.Runtime.CompilerServices.NullableAttribute..ctor(byte)
void System.Runtime.CompilerServices.NullableAttribute..ctor(byte[])
void System.Runtime.CompilerServices.NullableContextAttribute..ctor(byte)
...
```

This conclude our very simple HelloWorld example. The full source code of the HelloReflection can be found [here](https://github.com/cshung/blog-samples/tree/main/HelloReflection). Next, we will look into the other properties on the `ReadyToRunMethod` object. Stay tuned.