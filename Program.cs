// --- iRobot OI opcodes (subset) ---
using System.IO.Ports;

const byte START = 128; // 0x80
const byte CONTROL = 130; // a.k.a. SAFE on older docs (Create prefers SAFE=131)
const byte SAFE = 131; // 0x83
const byte FULL = 132; // 0x84
const byte DRIVE = 137; // 0x89
const byte SONG = 140; // 0x8C
const byte PLAY = 141; // 0x8D
const byte SENSORS = 142; // 0x8E
const byte STREAM = 148; // 0x94 (not used here)
const byte QUERYLIST = 149; // 0x95 (not used here)
const byte PAUSERESUME = 150; // 0x96 (not used here)

// Sensor packet groups commonly used
// 0 -> "All (26 bytes)" on older Roomba
// 6 -> "All (52 bytes)" on Create 2 (recommended)
static int ExpectedLengthForGroup(int group) => group switch
{
    0 => 26,
    1 => 10,
    2 => 6,
    3 => 10,
    4 => 14,
    5 => 12,
    6 => 52,
    _ => 26
};

static void Usage()
{
    Console.WriteLine("Usage: dotnet run -- --port <PORT> [--baud <RATE>] [--group <0|6>] [--raw]");
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run -- --port /dev/ttyUSB0 --baud 115200 --group 6 --raw");
    Console.WriteLine("  dotnet run -- --port COM3 --baud 57600 --group 0");
}

string? portName = null;
int baud = 115200;      // Create 2 default. Try 57600 for classic Roomba.
int group = 6;          // 52-byte “all” packet (Create 2). Use 0 for 26-byte.
bool printRaw = false;

// --- Parse args ---
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--port":
        case "-p":
            portName = args[++i];
            break;
        case "--baud":
            baud = int.Parse(args[++i]);
            break;
        case "--group":
            group = int.Parse(args[++i]);
            break;
        case "--raw":
            printRaw = true;
            break;
        case "--help":
        case "-h":
            Usage();
            return;
        default:
            Console.WriteLine($"Unknown arg: {args[i]}");
            Usage();
            return;
    }
}

if (string.IsNullOrWhiteSpace(portName))
{
    Console.WriteLine("ERROR: --port is required.");
    Usage();
    return;
}

// --- Open serial port ---
using var sp = new SerialPort(portName, baud, Parity.None, 8, StopBits.One)
{
    ReadTimeout = 250,       // per read call
    WriteTimeout = 250,
    Handshake = Handshake.None,
    DtrEnable = false,
    RtsEnable = false
};

try { sp.Open(); }
catch (Exception ex)
{
    Console.WriteLine($"ERROR opening {portName} at {baud}: {ex.Message}");
    return;
}

// --- Helper local functions ---
void SendByte(byte b)
{
    sp.Write(new[] { b }, 0, 1);
}

void SendBytes(params byte[] bytes)
{
    sp.Write(bytes, 0, bytes.Length);
}

// Read exactly N bytes or return null on timeout
byte[]? ReadExact(int count, int overallTimeoutMs = 1000)
{
    var buf = new byte[count];
    int off = 0;
    var start = Environment.TickCount;

    while (off < count)
    {
        if (Environment.TickCount - start > overallTimeoutMs) return null;
        try
        {
            int n = sp.Read(buf, off, count - off);
            if (n > 0) off += n;
        }
        catch (TimeoutException)
        {
            // keep looping until overall timeout
        }
        Thread.Sleep(1);
    }
    return buf;
}

string Hex(byte[] data) => BitConverter.ToString(data).Replace("-", " ");

// Basic beep so user knows we’re connected
void Beep()
{
    // Load a one-note song to slot 0, then play it
    // SONG [140], song#=0, length=1, note=72 (C5), dur=16 (~250ms)
    SendBytes(SONG, 0, 1, 72, 16);
    SendBytes(PLAY, 0);
}

// Request a sensor packet group and read response
byte[]? GetSensorsGroup(int grp)
{
    int expected = ExpectedLengthForGroup(grp);
    SendBytes(SENSORS, (byte)grp);
    return ReadExact(expected, overallTimeoutMs: 1000);
}

// Extract booleans from the first few bytes of the packet.
// For both group 0 (26B) and group 6 (52B), byte 0 is BUMPS+WHEEL DROPS,
// byte 1 is WALL (Create: “Wall IR Sensor” as a 0/1 value).
static (bool bumpLeft, bool bumpRight, bool wall, bool wdLeft, bool wdCenter, bool wdRight)
ParseCoreFlags(byte[] pkt)
{
    byte b0 = pkt[0]; // Bumps & Wheel Drops bitfield
    bool bumpRight = (b0 & 0x01) != 0;
    bool bumpLeft = (b0 & 0x02) != 0;
    bool wdRight = (b0 & 0x04) != 0;
    bool wdLeft = (b0 & 0x08) != 0;
    bool wdCenter = (b0 & 0x10) != 0;

    byte wallByte = pkt[1];
    bool wall = wallByte != 0;

    return (bumpLeft, bumpRight, wall, wdLeft, wdCenter, wdRight);
}

// --- Initialize OI ---
try
{
    // Enter OI and SAFE mode
    SendByte(START);
    Thread.Sleep(50);
    SendByte(SAFE);
    Thread.Sleep(50);

    // Beep so user knows we’re alive
    Beep();
    Console.WriteLine("Beep! Connected.");
    Console.WriteLine($"Port={portName}, Baud={baud}, Group={group} (expect {ExpectedLengthForGroup(group)} bytes)");
    Console.WriteLine("Press Ctrl+C to stop.\n");

    // Warm-up read
    _ = GetSensorsGroup(group);

    // Previous state for edge-triggered prints
    bool prevBL = false, prevBR = false, prevWall = false, prevWDL = false, prevWDC = false, prevWDR = false;

    while (true)
    {
        var pkt = GetSensorsGroup(group);
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (pkt == null)
        {
            Console.WriteLine($"{ts}: WARN timeout waiting for sensor packet");
            continue;
        }

        if (printRaw)
        {
            Console.WriteLine($"{ts}: RAW {Hex(pkt)}");
        }

        var (bumpLeft, bumpRight, wall, wdLeft, wdCenter, wdRight) = ParseCoreFlags(pkt);

        // Edge-triggered events
        if (bumpLeft && !prevBL) Console.WriteLine($"{ts}: EVENT bump_left");
        if (bumpRight && !prevBR) Console.WriteLine($"{ts}: EVENT bump_right");
        if (wall && !prevWall) Console.WriteLine($"{ts}: EVENT wall");
        if (wdLeft && !prevWDL) Console.WriteLine($"{ts}: EVENT wheel_drop_left");
        if (wdCenter && !prevWDC) Console.WriteLine($"{ts}: EVENT wheel_drop_center");
        if (wdRight && !prevWDR) Console.WriteLine($"{ts}: EVENT wheel_drop_right");

        // Periodic status line (compact)
        Console.WriteLine($"{ts}: BL={(bumpLeft ? 1 : 0)} BR={(bumpRight ? 1 : 0)} WALL={(wall ? 1 : 0)} WD[L/C/R]={(wdLeft ? 1 : 0)}/{(wdCenter ? 1 : 0)}/{(wdRight ? 1 : 0)}");

        prevBL = bumpLeft; prevBR = bumpRight; prevWall = wall;
        prevWDL = wdLeft; prevWDC = wdCenter; prevWDR = wdRight;

        Thread.Sleep(100); // ~10Hz
    }
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
}
finally
{
    try
    {
        // Stop motion just in case (drive 0, straight)
        byte velHi = 0, velLo = 0, radHi = 0x80, radLo = 0x00; // 0x8000 == straight
        SendBytes(DRIVE, velHi, velLo, radHi, radLo);
    }
    catch { /* ignore */ }
    if (sp.IsOpen) sp.Close();
}
