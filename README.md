# Blocky

Blocky
Blocking domains the way you want it.
Copyright EonaCat (Jeroen Saey) 2017-2023
https://blocky.eonacat.com

#### Windows notice:

It could be that you need to run the following command in CMD:
net stop http

#### Linux notice:

If you want to install Blocky on a Linux environment be sure that port 53 is free.
(If there is no command output there is no other application using port 53)

You can check if the port is free using the following command:
```bash
sudo lsof -i :53
```

1. Open the resolved.conf using the command:
```bash
sudo nano /etc/systemd/resolved.conf
```

2. Uncomment the following lines and set their values:

```bash
[Resolve]
DNS=127.0.0.1 
#FallbackDNS=
#Domains=
#LLMNR=no
#MulticastDNS=no
#DNSSEC=no
#DNSOverTLS=no
#Cache=no
DNSStubListener=no
#ReadEtcHosts=yes
```

3. Save the file and quit.

4. Create a symbolic link for /run/systemd/resolve/resolv.conf with /etc/resolv.conf as the destination:

```bash
sudo ln -sf /run/systemd/resolve/resolv.conf /etc/resolv.conf
```

5. Reboot your system.

6. Port 53 is now free for Blocky to use.
