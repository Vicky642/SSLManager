<div align="center">

# 🔒 SslManager

**The zero-config, cross-platform Local HTTPS Tool with an Avalonia Desktop GUI.**

[![Release](https://img.shields.io/github/v/release/Vicky642/sslmanager?style=flat-square)](https://github.com/Vicky642/sslmanager/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)
[![Sponsor](https://img.shields.io/static/v1?label=Sponsor&message=%E2%9D%A4&logo=GitHub&color=ff69b4&style=flat-square)](https://github.com/sponsors/Vicky642)

<img src="https://via.placeholder.com/600x300.png?text=SslManager+CLI+and+Tray+App+Snapshot" alt="SslManager Demo" />
<br/>

*Stop wrestling with OpenSSL commands just to get the lock icon on localhost.*

</div>

---

## 💡 What is it?

SslManager is a modern replacement for manually constructing certificate authorities and typing out complex `openssl` configurations. It generates 4096-bit Root CAs and issues ECDSA or RSA certificates instantly for your local development domains (`localhost`, `myapp.local`, etc).

It features a beautiful **Command-Line Interface** (CLI) and an **Avalonia-powered System Tray GUI**, letting you inspect your local certificates, track expirations, and automatically push trust into your host OS (Windows, macOS, Linux).

## ✨ Features

- **No Strings Attached:** A single standalone executable. No Java, No Python, No Node required to run.
- **Cross-Platform OS Trust:** Automatically injects your Root CA into the Windows `X509Store`, the macOS `login.keychain`, or the Linux `/etc/ca-certificates` seamlessly.
- **Desktop Companion App:** Right-click the system tray icon to view your expiring certificates in a rich data grid.
- **Modern Cryptography:** Issues native RSA-2048 or blazing-fast ECDSA P-256 certs that modern chromium browsers love.
- **Native File Exports:** Export `.cer` and `.pfx` effortlessly to bind to your IIS/Nginx/Docker/Node.js web servers.

---

## ⚡ Installation

The easiest way to get SslManager is downloading the latest standalone executable from our [Releases Page](https://github.com/Vicky642/sslmanager/releases) for your OS.

### For .NET Developers
Install it instantly as a global tool!
```bash
dotnet tool install -g sslmgr
```

---

## 🚀 Quick Start

Solving localhost HTTPs warnings only takes 2 commands:

**1. Initialize the Root CA**
Creates a secure localized authority key on your machine.
```bash
sslmgr init
```

**2. Create a Certificate for your custom domain**
Generates your certificate, and attaches Subject Alternative Names automatically so it operates on sub-domains securely.
```bash
sslmgr create myapp.local --san *.myapp.local
```

**3. Inject Trust**
Force Chromium / Safari / Firefox / Edge to honor your new certificates securely.
```bash
sslmgr trust myapp.local
```

That's it! 

> Need to assign certificates inside a Linux Docker container? 
> Use `sslmgr share myapp.local --out ./docker-certs` to spit out the raw files instantly!

---

## 🛠 Avalonia UI System Tray

Don't want to use the terminal? We got you.
Launch `SslManager.Tray.exe` and enjoy managing your certs visually!

- **Green Badges:** Certificates are tracked and healthy.
- **Yellow Warning Badges:** Certificates expiring within 30 days. Renew with one click!
- **Deep Integrations:** Avalonia natively binds the system tray without taking up an active taskbar window on macOS or Windows Notification Area!

---

## ❤️ Sponsoring 

SslManager is open source. If it saves you or your company 4 hours of screaming at OpenSSL, consider sponsoring the project so I can continue to maintain Avalonia patches natively! 

👉 [Sponsor the Developer](https://github.com/sponsors/Vicky642)

## 🤝 Contributing

We love pull requests! Please check our [Contributing Guidelines](CONTRIBUTING.md) to get started. 

## 📜 License
This software is licensed under the [MIT License](LICENSE).
