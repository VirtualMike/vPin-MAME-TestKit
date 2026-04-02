namespace vPinEventMonitor.UI;

/// <summary>
/// UserControl that displays a DMD (Dot Matrix Display) frame.
/// </summary>
public class DmdPanel : UserControl
{
    private readonly PictureBox _pictureBox;
    private readonly DmdBitmap  _dmdBitmap;

    public DmdPanel()
    {
        _dmdBitmap = new DmdBitmap(128, 32);

        _pictureBox = new PictureBox
        {
            Width  = 256,
            Height = 64,
            Top    = 10,
            Left   = 10,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom
        };

        Controls.Add(_pictureBox);
        BackColor = Color.Black;
        AutoScroll = true;
    }

    /// <summary>Updates the displayed DMD frame. Must be called on UI thread.</summary>
    public void UpdateFrame(byte[] pixels)
    {
        _dmdBitmap.UpdateBitmap(pixels);
        _pictureBox.Image = _dmdBitmap.Bitmap;
    }

    /// <summary>Changes the LED color used for rendering.</summary>
    public void SetLedColor(Color color) => _dmdBitmap.LedColor = color;
}
