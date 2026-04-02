using vPinEventMonitor;
using vPinEventMonitor.Pipe;

namespace vPinEventMonitor.UI;

/// <summary>
/// Main application window. Hosts all monitor panels and wires VPinMAMEMonitor events
/// to the UI and to the PEP named pipe server for external clients.
/// </summary>
public class MainForm : Form
{
    // ---- Core objects ----
    private readonly VPinMAMEMonitor _monitor;
    private readonly PipeServer      _pipeServer;
    private AppSettings              _settings;
    private B2sMappingTable?         _b2sMapping;

    private readonly string _settingsPath =
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    // ---- UI Controls ----
    private ToolStrip            _toolStrip       = null!;
    private ToolStripLabel       _lblRom          = null!;
    private ToolStripTextBox     _txtGameName     = null!;
    private ToolStripButton      _btnStart        = null!;
    private ToolStripButton      _btnStop         = null!;
    private ToolStripButton      _btnB2s          = null!;
    private TabControl           _tabControl      = null!;
    private TabPage              _tabDmd          = null!;
    private TabPage              _tabLamps        = null!;
    private TabPage              _tabSwitches     = null!;
    private TabPage              _tabSolenoids    = null!;
    private TabPage              _tabLog          = null!;
    private StatusStrip          _statusStrip     = null!;
    private ToolStripStatusLabel _lblRunning      = null!;
    private ToolStripStatusLabel _lblClientCount  = null!;

    // ---- Panels ----
    private DmdPanel      _dmdPanel      = null!;
    private LampPanel     _lampPanel     = null!;
    private SwitchPanel   _switchPanel   = null!;
    private SolenoidPanel _solenoidPanel = null!;
    private EventLogPanel _eventLogPanel = null!;

    public MainForm()
    {
        _monitor    = new VPinMAMEMonitor();
        _pipeServer = new PipeServer();
        _settings   = new AppSettings();

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        // ---- Panels ----
        _dmdPanel      = new DmdPanel      { Dock = DockStyle.Fill };
        _lampPanel     = new LampPanel     { Dock = DockStyle.Fill };
        _switchPanel   = new SwitchPanel   { Dock = DockStyle.Fill };
        _solenoidPanel = new SolenoidPanel { Dock = DockStyle.Fill };
        _eventLogPanel = new EventLogPanel { Dock = DockStyle.Fill };

        // ---- Tab pages ----
        _tabDmd       = new TabPage("DMD");
        _tabLamps     = new TabPage("Lamps");
        _tabSwitches  = new TabPage("Switches");
        _tabSolenoids = new TabPage("Solenoids / B2S");
        _tabLog       = new TabPage("Event Log");

        _tabDmd.Controls.Add(_dmdPanel);
        _tabLamps.Controls.Add(_lampPanel);
        _tabSwitches.Controls.Add(_switchPanel);
        _tabSolenoids.Controls.Add(_solenoidPanel);
        _tabLog.Controls.Add(_eventLogPanel);

        // ---- Tab control ----
        _tabControl = new TabControl { Dock = DockStyle.Fill };
        _tabControl.Controls.AddRange(new TabPage[]
            { _tabDmd, _tabLamps, _tabSwitches, _tabSolenoids, _tabLog });

        // ---- Tool strip ----
        _lblRom      = new ToolStripLabel("ROM:");
        _txtGameName = new ToolStripTextBox { Size = new Size(120, 23), Text = "ft_l5" };
        _btnStart    = new ToolStripButton("Start") { ForeColor = Color.DarkGreen };
        _btnStop     = new ToolStripButton("Stop")  { ForeColor = Color.DarkRed, Enabled = false };
        _btnB2s      = new ToolStripButton("Load .b2s…");

        _toolStrip = new ToolStrip();
        _toolStrip.Items.AddRange(new ToolStripItem[]
            { _lblRom, _txtGameName, new ToolStripSeparator(),
              _btnStart, _btnStop, new ToolStripSeparator(), _btnB2s });

        // ---- Status strip ----
        _lblRunning     = new ToolStripStatusLabel("Stopped") { Spring = false };
        _lblClientCount = new ToolStripStatusLabel("Pipe clients: 0") { Spring = true, TextAlign = ContentAlignment.MiddleRight };
        _statusStrip = new StatusStrip();
        _statusStrip.Items.AddRange(new ToolStripItem[] { _lblRunning, _lblClientCount });

        // ---- Event wiring ----
        _btnStart.Click += BtnStart_Click;
        _btnStop.Click  += BtnStop_Click;
        _btnB2s.Click   += BtnB2s_Click;

        // ---- Form ----
        SuspendLayout();
        Text        = "vPin Event Monitor";
        Size        = new Size(1000, 750);
        MinimumSize = new Size(700, 500);

        Controls.Add(_tabControl);
        Controls.Add(_toolStrip);
        Controls.Add(_statusStrip);

        Load        += MainForm_Load;
        FormClosed  += MainForm_FormClosed;
        ResumeLayout();
    }

    // ---- Form lifecycle ----

    private void MainForm_Load(object? sender, EventArgs e)
    {
        _settings = AppSettings.Load(_settingsPath);
        _txtGameName.Text = _settings.GameName;

        // Load B2S mapping if configured
        if (!string.IsNullOrEmpty(_settings.B2sFilePath))
            LoadB2sMapping(_settings.B2sFilePath);

        // Wire monitor events
        _monitor.LampsChanged     += Monitor_LampsChanged;
        _monitor.SolenoidsChanged += Monitor_SolenoidsChanged;
        _monitor.GIsChanged       += Monitor_GIsChanged;
        _monitor.DmdFrame         += Monitor_DmdFrame;
        _monitor.SwitchesPoll     += Monitor_SwitchesPoll;
        _monitor.StatusChanged    += Monitor_StatusChanged;

        // Wire pipe server events
        _pipeServer.ClientConnected    += PipeServer_ClientConnected;
        _pipeServer.ClientDisconnected += PipeServer_ClientDisconnected;
        _pipeServer.MessageReceived    += PipeServer_MessageReceived;

        // Start pipe server
        try
        {
            _pipeServer.Start(_settings.PipeName);
            _eventLogPanel.Append($"Pipe server listening on {_settings.PipeName}");
        }
        catch (Exception ex)
        {
            _eventLogPanel.Append($"Pipe server error: {ex.Message}");
        }

        if (_settings.AutoStart)
            StartMonitor();
    }

    private void MainForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        _monitor.Stop();
        _monitor.Dispose();

        _settings.GameName = _txtGameName.Text;
        _settings.Save(_settingsPath);
    }

    // ---- Toolbar buttons ----

    private void BtnStart_Click(object? sender, EventArgs e) => StartMonitor();

    private void BtnStop_Click(object? sender, EventArgs e)
    {
        _monitor.Stop();
        _btnStart.Enabled = true;
        _btnStop.Enabled  = false;
        _lampPanel.Reset();
        _switchPanel.Reset();
    }

    private void BtnB2s_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Select .b2s backglass file",
            Filter = "B2S Backglass (*.b2s)|*.b2s|All files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _settings.B2sFilePath = dlg.FileName;
            LoadB2sMapping(dlg.FileName);
        }
    }

    private void StartMonitor()
    {
        string gameName = _txtGameName.Text.Trim();
        if (string.IsNullOrEmpty(gameName)) return;

        _settings.GameName = gameName;
        _btnStart.Enabled  = false;
        _btnStop.Enabled   = true;
        _lampPanel.Reset();
        _switchPanel.Reset();
        _solenoidPanel.Clear();

        _monitor.Start(gameName, Handle);
    }

    private void LoadB2sMapping(string path)
    {
        _b2sMapping = B2sMappingTable.TryLoad(path);
        if (_b2sMapping != null)
            _eventLogPanel.Append($"Loaded B2S mapping from {Path.GetFileName(path)}");
        else
            _eventLogPanel.Append($"Could not load B2S mapping from {path}");
    }

    // ---- VPinMAMEMonitor event handlers (all on UI thread) ----

    private void Monitor_LampsChanged(object? sender, LampsChangedEventArgs e)
    {
        _lampPanel.ApplyChanges(e.Changes, _monitor.IndexToMatrix);
        BroadcastLamps(e.Changes);

        foreach (var (id, on) in e.Changes)
            _eventLogPanel.Append($"Lamp {id}: {(on ? "ON" : "OFF")}");
    }

    private void Monitor_SolenoidsChanged(object? sender, SolenoidsChangedEventArgs e)
    {
        foreach (var (id, on) in e.Changes)
        {
            string label = _b2sMapping?.GetSolenoidLabel(id) ?? "";
            _solenoidPanel.AddSolenoidChange(id, on, label);
            _eventLogPanel.Append($"Solenoid {id}: {(on ? "ON" : "OFF")}{(label.Length > 0 ? $" ({label})" : "")}");
        }

        BroadcastSolenoids(e.Changes);
    }

    private void Monitor_GIsChanged(object? sender, GIsChangedEventArgs e)
    {
        foreach (var (id, brightness) in e.Changes)
        {
            string label = _b2sMapping?.GetGiLabel(id) ?? "";
            _solenoidPanel.AddGiChange(id, brightness, label);
            _eventLogPanel.Append($"GI {id}: {brightness}{(label.Length > 0 ? $" ({label})" : "")}");
        }

        BroadcastGIs(e.Changes);
    }

    private void Monitor_DmdFrame(object? sender, DmdFrameEventArgs e)
    {
        _dmdPanel.UpdateFrame(e.Pixels);
        BroadcastDmdFrame(e.Pixels);
    }

    private void Monitor_SwitchesPoll(object? sender, SwitchesPolledEventArgs e)
    {
        _switchPanel.ApplySnapshot(e.Matrix);
    }

    private void Monitor_StatusChanged(object? sender, string msg)
    {
        _lblRunning.Text = msg;
        _eventLogPanel.Append($"[STATUS] {msg}");
    }

    // ---- Pipe server event handlers ----

    private void PipeServer_ClientConnected()
    {
        int count = _pipeServer.TotalConnectedClients;
        SafeInvoke(() =>
        {
            _lblClientCount.Text = $"Pipe clients: {count}";
            _eventLogPanel.Append($"Pipe client connected ({count} total)");
        });
    }

    private void PipeServer_ClientDisconnected()
    {
        int count = _pipeServer.TotalConnectedClients;
        SafeInvoke(() =>
        {
            _lblClientCount.Text = $"Pipe clients: {count}";
            _eventLogPanel.Append($"Pipe client disconnected ({count} remaining)");
        });
    }

    private void PipeServer_MessageReceived(int eventType, byte[] message)
    {
        // Switch events from external clients (e.g. table script via PEP)
        if (eventType == PipeEventCodes.Switches && message.Length >= 2)
        {
            int switchId = message[0];
            bool on      = message[1] != 0;
            SafeInvoke(() =>
            {
                _monitor.SetSwitch(switchId, on);
                _eventLogPanel.Append($"[PEP IN] Switch {switchId}: {(on ? "ON" : "OFF")}");
            });
        }
    }

    /// <summary>Marshals an action to the UI thread; safe to call from any thread.</summary>
    private void SafeInvoke(Action action)
    {
        try
        {
            if (IsHandleCreated && !IsDisposed)
                Invoke(action);
        }
        catch { }
    }

    // ---- Pipe broadcasting helpers ----

    private void BroadcastDmdFrame(byte[] pixels)
    {
        if (_pipeServer.TotalConnectedClients == 0) return;
        try { _pipeServer.SendMessage(PipeEventCodes.DmdFrame, pixels); } catch { }
    }

    private void BroadcastLamps((int LampId, bool On)[] changes)
    {
        if (_pipeServer.TotalConnectedClients == 0) return;
        var data = new byte[changes.Length * 2];
        for (int i = 0; i < changes.Length; i++)
        {
            data[i * 2]     = (byte)changes[i].LampId;
            data[i * 2 + 1] = changes[i].On ? (byte)1 : (byte)0;
        }
        try { _pipeServer.SendMessage(PipeEventCodes.Lamps, data); } catch { }
    }

    private void BroadcastSolenoids((int SolenoidId, bool On)[] changes)
    {
        if (_pipeServer.TotalConnectedClients == 0) return;
        var data = new byte[changes.Length * 2];
        for (int i = 0; i < changes.Length; i++)
        {
            data[i * 2]     = (byte)changes[i].SolenoidId;
            data[i * 2 + 1] = changes[i].On ? (byte)1 : (byte)0;
        }
        try { _pipeServer.SendMessage(PipeEventCodes.Solenoids, data); } catch { }
    }

    private void BroadcastGIs((int GiId, int Brightness)[] changes)
    {
        if (_pipeServer.TotalConnectedClients == 0) return;
        var data = new byte[changes.Length * 2];
        for (int i = 0; i < changes.Length; i++)
        {
            data[i * 2]     = (byte)changes[i].GiId;
            data[i * 2 + 1] = (byte)Math.Min(255, changes[i].Brightness);
        }
        try { _pipeServer.SendMessage(PipeEventCodes.GIs, data); } catch { }
    }
}
