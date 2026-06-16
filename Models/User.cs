using SQLite;

namespace VolcanoMonitor.Models;

[Table("Users")]
public class User
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true)]
    public string Email { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "user"; // "admin" / "user"
    public bool IsActive { get; set; } = true;
    public byte[]? FaceEmbedding { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}