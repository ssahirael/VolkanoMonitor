using SQLite;

namespace VolcanoMonitor.Models;

[Table("Readings")]
public class Reading
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SensorId { get; set; }

    public double Value { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}