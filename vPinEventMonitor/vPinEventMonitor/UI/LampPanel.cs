namespace vPinEventMonitor.UI;

/// <summary>
/// UserControl displaying an 8x8 lamp matrix.
/// Grid layout and indexing ported from VPinMAMETest/TestForm.cs.
/// </summary>
public class LampPanel : UserControl
{
    private const int Columns = 8;
    private const int Rows    = 8;

    private readonly Label[,] _grid = new Label[Columns + 1, Rows + 1];

    private static readonly Color LightOnColor  = Color.DarkOrange;
    private static readonly Color LightOffColor = Color.FromArgb(0x40, 0x30, 0x00);

    public LampPanel()
    {
        const int size   = 40;
        const int margin = 3;

        for (int c = 1; c <= Columns; c++)
        {
            for (int r = 1; r <= Rows; r++)
            {
                var lamp = new Label
                {
                    Width     = size,
                    Height    = size,
                    Text      = $"{c},{r}",
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.White,
                    BackColor = LightOffColor,
                    Top       = (r - 1) * (size + margin),
                    Left      = (c - 1) * (size + margin)
                };

                _grid[c, r] = lamp;
                Controls.Add(lamp);
            }
        }

        BackColor = Color.Black;
        AutoScroll = true;
    }

    /// <summary>
    /// Applies lamp change events to the grid.
    /// </summary>
    /// <param name="changes">Array of (lampId, isOn) pairs.</param>
    /// <param name="indexToMatrix">Converts a lamp index to grid (col, row).</param>
    public void ApplyChanges(
        (int LampId, bool On)[] changes,
        Func<int, System.Drawing.Point> indexToMatrix)
    {
        foreach (var (lampId, on) in changes)
        {
            var pos = indexToMatrix(lampId);
            if (pos.X >= 1 && pos.X <= Columns && pos.Y >= 1 && pos.Y <= Rows)
                _grid[pos.X, pos.Y].BackColor = on ? LightOnColor : LightOffColor;
        }
    }

    /// <summary>Resets all lamps to off.</summary>
    public void Reset()
    {
        for (int c = 1; c <= Columns; c++)
            for (int r = 1; r <= Rows; r++)
                _grid[c, r].BackColor = LightOffColor;
    }
}
