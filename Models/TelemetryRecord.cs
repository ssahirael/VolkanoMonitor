using System.ComponentModel.DataAnnotations.Schema;
using System.Data.SqlTypes;
using SQLite;

namespace VolcanoMonitor.Models;

[SQLite.Table("TelemetryRecords")]
public class TelemetryRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string VolcanoName { get; set; } = "Merapi";
    public double SO2_ppm { get; set; }
    public double CO2_ppm { get; set; }
    public double Temperature_C { get; set; }
    public string AlertLevel { get; set; } = "Normal"; // Normal, Waspada, Siaga, Awas
    public string PhotoPath { get; set; } = "";
    public DateTime RecordedAt { get; set; } = DateTime.Now;

    // Display helper
    public string Summary =>
        $"[{RecordedAt:HH:mm:ss}] SO₂:{SO2_ppm:F1}ppm | CO₂:{CO2_ppm:F1}ppm | {Temperature_C:F1}°C | {AlertLevel}";
}