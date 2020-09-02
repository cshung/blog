---
title: "Automate WinDBG"
date: 2020-08-31T17:01:33-07:00
draft: false
---

# Automate WinDBG
WinDBG is a very convenient debugger, but typing the same command again and again is tiresome, especially during startup. GDB provided a mechanism to run some commands on startup through gdbinit. WinDBG has the equivalent, although it is not as clear. In this blog post, I am going to share how I automated WinDBG to run some commands on startup. For the impatient, just rush to the [summary](#summary) to get the commands.

# Startup command
WinDBG supports using the `-c` command line argument to specify a startup command. The option is documented [here](https://docs.microsoft.com/en-us/windows-hardware/drivers/debugger/windbg-command-line-options), search for `-c` in the page.

Here is a very simple example:

```
windbg.exe -c q cmd.exe
```

This will launch a debugger trying to debug `cmd.exe`. When the debugger starts up, it will quit itself immediately. Not super interesting, but it works.

# Run script file
We do not want to run just one command, we wanted to run a series of them. To do so, WinDBG has a command that run a script file. The command is documented [here](https://docs.microsoft.com/en-us/windows-hardware/drivers/debugger/-----------------------a---run-script-file-). The remark is probably the most interesting part to read, which explain how string escaping works.

I do not want to screw myself, so I will use a script file that does not contain a semicolon `;` in the file name. That allow us to use `$$>a<`. 

Let's first create an autodbg.script is a file with a single command `q`. Then type this into the command prompt, what do we see?

```
windbg.exe -c $$>a<autodbg.script cmd.exe
```

We see the debugger do not quit, and a new file named `a` is created. The problem is that the `>` and `<` operators are interpreted by the command prompt as redirection operators, we need to escape them using the caret `^` sign as follow:

```
windbg.exe -c $$^>a^<autodbg.script cmd.exe
```

Now it works to quit the prompt.

# Handling crash
Suppose a process is launched without a debugger, if it crashes, the process is just gone. To debug that crash, it is possible to tell the system to launch WinDBG and debug when a crash happens. To do so, you run the following command in an elevated command prompt:

```
windbg.exe -I
```

This will install WinDBG as a post-mortem debugger. We can experiment with it by having a crashing process as follow:

```c++
#include <iostream>
using namespace std;

int main()
{
    int* a = NULL;
    cout << "Pre crash" << endl;
    *a = 0;
    cout << "Post crash" << endl;
}

```
This is obviously an access violation. If we launch this process outside of Visual Studio, we should see WinDBG popping up an pointing right at the line that write to `*a`.

# Automate Crash Handling
When we invoke `windbg.exe -I`, all it does is that it writes to the registry. The key:

```
HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AeDebug
```

contains a value:

```
Debugger
```

pointing to the debugger command as follow:

```
"c:\toolssw\debuggers\amd64\windbg.exe" -p %ld -e %ld -g
```

That makes me ponder, is it possible to add the `-c` option above to automate some of the processing? The answer is yes!

```
"c:\toolssw\debuggers\amd64\windbg.exe" -c $$>a<c:\temp\autodbg.script -p %ld -e %ld -g
```

Note that this is not the command prompt, so we no longer need to escape the `>` and `<` signs. Also, I specified a full path for the script.

For Visual Studio user, the original value is this, you can restore it after the WinDBG session if you wish.

```
"C:\WINDOWS\system32\vsjitdebugger.exe" -p %ld -e %ld
```

# Launching executables
Launching an executable in a script has some interesting twist. The WinDBG way for launching an executable is using the .shell command. The command is documented [here](https://docs.microsoft.com/en-us/windows-hardware/drivers/debugger/-shell--command-shell-). Notice the `-i` option and the fact that `-i-` can avoid WinDBG prompting for inputs for the process, it is important if you don't 

To start with, I wrote a very simple executable to be launched to observe the executable launching behavior.

```c++
#include <string>
#include <fstream>
using namespace std;

int main(int argc, char** argv)
{
    ofstream outputFile("c:\\temp\\output.txt");
    for (int i = 0; i < argc; i++)
    {
        outputFile << argv[i] << endl;
    }
    outputFile.close();
    return 0;
}
```

This is compiled into `c:\temp\work.exe`. Obviously, the program simply write each argument into a separate line in the file.

The next natural step is to automate the launching of this executable. We write this script and launch it as usual.

```
.shell -i- -o- c:\temp\work.exe 
q
```

You would expect the debugger would quit after running the command, but it actually doesn't. Inspecting `c:\temp\output.txt` would find

```
c:\temp\work.exe 
;q
```

Now the mystery is clear. WinDBG is interpreting everything after the .shell as the executable name and then their arguments, making it impossible to chain any command after it.

How can we solve this problem? We can make a separate script that contains only the `.shell` command and chain it using the master script as follow:

`autodbg.script`
```
$$>a<run.script
q
```

`run.script`
```
.shell -i- -o- c:\temp\work.exe "what a test"
```

This will produce exactly what we want. The script runs, launch the executable with right arguments, and quits.

# Summary
The post documented the process of automating WinDBG in various scenarios. Here are the key takeaways:

## Command Line
Here is the command to run autodbg.script on startup:

```
windbg.exe -c $$^>a^<autodbg.script cmd.exe
```

## Registry for crash processing

Put this string into 
```
"c:\toolssw\debuggers\amd64\windbg.exe" -c $$>a<c:\temp\autodbg.script -p %ld -e %ld -g
```

this registry key 
```
HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AeDebug:Debugger
```

to have WinDBG to process crashes with autodbg.script.

## Launching executable

Put the `.shell -i- -o-` command in a separate script file and chain it with the master script to avoid interpreting the rest of the commands as arguments.