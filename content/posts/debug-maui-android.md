---
title: "Debug MAUI Android"
date: 2022-04-04T08:20:54-07:00
draft: false
---

These days I have been helping the MAUI team to fix some bugs. I always learn something new when I work on something new, and this time is no different. Today I am going to write about how to debug the Android code running inside a MAUI app.

# Procedure

## Step 1: Launching the App.
I start the MAUI app in Visual Studio as usual. In this post, I am going to assume that we know how to start an App in Visual Studio. We can even start it in debug mode so that we can debug the C# code as well. The mono debugger and the java debugger do not interfere each other and it is really convenient.

## Step 2: Finding the process
Next, we will want to attach the Java debugger. To do that, we need to know the process ID. We will use the [Android Debug Bridge](https://developer.android.com/studio/command-line/adb) (adb) tool.

If you have MAUI installed with the Android workload, you should have the Android SDK already. On my windows machine, the tool is available on

```
C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe
```

The tool has many options, but we will use only two of them. To find the process ID, we will use this command

```
C:\AndroidDebug>adb.exe jdwp
```

`jdwp` stands for [Java Debug Wire Protocol](https://docs.oracle.com/javase/8/docs/technotes/guides/jpda/jdwp-spec.html). Basically, this is a protocol that allows a Java debugger connects to so that it can remotely debug the process.

The command will list all the ID of all the process that expose the Java Debug Wire Protocol. That will include our app, and typically this is the last one in the output list.

```
C:\AndroidDebug>adb jdwp
528
691
...
5019
6204
```

> Note that the process does not stop. To terminate the process, press Ctrl + C.

## Step 3: Port Forwarding

Next, we will use the Android Debug Bridge to expose the debug port so that we can debug it. The port is currently opened on the Android emulator, but it is not accessible. To do make it accessible, the Android Debug Bridge will open a port on localhost so that it can be connected. The traffic are forwarded in and out the Android emulator so that we can debug.

The command to perform the port forwarding is as follow:

```
C:\AndroidDebug>adb.exe forward tcp:12345 jdwp:6204
```

The command is self-explanatory. On the localhost, we will open a port 12345 and it will be connected to the Java Debug Wire Protocol of process 6204.

## Step 4: Visual Studio Code Configuration

Now we can debug the Java code using a Java debugger. My personal choice is Visual Studio Code. Here is how I setup the Visual Studio Code for debugging Java.

- Install the Visual Studio Code Extension named "Extension Pack for Java".
- Create a new folder (I called mine `c:\AndroidDebug`).
- Open the folder.
- Click the (Run and Debug) button on the toolbar on the left hand side.
- Create a `launch.json`.

There are many options to set in `launch.json`, but we only need to set the mode to attach, the host, and the port number.

Here is my `launch.json` file:

```json
{
    // Use IntelliSense to learn about possible attributes.
    // Hover to view descriptions of existing attributes.
    // For more information, visit: https://go.microsoft.com/fwlink/?linkid=830387
    "version": "0.2.0",
    "configurations": [
        {
            "type": "java",
            "name": "Attach to Android",
            "request": "attach",
            "hostName": "localhost",
            "port": 12345,
            "sourcePaths": [
                "C:\\Users\\andrewau\\AppData\\Local\\Android\\Sdk\\sources\\android-30\\",
                "C:\\Users\\andrewau\\AppData\\Local\\Android\\Sdk\\sources\\android-31\\"
            ]
        }
    ]
}
```

## Step 5: Android Source Code

Notice I specified a couple of source paths above. This is done to ensure I can step the sources, but where does those sources come from? As you can tell from the path, it comes from the Android SDK.

I installed my Android source code using Android Studio. It is available using the Tools -> SDK Manager menu. At the lower right corner, there is a show package detail checkbox. This allowed me to pick the Android source code and install it.

## Step 6: Troubleshooting

There are a few caveats that I ran through:

- For some unknown reason, debugging does not work with the Android Studio is opened. Make sure you close all instances of Android Studio.
- Make sure you install the right version of the source code. You need both the SDK that is used to build the app as well as the API version that the emulator is running.
- If a breakpoint is hollow (i.e. not a solid red dot), it is invalid. It could be due to optimization or wrong API version. Check if you can set a breakpoint in some nearby lines.

# Final words

If you don't need to debug the Java code, you don't need any of these. In fact, MAUI should be encapsulating all of these from a developer so we don't need to do this. In my case, I need to debug something wrong in the MAUI implementation itself.