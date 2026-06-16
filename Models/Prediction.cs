using SQLite;

namespace VolcanoMonitor.Models;

[Table("Predictions")]
public class Prediction
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int VolcanoId { get; set; }

    public string NNResult { get; set; } = string.Empty; // e.g. "SIAGA (85% confidence)"
    public double FuzzyRiskIndex { get; set; } // 0 - 100
    public string Explanation { get; set; } = string.Empty; // Layman terms
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}