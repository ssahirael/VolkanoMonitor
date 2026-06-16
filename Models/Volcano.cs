using SQLite;

namespace VolcanoMonitor.Models;

[Table("Volcanoes")]
public class Volcano
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Elevation { get; set; } // in meters
    public string Description { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = "NORMAL"; // NORMAL, WASPADA, SIAGA, AWAS
}