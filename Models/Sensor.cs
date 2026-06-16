using SQLite;

namespace VolcanoMonitor.Models;

[Table("Sensors")]
public class Sensor
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int VolcanoId { get; set; }

    public string Type { get; set; } = string.Empty; // SO2, CO2, H2S, TEMPERATURE, SEISMIC
    public string Unit { get; set; } = string.Empty; // ppm, °C, index
}