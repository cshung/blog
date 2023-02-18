---
title: "Watch Soccer with SOCKS"
date: 2022-12-17T10:32:43-08:00
draft: false
---

As of the time of writing, tomorrow is the final for World Cup 2022. It is a pity to miss it. Unfortunately, in the US, unlike most other places in the world, you can't watch it for free, while it is available for free online in many other countries.

To get around that restriction, I ...

- hosted a cheap Linux virtual machine on Azure.
- used SSH as a sock server.
- used SSH tunnel to encrypt the traffic, and
- made Chrome use the sock server and start watching

# Part 1 - hosting a Linux Virtual Machine
After logging on the Azure portal, I can easily create a Linux virtual machine. This machine is going to be used as a proxy only, so I chose the least possible size with HDD storage only. We need to at least make it accessible through SSH, so I leave the SSH port open and ask Azure to use my public key for authentication.

# Part 2 - Socks server
SSH can be used as a SOCKS server. To do that, we need to make sure ssh localhost work first, this can be done by making sure a matching private key is available in the `./ssh` folder with permission `400`.

Using the -D option, we can make it runs a SOCK server listening on the localhost port 10086 as follows:

```txt
ssh -N -D "0.0.0.0:10086" localhost
```

The `-D` is the key option that allow SSH to serve as a SOCKS server, the -N tells SSH to not start an interactive prompt.

By default, port 10086 is not exposed to the public. Therefore we cannot use the SOCKS server remotely yet, but we can test that on the Azure VM as follows:

```txt
curl --socks5-hostname localhost:10086 http://www.google.com/
```

This will instruct curl to download the Google homepage using localhost:10086 as the sock server, and it should work if and only if the socks server is on.

# Part 3 - SSH tunnel
Next, we want to use the remote SOCKS server securely. We will do that using SSH port mapping using this command:

```txt
ssh -N -L 12580:localhost:10086 <azure-vm-ip>
```

The `-L` option indicates that the port 12580 on the machine will be mapped to the 'localhost' (as interpreted by the remote machine) port 10086. This mean we will be able to access the SOCKS server through localhost:12580.

Of course, we can test that locally:

```txt
curl --socks5-hostname localhost:12580 http://www.google.com/
```

# Part 4 - Launch Chrome with proxy
Last but not least, here is how I do it in OSX. Using a command line option works much better than the system settings. To make sure things work, it is best to use the incognito mode to avoid cookies.

```txt
/Applications/Google\ Chrome.app/Contents/MacOS/Google\ Chrome --proxy-server="socks://localhost:12580"
```

