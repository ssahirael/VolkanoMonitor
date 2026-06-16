using SQLite;
using VolcanoMonitor.Models;

namespace VolcanoMonitor.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection? _db;

    public async Task InitAsync()
    {
        if (_db != null) return;

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "volcano.db3");
        _db = new SQLiteAsyncConnection(dbPath);

        // Create tables
        await _db.CreateTableAsync<User>();
        await _db.CreateTableAsync<Volcano>();
        await _db.CreateTableAsync<Sensor>();
        await _db.CreateTableAsync<Reading>();
        await _db.CreateTableAsync<Alert>();
        await _db.CreateTableAsync<Prediction>();
        await _db.CreateTableAsync<ChatLog>();

        // Seed data if empty
        var userCount = await _db.Table<User>().CountAsync();
        if (userCount == 0)
        {
            await SeedDataAsync();
        }
    }

    private async Task SeedDataAsync()
    {
        if (_db == null) return;

        // 1. Users
        var adminPassword = HashHelper.ComputeSha256Hash("admin123");
        var userPassword = HashHelper.ComputeSha256Hash("user123");

        var adminUser = new User
        {
            Name = "Petugas Pos Pemantau",
            Email = "admin@vulkano.com",
            PasswordHash = adminPassword,
            Role = "admin",
            IsActive = true
        };
        var normalUser = new User
        {
            Name = "Warga Sipil Merapi",
            Email = "user@vulkano.com",
            PasswordHash = userPassword,
            Role = "user",
            IsActive = true
        };

        await _db.InsertAsync(adminUser);
        await _db.InsertAsync(normalUser);

        // 2. Volcanoes
        var v1 = new Volcano
        {
            Name = "Merapi",
            Location = "Sleman, Yogyakarta & Klaten, Jawa Tengah",
            Latitude = -7.5407,
            Longitude = 110.4470,
            Elevation = 2910,
            Description = "Gunung berapi teraktif di Indonesia. Mengalami erupsi efusif berkala dengan bahaya kubah lava runtuh dan awan panas (wedhus gembel).",
            CurrentStatus = "SIAGA"
        };
        var v2 = new Volcano
        {
            Name = "Sinabung",
            Location = "Karo, Sumatera Utara",
            Latitude = 3.1700,
            Longitude = 98.3920,
            Elevation = 2460,
            Description = "Gunung berapi tipe strato yang tertidur sejak tahun 1600-an dan aktif kembali secara eksplosif sejak tahun 2010 hingga sekarang.",
            CurrentStatus = "WASPADA"
        };
        var v3 = new Volcano
        {
            Name = "Anak Krakatau",
            Location = "Selat Sunda, Lampung",
            Latitude = -6.1020,
            Longitude = 105.4230,
            Elevation = 230,
            Description = "Tumbuh di kaldera pasca-letusan hebat Krakatau 1883. Menunjukkan aktivitas kegempaan vulkanik dalam dan hembusan abu vulkanik pekat.",
            CurrentStatus = "AWAS"
        };
        var v4 = new Volcano
        {
            Name = "Semeru",
            Location = "Lumajang & Malang, Jawa Timur",
            Latitude = -8.1080,
            Longitude = 112.9224,
            Elevation = 3676,
            Description = "Gunung tertinggi di Pulau Jawa (Mahameru). Terkenal dengan hembusan abu kawah (Jonggring Saloko) yang terjadi berkala setiap 15-30 menit.",
            CurrentStatus = "NORMAL"
        };

        await _db.InsertAsync(v1);
        await _db.InsertAsync(v2);
        await _db.InsertAsync(v3);
        await _db.InsertAsync(v4);

        // 3. Sensors and Readings (Simulated historical data for the last 10 hours)
        var volcanoes = new[] { v1, v2, v3, v4 };
        var rand = new Random();

        foreach (var v in volcanoes)
        {
            // Create 5 sensors per volcano
            var sSO2 = new Sensor { VolcanoId = v.Id, Type = "SO2", Unit = "ppm" };
            var sCO2 = new Sensor { VolcanoId = v.Id, Type = "CO2", Unit = "ppm" };
            var sH2S = new Sensor { VolcanoId = v.Id, Type = "H2S", Unit = "ppm" };
            var sTemp = new Sensor { VolcanoId = v.Id, Type = "TEMPERATURE", Unit = "°C" };
            var sSeismic = new Sensor { VolcanoId = v.Id, Type = "SEISMIC", Unit = "index" };

            await _db.InsertAsync(sSO2);
            await _db.InsertAsync(sCO2);
            await _db.InsertAsync(sH2S);
            await _db.InsertAsync(sTemp);
            await _db.InsertAsync(sSeismic);

            // Seed historical readings (10 readings back in time)
            double baseSO2 = v.CurrentStatus switch { "NORMAL" => 1.0, "WASPADA" => 7.0, "SIAGA" => 22.0, _ => 45.0 };
            double baseCO2 = v.CurrentStatus switch { "NORMAL" => 380.0, "WASPADA" => 550.0, "SIAGA" => 1100.0, _ => 1800.0 };
            double baseH2S = v.CurrentStatus switch { "NORMAL" => 0.2, "WASPADA" => 2.5, "SIAGA" => 7.0, _ => 15.0 };
            double baseTemp = v.CurrentStatus switch { "NORMAL" => 80.0, "WASPADA" => 150.0, "SIAGA" => 320.0, _ => 700.0 };
            double baseSeismic = v.CurrentStatus switch { "NORMAL" => 1.0, "WASPADA" => 3.0, "SIAGA" => 6.0, _ => 9.5 };

            for (int i = 9; i >= 0; i--)
            {
                var time = DateTime.Now.AddHours(-i);

                await _db.InsertAsync(new Reading { SensorId = sSO2.Id, Value = baseSO2 + rand.NextDouble() * (baseSO2 * 0.1) - (baseSO2 * 0.05), Timestamp = time });
                await _db.InsertAsync(new Reading { SensorId = sCO2.Id, Value = baseCO2 + rand.NextDouble() * (baseCO2 * 0.05) - (baseCO2 * 0.025), Timestamp = time });
                await _db.InsertAsync(new Reading { SensorId = sH2S.Id, Value = baseH2S + rand.NextDouble() * (baseH2S * 0.1) - (baseH2S * 0.05), Timestamp = time });
                await _db.InsertAsync(new Reading { SensorId = sTemp.Id, Value = baseTemp + rand.NextDouble() * (baseTemp * 0.08) - (baseTemp * 0.04), Timestamp = time });
                await _db.InsertAsync(new Reading { SensorId = sSeismic.Id, Value = Math.Clamp(baseSeismic + rand.NextDouble() * 1.0 - 0.5, 0, 10), Timestamp = time });
            }
        }

        // 4. Alerts
        await _db.InsertAsync(new Alert
        {
            VolcanoId = v3.Id,
            Level = "AWAS",
            Message = "Status Gunung Anak Krakatau dinaikkan menjadi AWAS (Level IV) dikarenakan tinggi hembusan abu meletus melebihi 2000 meter dan emisi gas SO₂ terpantau sangat tinggi di atas 45 ppm secara konsisten.",
            CreatedAt = DateTime.Now.AddHours(-2)
        });
        await _db.InsertAsync(new Alert
        {
            VolcanoId = v1.Id,
            Level = "SIAGA",
            Message = "Status Gunung Merapi ditetapkan SIAGA (Level III). Aktivitas seismik menunjukkan gempa guguran meningkat tajam. Disarankan menjauh di luar radius aman 5 km dari puncak.",
            CreatedAt = DateTime.Now.AddHours(-5)
        });
    }

    // ===== USER CRUD =====
    public Task<User?> GetUserByEmailAsync(string email) =>
        _db!.Table<User>().Where(u => u.Email == email).FirstOrDefaultAsync()!;

    public Task<List<User>> GetAllUsersAsync() =>
        _db!.Table<User>().ToListAsync();

    public Task<int> InsertUserAsync(User u) => _db!.InsertAsync(u);
    public Task<int> UpdateUserAsync(User u) => _db!.UpdateAsync(u);
    public Task<int> DeleteUserAsync(User u) => _db!.DeleteAsync(u);

    // ===== VOLCANO CRUD =====
    public Task<List<Volcano>> GetAllVolcanoesAsync() =>
        _db!.Table<Volcano>().ToListAsync();

    public Task<Volcano> GetVolcanoByIdAsync(int id) =>
        _db!.Table<Volcano>().Where(v => v.Id == id).FirstAsync();

    public Task<int> InsertVolcanoAsync(Volcano v) => _db!.InsertAsync(v);
    public Task<int> UpdateVolcanoAsync(Volcano v) => _db!.UpdateAsync(v);
    public Task<int> DeleteVolcanoAsync(Volcano v) => _db!.DeleteAsync(v);

    // ===== SENSOR & READING CRUD =====
    public Task<List<Sensor>> GetSensorsForVolcanoAsync(int volcanoId) =>
        _db!.Table<Sensor>().Where(s => s.VolcanoId == volcanoId).ToListAsync();

    public Task<Sensor?> GetSensorByTypeAsync(int volcanoId, string type) =>
        _db!.Table<Sensor>().Where(s => s.VolcanoId == volcanoId && s.Type == type).FirstOrDefaultAsync()!;

    public Task<int> InsertSensorAsync(Sensor s) => _db!.InsertAsync(s);
    public Task<int> InsertReadingAsync(Reading r) => _db!.InsertAsync(r);

    public Task<List<Reading>> GetReadingsForSensorAsync(int sensorId) =>
        _db!.Table<Reading>().Where(r => r.SensorId == sensorId).OrderBy(r => r.Timestamp).ToListAsync();

    public async Task<List<Reading>> GetReadingsForVolcanoAsync(int volcanoId, string sensorType)
    {
        var sensor = await GetSensorByTypeAsync(volcanoId, sensorType);
        if (sensor == null) return new List<Reading>();
        return await GetReadingsForSensorAsync(sensor.Id);
    }

    public async Task<Dictionary<string, double>> GetLatestReadingsForVolcanoAsync(int volcanoId)
    {
        var dict = new Dictionary<string, double>();
        var sensors = await GetSensorsForVolcanoAsync(volcanoId);
        foreach (var s in sensors)
        {
            var latest = await _db!.Table<Reading>()
                                   .Where(r => r.SensorId == s.Id)
                                   .OrderByDescending(r => r.Timestamp)
                                   .FirstOrDefaultAsync();
            if (latest != null)
            {
                dict[s.Type] = latest.Value;
            }
            else
            {
                dict[s.Type] = 0.0;
            }
        }
        return dict;
    }

    // ===== ALERTS =====
    public Task<List<Alert>> GetAllAlertsAsync() =>
        _db!.Table<Alert>().OrderByDescending(a => a.CreatedAt).ToListAsync();

    public Task<int> InsertAlertAsync(Alert a) => _db!.InsertAsync(a);
    public Task<int> DeleteAlertAsync(Alert a) => _db!.DeleteAsync(a);

    // ===== PREDICTIONS =====
    public Task<List<Prediction>> GetPredictionsForVolcanoAsync(int volcanoId) =>
        _db!.Table<Prediction>().Where(p => p.VolcanoId == volcanoId).OrderByDescending(p => p.CreatedAt).ToListAsync();

    public Task<int> SavePredictionAsync(Prediction p) => _db!.InsertAsync(p);

    // ===== CHATLOGS =====
    public Task<List<ChatLog>> GetChatLogsForUserAsync(int userId) =>
        _db!.Table<ChatLog>().Where(c => c.UserId == userId).OrderBy(c => c.CreatedAt).ToListAsync();

    public Task<int> InsertChatLogAsync(ChatLog log) => _db!.InsertAsync(log);
}