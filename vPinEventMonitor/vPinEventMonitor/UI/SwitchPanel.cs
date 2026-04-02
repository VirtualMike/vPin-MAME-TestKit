namespace vPinEventMonitor.UI;

/// <summary>
/// UserControl displaying an 11x8 switch matrix (display-only).
/// Grid layout ported from VPinMAMETest/TestForm.cs.
/// </summary>
public class SwitchPanel : UserControl
{
    private const int Columns = 11;
    private const int Rows    = 8;

    private readonly CheckBox[,] _grid = new CheckBox[Columns + 1, Rows + 1];

    public SwitchPanel()
    {
        const int size   = 40;
        const int margin = 3;

        for (int c = 0; c <= Columns; c++)
        {
            for (int r = 1; r <= Rows; r++)
            {
                var cb = new CheckBox
                {
                    Appearance = Appearance.Button,
                    Width      = size,
                    Height     = size,
                    Text       = $"{c},{r}",
                    Top        = (r - 1) * (size + margin),
                    Left       = c * (size + margin),
                    Enabled    = false  // display-only
                };

                _grid[c, r] = cb;
                Controls.Add(cb);
            }
        }

        BackColor = SystemColors.Control;
        AutoScroll = true;
    }

    /// <summary>
    /// Applies a full switch matrix snapshot.
    /// </summary>
    /// <param name="matrix">Boolean matrix [col 0..11, row 1..8].</param>
    public void ApplySnapshot(bool[,] matrix)
    {
        for (int c = 0; c <= Columns; c++)
            for (int r = 1; r <= Rows; r++)
                _grid[c, r].Checked = matrix[c, r];
    }

    /// <summary>Resets all switches to unchecked.</summary>
    public void Reset()
    {
        for (int c = 0; c <= Columns; c++)
            for (int r = 1; r <= Rows; r++)
                _grid[c, r].Checked = false;
    }
}
