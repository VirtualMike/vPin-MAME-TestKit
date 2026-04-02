namespace vPinEventMonitor.UI;

/// <summary>
/// UserControl showing a timestamped event log for all pinball events.
/// </summary>
public class EventLogPanel : UserControl
{
    private const int MaxLines = 2000;

    private readonly TextBox _log;
    private readonly Button  _btnClear;

    public EventLogPanel()
    {
        _log = new TextBox
        {
            Multiline   = true,
            ReadOnly    = true,
            ScrollBars  = ScrollBars.Vertical,
            Dock        = DockStyle.Fill,
            Font        = new Font("Consolas", 8.5f),
            BackColor   = Color.Black,
            ForeColor   = Color.LimeGreen,
            WordWrap    = false
        };

        _btnClear = new Button
        {
            Text   = "Clear",
            Dock   = DockStyle.Bottom,
            Height = 28
        };
        _btnClear.Click += (_, _) => _log.Clear();

        Controls.Add(_log);
        Controls.Add(_btnClear);
    }

    /// <summary>Appends a timestamped line to the log.</summary>
    public void Append(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}";
        _log.AppendText(line);

        // Trim oldest lines when over the limit
        int lineCount = _log.Lines.Length;
        if (lineCount > MaxLines)
        {
            // Keep newest MaxLines/2 lines to avoid trimming too frequently
            int keep = MaxLines / 2;
            var lines = _log.Lines;
            _log.Lines = lines[(lineCount - keep)..];
        }
    }

    public void Clear() => _log.Clear();
}
