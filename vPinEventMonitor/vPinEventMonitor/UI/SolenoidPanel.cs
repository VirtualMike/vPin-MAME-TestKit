namespace vPinEventMonitor.UI;

/// <summary>
/// UserControl showing a live log of solenoid, GI, and B2S backglass events.
/// </summary>
public class SolenoidPanel : UserControl
{
    private const int MaxRows = 500;

    private readonly ListView _listView;

    public SolenoidPanel()
    {
        _listView = new ListView
        {
            Dock      = DockStyle.Fill,
            View      = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font      = new Font("Consolas", 8.5f)
        };

        _listView.Columns.Add("Time",     80);
        _listView.Columns.Add("Type",     70);
        _listView.Columns.Add("Id",       40);
        _listView.Columns.Add("State",    50);
        _listView.Columns.Add("Label",   200);

        Controls.Add(_listView);
    }

    public void AddSolenoidChange(int id, bool on, string label = "")
        => AddRow("Solenoid", id, on ? "ON" : "OFF", label);

    public void AddGiChange(int id, int brightness, string label = "")
        => AddRow("GI", id, brightness.ToString(), label);

    private void AddRow(string type, int id, string state, string label)
    {
        var item = new ListViewItem(DateTime.Now.ToString("HH:mm:ss.fff"));
        item.SubItems.Add(type);
        item.SubItems.Add(id.ToString());
        item.SubItems.Add(state);
        item.SubItems.Add(label);

        _listView.Items.Insert(0, item);

        while (_listView.Items.Count > MaxRows)
            _listView.Items.RemoveAt(_listView.Items.Count - 1);
    }

    public void Clear() => _listView.Items.Clear();
}
