using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace vPinEventMonitor.Pipe;

/// <summary>
/// Named pipe server. Broadcasts pinball events to all connected clients.
/// Ported from PEP/PipeServer/PipeServer/PipeServer.cs (namespace updated).
/// </summary>
public class PipeServer
{
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern SafeFileHandle CreateNamedPipe(
       string pipeName, uint dwOpenMode, uint dwPipeMode,
       uint nMaxInstances, uint nOutBufferSize, uint nInBufferSize,
       uint nDefaultTimeOut, IntPtr lpSecurityAttributes);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern int ConnectNamedPipe(SafeFileHandle hNamedPipe, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DisconnectNamedPipe(SafeFileHandle hHandle);

    [StructLayout(LayoutKind.Sequential)]
    struct SECURITY_DESCRIPTOR
    {
        public byte revision, size;
        public short control;
        public IntPtr owner, group, sacl, dacl;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;
    }

    private const uint SECURITY_DESCRIPTOR_REVISION = 1;

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool InitializeSecurityDescriptor(ref SECURITY_DESCRIPTOR sd, uint dwRevision);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool SetSecurityDescriptorDacl(ref SECURITY_DESCRIPTOR sd, bool daclPresent, IntPtr dacl, bool daclDefaulted);

    public class Client
    {
        public SafeFileHandle handle = null!;
        public FileStream stream = null!;
    }

    public delegate void MessageReceivedHandler(int eventType, byte[] message);
    public event MessageReceivedHandler? MessageReceived;

    public delegate void ClientConnectedHandler();
    public event ClientConnectedHandler? ClientConnected;

    public delegate void ClientDisconnectedHandler();
    public event ClientDisconnectedHandler? ClientDisconnected;

    const int BUFFER_SIZE = 8192;

    Thread? _listenThread;
    readonly List<Client> _clients = new();

    public int TotalConnectedClients
    {
        get { lock (_clients) { return _clients.Count; } }
    }

    public string? PipeName { get; private set; }
    public bool Running { get; private set; }

    public void Start(string pipename)
    {
        PipeName = pipename;
        _listenThread = new Thread(ListenForClients) { IsBackground = true };
        _listenThread.Start();
        Running = true;
    }

    void ListenForClients()
    {
        SECURITY_DESCRIPTOR sd = new();
        InitializeSecurityDescriptor(ref sd, SECURITY_DESCRIPTOR_REVISION);
        SetSecurityDescriptorDacl(ref sd, true, IntPtr.Zero, false);

        IntPtr ptrSD = Marshal.AllocCoTaskMem(Marshal.SizeOf(sd));
        Marshal.StructureToPtr(sd, ptrSD, false);

        SECURITY_ATTRIBUTES sa = new()
        {
            nLength = Marshal.SizeOf(sd),
            lpSecurityDescriptor = ptrSD,
            bInheritHandle = 1
        };

        IntPtr ptrSA = Marshal.AllocCoTaskMem(Marshal.SizeOf(sa));
        Marshal.StructureToPtr(sa, ptrSA, false);

        while (true)
        {
            SafeFileHandle clientHandle = CreateNamedPipe(
                PipeName!,
                0x40000003, // DUPLEX | FILE_FLAG_OVERLAPPED
                0, 255, BUFFER_SIZE, BUFFER_SIZE, 0, ptrSA);

            if (clientHandle.IsInvalid) continue;

            int success = ConnectNamedPipe(clientHandle, IntPtr.Zero);
            if (success == 0) { clientHandle.Close(); continue; }

            Client client = new() { handle = clientHandle };
            lock (_clients) _clients.Add(client);

            new Thread(Read) { IsBackground = true }.Start(client);

            ClientConnected?.Invoke();
        }
    }

    void Read(object? clientObj)
    {
        Client client = (Client)clientObj!;
        client.stream = new FileStream(client.handle, FileAccess.ReadWrite, BUFFER_SIZE, true);
        byte[] readBuffer = new byte[BUFFER_SIZE];

        while (true)
        {
            int bytesRead = 0;
            int eventType = 0;

            using MemoryStream ms = new();

            int totalSize = client.stream.Read(readBuffer, 0, 4);
            if (totalSize == 0) break;

            totalSize = BitConverter.ToInt32(readBuffer, 0);
            client.stream.Read(readBuffer, 0, 4);
            eventType = BitConverter.ToInt32(readBuffer, 0);

            do
            {
                int numBytes = client.stream.Read(readBuffer, 0, Math.Min(totalSize - bytesRead, BUFFER_SIZE));
                ms.Write(readBuffer, 0, numBytes);
                bytesRead += numBytes;
            } while (bytesRead < totalSize);

            if (bytesRead == 0) break;

            MessageReceived?.Invoke(eventType, ms.ToArray());
        }

        lock (_clients)
        {
            DisconnectNamedPipe(client.handle);
            client.stream.Close();
            client.handle.Close();
            _clients.Remove(client);
        }

        ClientDisconnected?.Invoke();
    }

    public void SendMessage(int eventType, byte[] message)
    {
        lock (_clients)
        {
            byte[] messageLength = BitConverter.GetBytes(message.Length);
            byte[] eventCode = BitConverter.GetBytes(eventType);

            foreach (Client client in _clients)
            {
                try
                {
                    client.stream.Write(messageLength, 0, 4);
                    client.stream.Write(eventCode, 0, 4);
                    client.stream.Write(message, 0, message.Length);
                    client.stream.Flush();
                }
                catch { }
            }
        }
    }
}
