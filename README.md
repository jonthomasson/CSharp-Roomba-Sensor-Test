# RoombaSensorTester

Small .NET console app that connects to an iRobot Roomba/Create over a serial link, **beeps on start**, then continuously polls sensor data and prints:
- raw packets (optional)
- human-readable events (bump left/right, wall, wheel drops)

> Works on Windows and Linux. You provide the serial port and baud.

---

## Contents
- [Requirements](#requirements)
- [Windows Setup](#windows-setup)
- [Linux Setup](#linux-setup)
- [Finding Your Serial Port](#finding-your-serial-port)
- [Build](#build)
- [Run](#run)
- [Options](#options)
- [Typical Combinations](#typical-combinations)
- [What You Should See](#what-you-should-see)
- [Troubleshooting](#troubleshooting)
- [Safety Notes](#safety-notes)

---

## Requirements
- .NET **SDK 8.0** (or newer)
- USB-to-serial adapter (FTDI/CP210x/CH34x), or native UART
- Roomba or Create/Open Interface cable

**Baud & packet group by model (typical):**
- **Create 2 / newer** → `--baud 115200 --group 6` (52-byte packet)
- **Older Roomba (4xx/Discovery)** → `--baud 57600 --group 0` (26-byte packet)

---

## Windows Setup

1) **Install .NET 8 SDK**
```powershell
winget install Microsoft.DotNet.SDK.8
dotnet --info
