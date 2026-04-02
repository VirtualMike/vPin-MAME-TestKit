using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace vPinEventMonitor.Pipe;

/// <summary>
/// Named pipe client. Connects to the PEP server to receive pinball events.
/// Ported from PEP/PipeClient/PipeClient/PipeClient.cs (namespace updated).
/// </summary>
public class PipeClient
{
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern SafeFileHandle CreateFile(
       string pipeName, uint dwDesiredAccess, uint dwShareMode,
       IntPtr lpSecurityAttributes, uint dwCreationDisposition,
       uint dwFlagsAndAttributes, IntPtr hTemplate);

    public delegate void MessageReceivedHandler(int eventType, byte[] message);
    public event MessageReceivedHandler? MessageReceived;

    public delegate void ServerDisconnectedHandler();
    public event ServerDisconnectedHandler? ServerDisconnected;

    const int BUFFER_SIZE = 8192;

    FileStream? _stream;
    SafeFileHandle? _handle;
    Thread? _readThread;

    public bool Connected { get; private set; }
    public string? PipeName { get; private set; }

    public void Connect(string pipename)
    {
        if (Connected) throw new InvalidOperationException("Already connected to pipe server.");

        PipeName = pipename;

        _handle = CreateFile(
            PipeName,
            0xC0000000, // GENERIC_READ | GENERIC_WRITE
            0, IntPtr.Zero,
            3,          // OPEN_EXISTING
            0x40000000, // FILE_FLAG_OVERLAPPED
            IntPtr.Zero);

        if (_handle.IsInvalid) return;

        Connected = true;
        _readThread = new Thread(Read) { IsBackground = true };
        _readThread.Start();
    }

    public void Disconnect()
    {
        if (!Connected) return;
        Connected = false;
        PipeName = null;
        _stream?.Close();
        _handle?.Close();
        _stream = null;
        _handle = null;
    }

    void Read()
    {
        _stream = new FileStream(_handle!, FileAccess.ReadWrite, BUFFER_SIZE, true);
        byte[] readBuffer = new byte[BUFFER_SIZE];

        while (true)
        {
            int bytesRead = 0;
            int eventType = 0;

            using MemoryStream ms = new();

            int totalSize = _stream.Read(readBuffer, 0, 4);
            if (totalSize == 0) break;

            totalSize = BitConverter.ToInt32(readBuffer, 0);
            _stream.Read(readBuffer, 0, 4);
            eventType = BitConverter.ToInt32(readBuffer, 0);

            do
            {
                int numBytes = _stream.Read(readBuffer, 0, Math.Min(totalSize - bytesRead, BUFFER_SIZE));
                ms.Write(readBuffer, 0, numBytes);
                bytesRead += numBytes;
            } while (bytesRead < totalSize);

            if (bytesRead == 0) break;

            MessageReceived?.Invoke(eventType, ms.ToArray());
        }

        if (Connected)
        {
            _stream?.Close();
            _handle?.Close();
            _stream = null;
            _handle = null;
            Connected = false;
            PipeName = null;
            ServerDisconnected?.Invoke();
        }
    }

    public void SendMessage(int eventType, byte[] message)
    {
        if (_stream == null) return;
        _stream.Write(BitConverter.GetBytes(message.Length), 0, 4);
        _stream.Write(BitConverter.GetBytes(eventType), 0, 4);
        _stream.Write(message, 0, message.Length);
        _stream.Flush();
    }
}
