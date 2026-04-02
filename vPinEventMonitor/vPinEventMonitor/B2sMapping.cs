using System.Xml.Linq;

namespace vPinEventMonitor;

/// <summary>
/// Optionally parses a .b2s backglass file to map solenoid/GI IDs to named labels.
/// B2S Server reads the same VPinMAME solenoid/GI data; we monitor from the same source.
/// </summary>
public class B2sMappingTable
{
    private readonly Dictionary<int, string> _solenoids = new();
    private readonly Dictionary<int, string> _gis       = new();
    private readonly Dictionary<int, string> _lamps     = new();

    private B2sMappingTable() { }

    /// <summary>
    /// Attempts to parse a .b2s XML file for illumination element labels.
    /// Returns null if the file cannot be read or has no illumination data.
    /// </summary>
    public static B2sMappingTable? TryLoad(string b2sFilePath)
    {
        if (string.IsNullOrEmpty(b2sFilePath) || !File.Exists(b2sFilePath))
            return null;

        try
        {
            var doc = XDocument.Load(b2sFilePath);
            var table = new B2sMappingTable();

            // B2S illumination elements live under /DirectB2SData/Illumination/Bulb
            var bulbs = doc.Descendants("Bulb");
            foreach (var bulb in bulbs)
            {
                string type  = bulb.Attribute("Type")?.Value ?? "";
                string idStr = bulb.Attribute("ID")?.Value ?? "";
                string name  = bulb.Attribute("Name")?.Value ?? "";

                if (!int.TryParse(idStr, out int id) || string.IsNullOrEmpty(name))
                    continue;

                switch (type)
                {
                    case "Solenoid": table._solenoids[id] = name; break;
                    case "GI":       table._gis[id]       = name; break;
                    case "Lamp":     table._lamps[id]     = name; break;
                }
            }

            return table;
        }
        catch
        {
            return null;
        }
    }

    public string GetSolenoidLabel(int id)
        => _solenoids.TryGetValue(id, out var name) ? name : "";

    public string GetGiLabel(int id)
        => _gis.TryGetValue(id, out var name) ? name : "";

    public string GetLampLabel(int id)
        => _lamps.TryGetValue(id, out var name) ? name : "";
}
