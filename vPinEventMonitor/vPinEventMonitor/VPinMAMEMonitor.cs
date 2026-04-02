namespace vPinEventMonitor;

// ---- Event argument types ----

public class LampsChangedEventArgs : EventArgs
{
    public (int LampId, bool On)[] Changes { get; }
    public LampsChangedEventArgs((int LampId, bool On)[] changes) => Changes = changes;
}

public class SolenoidsChangedEventArgs : EventArgs
{
    public (int SolenoidId, bool On)[] Changes { get; }
    public SolenoidsChangedEventArgs((int SolenoidId, bool On)[] changes) => Changes = changes;
}

public class GIsChangedEventArgs : EventArgs
{
    public (int GiId, int Brightness)[] Changes { get; }
    public GIsChangedEventArgs((int GiId, int Brightness)[] changes) => Changes = changes;
}

public class DmdFrameEventArgs : EventArgs
{
    public byte[] Pixels { get; }  // 128*32 bytes, values 0-100
    public DmdFrameEventArgs(byte[] pixels) => Pixels = pixels;
}

public class SwitchesPolledEventArgs : EventArgs
{
    public bool[,] Matrix { get; }  // [col 0..11, row 1..8]
    public SwitchesPolledEventArgs(bool[,] matrix) => Matrix = matrix;
}

// ---- Monitor class ----

/// <summary>
/// Polls VPinMAME (PinMAME) for lamp, solenoid, GI, DMD, and switch events.
/// Uses dynamic COM late-binding so no tlbimp/COMReference is required.
/// Must be used on the UI thread (System.Windows.Forms.Timer fires on UI thread).
/// </summary>
public class VPinMAMEMonitor : IDisposable
{
    // All events raised on the UI thread - no Invoke() needed in handlers.
    public event EventHandler<LampsChangedEventArgs>?     LampsChanged;
    public event EventHandler<SolenoidsChangedEventArgs>? SolenoidsChanged;
    public event EventHandler<GIsChangedEventArgs>?       GIsChanged;
    public event EventHandler<DmdFrameEventArgs>?         DmdFrame;
    public event EventHandler<SwitchesPolledEventArgs>?   SwitchesPoll;
    public event EventHandler<string>?                    StatusChanged;

    public bool IsRunning    { get; private set; }
    public bool WpcNumbering { get; private set; }

    private dynamic? _vpm;
    private readonly System.Windows.Forms.Timer _timer;
    private int _tickCount;

    const int SWITCH_MATRIX_COLUMNS = 11;
    const int SWITCH_MATRIX_ROWS    = 8;

    public VPinMAMEMonitor()
    {
        _timer = new System.Windows.Forms.Timer { Interval = 16 };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>
    /// Activates VPinMAMELib.Controller via COM, loads the ROM, and starts polling.
    /// </summary>
    /// <param name="gameName">VPinMAME ROM name (e.g. "ft_l5").</param>
    /// <param name="ownerHandle">Handle of the owner window passed to vpm.Run().</param>
    public void Start(string gameName, IntPtr ownerHandle)
    {
        Stop();

        var type = Type.GetTypeFromProgID("VPinMAMELib.Controller");
        if (type == null)
        {
            StatusChanged?.Invoke(this, "ERROR: VPinMAMELib not registered. Install VPinMAME.");
            return;
        }

        try
        {
            _vpm = Activator.CreateInstance(type);
            _vpm!.GameName = gameName;
            _vpm.ShowDMDOnly = false;
            _vpm.Run((int)ownerHandle);

            WpcNumbering = (bool)_vpm.WPCNumbering;

            IsRunning = true;
            _tickCount = 0;
            _timer.Start();

            StatusChanged?.Invoke(this, $"Running — {gameName} (WPC={WpcNumbering})");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"ERROR starting VPinMAME: {ex.Message}");
            _vpm = null;
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _timer.Stop();

        try { _vpm?.Stop(); } catch { }
        _vpm = null;
        IsRunning = false;
        StatusChanged?.Invoke(this, "Stopped");
    }

    /// <summary>Sets a switch state on the running game (for injection).</summary>
    public void SetSwitch(int switchIndex, bool on)
    {
        if (_vpm == null) return;
        try { _vpm.Switch[switchIndex] = on; } catch { }
    }

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }

    // ---- Polling ----

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_vpm == null) return;

        PollLamps();
        PollSolenoids();
        PollGIs();
        PollDmd();

        _tickCount++;
        if (_tickCount % 5 == 0)
            PollSwitches();
    }

    private void PollLamps()
    {
        try
        {
            object? raw = _vpm!.ChangedLamps;
            if (raw is not object[,] arr) return;

            int count = arr.Length / 2;
            var changes = new (int LampId, bool On)[count];
            for (int i = 0; i < count; i++)
                changes[i] = ((int)arr[i, 0], (int)arr[i, 1] == 1);

            LampsChanged?.Invoke(this, new LampsChangedEventArgs(changes));
        }
        catch { }
    }

    private void PollSolenoids()
    {
        try
        {
            object? raw = _vpm!.ChangedSolenoids;
            if (raw is not object[,] arr) return;

            int count = arr.Length / 2;
            var changes = new (int SolenoidId, bool On)[count];
            for (int i = 0; i < count; i++)
                changes[i] = ((int)arr[i, 0], (int)arr[i, 1] == 1);

            SolenoidsChanged?.Invoke(this, new SolenoidsChangedEventArgs(changes));
        }
        catch { }
    }

    private void PollGIs()
    {
        try
        {
            object? raw = _vpm!.ChangedGIs;
            if (raw is not object[,] arr) return;

            int count = arr.Length / 2;
            var changes = new (int GiId, int Brightness)[count];
            for (int i = 0; i < count; i++)
                changes[i] = ((int)arr[i, 0], (int)arr[i, 1]);

            GIsChanged?.Invoke(this, new GIsChangedEventArgs(changes));
        }
        catch { }
    }

    private void PollDmd()
    {
        try
        {
            object? raw = _vpm!.RawDmdPixels;
            if (raw is not object[] arr) return;

            var pixels = new byte[arr.Length];
            for (int i = 0; i < arr.Length; i++)
                pixels[i] = (byte)arr[i];

            DmdFrame?.Invoke(this, new DmdFrameEventArgs(pixels));
        }
        catch { }
    }

    private void PollSwitches()
    {
        try
        {
            bool[,] matrix = new bool[SWITCH_MATRIX_COLUMNS + 1, SWITCH_MATRIX_ROWS + 1];

            for (int c = 0; c <= SWITCH_MATRIX_COLUMNS; c++)
            {
                for (int r = 1; r <= SWITCH_MATRIX_ROWS; r++)
                {
                    int index = WpcNumbering ? c * 10 + r : c * 8 + r;
                    matrix[c, r] = (bool)_vpm!.Switch[index];
                }
            }

            SwitchesPoll?.Invoke(this, new SwitchesPolledEventArgs(matrix));
        }
        catch { }
    }

    // ---- Index helpers (ported from TestForm.cs) ----

    /// <summary>
    /// Converts a lamp index to (column, row) for the 8x8 lamp matrix.
    /// Ported from TestForm.IndexToMatrix().
    /// </summary>
    public System.Drawing.Point IndexToMatrix(int index)
    {
        int column, row;
        if (WpcNumbering)
        {
            column = index / 10;
            row = index - column * 10;
        }
        else
        {
            int zeroBasedIndex = index - 1;
            column = zeroBasedIndex / 8 + 1;
            row = zeroBasedIndex % 8 + 1;
        }
        return new System.Drawing.Point(column, row);
    }
}
