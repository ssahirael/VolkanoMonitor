using SQLite;

namespace VolcanoMonitor.Models;

[Table("Alerts")]
public class Alert
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int VolcanoId { get; set; }

    public string Level { get; set; } = "NORMAL"; // NORMAL, WASPADA, SIAGA, AWAS
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}