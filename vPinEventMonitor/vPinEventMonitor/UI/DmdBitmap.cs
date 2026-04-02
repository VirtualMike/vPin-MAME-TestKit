namespace vPinEventMonitor.UI;

/// <summary>
/// Renders a DMD frame into a Bitmap with 2x scaling for the grid effect.
/// Ported from PEP/PipeClient/PipeClient/DmdBitmap.cs (namespace updated).
/// </summary>
public class DmdBitmap
{
    public Bitmap Bitmap { get; }
    public int Width { get; }
    public int Height { get; }
    public Color LedColor { get; set; }

    public DmdBitmap(int width, int height)
    {
        Width = width;
        Height = height;
        Bitmap = new Bitmap(width * 2, height * 2);
        LedColor = Color.FromArgb(0xFF, 0x58, 0x20); // pinball orange
    }

    private static Color DimColor(Color color, float brightness)
    {
        return Color.FromArgb(
            (int)(color.R * brightness),
            (int)(color.G * brightness),
            (int)(color.B * brightness));
    }

    public void UpdateBitmap(byte[] pixels)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            byte pixel = pixels[i];
            int x = i % Width;
            int y = i / Width;

            float intensity = pixel / 100.0f;
            Color pixelColor   = DimColor(LedColor, intensity);
            Color offPixelColor = DimColor(LedColor, intensity * 0.25f);

            Bitmap.SetPixel(x * 2,     y * 2,     pixelColor);
            Bitmap.SetPixel(x * 2 + 1, y * 2 + 1, offPixelColor);
            Bitmap.SetPixel(x * 2,     y * 2 + 1, offPixelColor);
            Bitmap.SetPixel(x * 2 + 1, y * 2,     offPixelColor);
        }
    }
}
