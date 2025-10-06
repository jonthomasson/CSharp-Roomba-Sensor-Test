RoombaSensorTester
==================

Small .NET console app that connects to an iRobot Roomba/Create over a serial link, **beeps on start**, then continuously polls sensor data and prints:
*   raw packets (optional)
    
*   human-readable events (bump left/right, wall, wheel drops)
    

> Works on Windows and Linux. You provide the serial port and baud.

* * *

Contents
--------

*   [Requirements](#requirements)
    
*   [Windows Setup](#windows-setup)
    
*   [Linux Setup](#linux-setup)
    
*   [Finding Your Serial Port](#finding-your-serial-port)
    
*   [Build](#build)
    
*   [Run](#run)
    
*   [Options](#options)
    
*   [Typical Combinations](#typical-combinations)
    
*   [What You Should See](#what-you-should-see)
    
*   [Troubleshooting](#troubleshooting)
    
*   [Safety Notes](#safety-notes)
    

* * *

Requirements
------------

*   .NET **SDK 8.0** (or newer)
    
*   USB-to-serial adapter (FTDI/CP210x/CH34x), or native UART
    
*   Roomba or Create/Open Interface cable
    
**Baud & packet group by model (typical):**
*   **Create 2 / newer** → `--baud 115200 --group 6` (52-byte packet)
    
*   **Older Roomba (4xx/Discovery)** → `--baud 57600 --group 0` (26-byte packet)
    

* * *

Windows Setup
-------------

1.  **Install .NET 8 SDK**
    

`winget install Microsoft.DotNet.SDK.8 dotnet --info`

2.  **Find your COM port**
    
*   Device Manager → **Ports (COM & LPT)** (e.g., `COM3`)
    
*   Or PowerShell:
    

`Get-CimInstance Win32_SerialPort | Select Name,DeviceID`

* * *

Linux Setup
-----------

1.  **Install .NET 8 SDK**
    
*   On Ubuntu/Debian (example):
    

`sudo apt-get update sudo apt-get install -y dotnet-sdk-8.0 dotnet --info`

_(If your distro uses a different package name, install the Microsoft-packaged .NET SDK for your system.)_
2.  **Permissions for serial ports**
    

`sudo usermod -a -G dialout $USER # log out/in (or reboot) so the new group applies`

* * *

Finding Your Serial Port
------------------------

**Windows:** `COM3`, `COM4`, etc. (Device Manager)
**Linux (USB adapters):**

`ls -l /dev/ttyUSB* /dev/ttyACM* 2>/dev/null`

**Linux (Raspberry Pi GPIO UART):**
*   Disable the serial login shell and enable UART (e.g., `raspi-config`).
    
*   Use `/dev/serial0`.
    

* * *

Build
-----

From the project folder (contains `RoombaSensorTester.csproj` and `Program.cs`):

`dotnet build`

**Optional (standalone):**

`# Windows x64 dotnet publish -c Release -r win-x64 --self-contained false  # Linux x64 dotnet publish -c Release -r linux-x64 --self-contained false`

* * *

Run
---

**Windows (COM3 example):**

`dotnet run -- --port COM3 --baud 115200 --group 6 --raw`

**Linux (USB example):**

`dotnet run -- --port /dev/ttyUSB0 --baud 115200 --group 6 --raw`

**Linux (Pi UART example):**

`dotnet run -- --port /dev/serial0 --baud 57600 --group 0`

> You should hear a **beep** immediately after the app enters SAFE mode.

* * *

Options
-------

`--port <PORT>    Serial port. Examples: COM3, /dev/ttyUSB0, /dev/serial0   (required) --baud <RATE>    Baud rate (default 115200)  [115200 for Create 2, 57600 for older Roomba] --group <N>      Sensor packet group (default 6). Common: 6=52 bytes, 0=26 bytes --raw            Also print raw packet bytes in hex each cycle`

* * *

Typical Combinations
--------------------

*   **Create 2 / newer:**  
    `--baud 115200 --group 6`
    
*   **Older Roomba (26-byte “all”):**  
    `--baud 57600 --group 0`
    

* * *

What You Should See
-------------------

*   On connect: **“Beep! Connected.”**
    
*   When you press bumpers or approach a wall:
    

`EVENT bump_left EVENT bump_right EVENT wall EVENT wheel_drop_left ...`

*   A compact status line each cycle:
    

`<timestamp>: BL=0 BR=1 WALL=0 WD[L/C/R]=0/0/0`

*   If `--raw` is set, the 26/52-byte packet in hex is also printed.
    

* * *

Troubleshooting
---------------

**Windows**
*   _Access to the port is denied_ → Another app (IDE/Serial Monitor/PuTTY) has it open. Close it.
    
*   _No beep/no events_ → Try switching baud (115200 ↔ 57600) and/or packet group (6 ↔ 0). Recheck the COM number after driver install.
    
**Linux**
*   _Permission denied_ → Ensure you’re in the `dialout` group and re-log.
    
*   _No data/timeout_ → Wrong device path (`/dev/ttyUSB0` vs `/dev/ttyACM0`), or wrong baud/group.
    
*   _Raspberry Pi UART_ → Make sure the serial login shell is **disabled** and hardware UART **enabled**; use `/dev/serial0`.
    
**Everyone**
*   Unreliable output can be a bad cable/adapter or noise; try a different USB port/cable.
    
*   Very old/alternate OI revisions may arrange fields differently; if wall/bump don’t line up, try the other group.
    

* * *

Safety Notes
------------

*   **SAFE vs FULL:** The app uses **SAFE** mode; cliff/wheel-drop protection is active. Do **not** switch to FULL near stairs.
    
*   Ensure the robot is on a flat surface during tests.
