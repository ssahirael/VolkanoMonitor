using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VolcanoMonitor.Models;
using VolcanoMonitor.Services;

namespace VolcanoMonitor.Pages;

public partial class PengaturanPage : ContentPage
{
    private readonly DatabaseService _db;
    private User? _currentUser;
    private List<Volcano> _volcanoes = new();
    private List<User> _users = new();
    private Volcano? _selectedEditVolcano;

    public PengaturanPage(DatabaseService db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!Preferences.Get("session_active", false))
        {
            Shell.Current.FlyoutBehavior = FlyoutBehavior.Disabled;
            await Shell.Current.GoToAsync("//FaceScanPage");
            return;
        }
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Locked;
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        await _db.InitAsync();

        // 1. Get current logged-in user profile
        var email = Preferences.Get("session_nama", ""); // or email
        var role = Preferences.Get("session_role", "user");

        var allUsers = await _db.GetAllUsersAsync();
        _currentUser = allUsers.FirstOrDefault(u => u.Name == email || u.Email == email);
        if (_currentUser == null && allUsers.Count > 0)
        {
            _currentUser = allUsers.First();
        }

        if (_currentUser != null)
        {
            TxtProfileName.Text = _currentUser.Name;
            TxtProfileEmail.Text = _currentUser.Email;
            TxtProfilePassword.Text = string.Empty;
        }

        // 2. Load Admin Panel components if role is admin
        bool isAdmin = role == "admin";
        LblAdminConsoleHeader.IsVisible = isAdmin;
        AdminPanel.IsVisible = isAdmin;

        if (isAdmin)
        {
            _volcanoes = await _db.GetAllVolcanoesAsync();
            _users = allUsers;

            PickerSimulateVolcano.ItemsSource = _volcanoes;
            PickerSimulateVolcano.ItemDisplayBinding = new Binding("Name");

            LoadVolcanoesAdminList();
            LoadUsersAdminList();
            LoadAIThresholds();
        }
    }

    // ===== PROFILE EDIT =====
    private async void OnSaveProfileClicked(object sender, EventArgs e)
    {
        if (_currentUser == null) return;

        var name = TxtProfileName.Text?.Trim();
        var email = TxtProfileEmail.Text?.Trim();
        var newPass = TxtProfilePassword.Text;

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email))
        {
            await DisplayAlert("Validasi", "Nama dan Email wajib diisi.", "OK");
            return;
        }

        _currentUser.Name = name;
        _currentUser.Email = email;

        if (!string.IsNullOrEmpty(newPass))
        {
            _currentUser.PasswordHash = HashHelper.ComputeSha256Hash(newPass);
        }

        await _db.UpdateUserAsync(_currentUser);
        Preferences.Set("session_nama", name);

        TxtProfilePassword.Text = string.Empty;
        await DisplayAlert("Sukses", "Profil akun berhasil diperbarui.", "OK");
    }

    // ===== VOLCANO CRUD =====
    private void LoadVolcanoesAdminList()
    {
        AdminVolcanoList.Children.Clear();
        foreach (var v in _volcanoes)
        {
            var title = new Label
            {
                Text = $"{v.Name.ToUpper()} ({v.CurrentStatus})",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                VerticalOptions = LayoutOptions.Center
            };

            var location = new Label
            {
                Text = $"{v.Location} • Elevasi: {v.Elevation}m",
                FontSize = 10,
                TextColor = Colors.Gray,
                VerticalOptions = LayoutOptions.Center
            };

            var labelsStack = new VerticalStackLayout { Children = { title, location }, VerticalOptions = LayoutOptions.Center };

            var btnEdit = new Button
            {
                Text = "EDIT",
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.LightSkyBlue,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                Padding = new Thickness(6),
                AutomationId = v.Id.ToString()
            };
            btnEdit.Clicked += OnEditVolcanoSelected;

            var btnDelete = new Button
            {
                Text = "HAPUS",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#E10600"),
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                Padding = new Thickness(6),
                AutomationId = v.Id.ToString()
            };
            btnDelete.Clicked += OnDeleteVolcanoSelected;

            var buttonsStack = new HorizontalStackLayout { Children = { btnEdit, btnDelete } };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Padding = new Thickness(10, 6)
            };
            grid.Children.Add(labelsStack);
            Grid.SetColumn(labelsStack, 0);
            grid.Children.Add(buttonsStack);
            Grid.SetColumn(buttonsStack, 1);

            var border = new Border
            {
                BackgroundColor = Color.FromArgb("#0D0D0D"),
                Stroke = new SolidColorBrush(Color.FromArgb("#222")),
                StrokeThickness = 1,
                Content = grid,
                Margin = new Thickness(0, 0, 0, 4)
            };

            AdminVolcanoList.Children.Add(border);
        }
    }

    private void OnEditVolcanoSelected(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.AutomationId != null)
        {
            int id = int.Parse(btn.AutomationId);
            _selectedEditVolcano = _volcanoes.FirstOrDefault(v => v.Id == id);

            if (_selectedEditVolcano != null)
            {
                TxtVolcName.Text = _selectedEditVolcano.Name;
                TxtVolcLocation.Text = _selectedEditVolcano.Location;
                TxtVolcLat.Text = _selectedEditVolcano.Latitude.ToString();
                TxtVolcLong.Text = _selectedEditVolcano.Longitude.ToString();
                TxtVolcElevation.Text = _selectedEditVolcano.Elevation.ToString();
                TxtVolcDesc.Text = _selectedEditVolcano.Description;
                PickerVolcStatus.SelectedItem = _selectedEditVolcano.CurrentStatus;

                BtnUpdateVolcano.IsEnabled = true;
            }
        }
    }

    private async void OnDeleteVolcanoSelected(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.AutomationId != null)
        {
            int id = int.Parse(btn.AutomationId);
            var volcano = _volcanoes.FirstOrDefault(v => v.Id == id);

            if (volcano != null)
            {
                bool confirm = await DisplayAlert("Konfirmasi", $"Hapus data Gunung {volcano.Name} beserta seluruh data sensornya?", "YA", "TIDAK");
                if (confirm)
                {
                    await _db.DeleteVolcanoAsync(volcano);
                    await LoadDataAsync();
                }
            }
        }
    }

    private async void OnAddVolcanoClicked(object sender, EventArgs e)
    {
        var name = TxtVolcName.Text?.Trim();
        var loc = TxtVolcLocation.Text?.Trim();
        var latStr = TxtVolcLat.Text?.Trim();
        var longStr = TxtVolcLong.Text?.Trim();
        var elevStr = TxtVolcElevation.Text?.Trim();
        var desc = TxtVolcDesc.Text?.Trim();
        var status = PickerVolcStatus.SelectedItem as string ?? "NORMAL";

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(loc) ||
            !double.TryParse(latStr, out double lat) ||
            !double.TryParse(longStr, out double lon) ||
            !double.TryParse(elevStr, out double elev))
        {
            await DisplayAlert("Validasi", "Semua field formulir gunung wajib diisi dengan benar.", "OK");
            return;
        }

        var newV = new Volcano
        {
            Name = name,
            Location = loc,
            Latitude = lat,
            Longitude = lon,
            Elevation = elev,
            Description = desc ?? string.Empty,
            CurrentStatus = status
        };

        await _db.InsertVolcanoAsync(newV);

        // Seed default sensors for the new volcano
        await _db.InsertSensorAsync(new Sensor { VolcanoId = newV.Id, Type = "SO2", Unit = "ppm" });
        await _db.InsertSensorAsync(new Sensor { VolcanoId = newV.Id, Type = "CO2", Unit = "ppm" });
        await _db.InsertSensorAsync(new Sensor { VolcanoId = newV.Id, Type = "H2S", Unit = "ppm" });
        await _db.InsertSensorAsync(new Sensor { VolcanoId = newV.Id, Type = "TEMPERATURE", Unit = "°C" });
        await _db.InsertSensorAsync(new Sensor { VolcanoId = newV.Id, Type = "SEISMIC", Unit = "index" });

        ClearVolcanoForm();
        await LoadDataAsync();
        await DisplayAlert("Sukses", $"Gunung {name} berhasil ditambahkan ke database.", "OK");
    }

    private async void OnUpdateVolcanoClicked(object sender, EventArgs e)
    {
        if (_selectedEditVolcano == null) return;

        var name = TxtVolcName.Text?.Trim();
        var loc = TxtVolcLocation.Text?.Trim();
        var latStr = TxtVolcLat.Text?.Trim();
        var longStr = TxtVolcLong.Text?.Trim();
        var elevStr = TxtVolcElevation.Text?.Trim();
        var desc = TxtVolcDesc.Text?.Trim();
        var status = PickerVolcStatus.SelectedItem as string ?? "NORMAL";

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(loc) ||
            !double.TryParse(latStr, out double lat) ||
            !double.TryParse(longStr, out double lon) ||
            !double.TryParse(elevStr, out double elev))
        {
            await DisplayAlert("Validasi", "Semua field formulir wajib diisi dengan benar.", "OK");
            return;
        }

        _selectedEditVolcano.Name = name;
        _selectedEditVolcano.Location = loc;
        _selectedEditVolcano.Latitude = lat;
        _selectedEditVolcano.Longitude = lon;
        _selectedEditVolcano.Elevation = elev;
        _selectedEditVolcano.Description = desc ?? string.Empty;
        _selectedEditVolcano.CurrentStatus = status;

        await _db.UpdateVolcanoAsync(_selectedEditVolcano);

        ClearVolcanoForm();
        await LoadDataAsync();
        await DisplayAlert("Sukses", $"Data Gunung {name} berhasil diperbarui.", "OK");
    }

    private void ClearVolcanoForm()
    {
        TxtVolcName.Text = string.Empty;
        TxtVolcLocation.Text = string.Empty;
        TxtVolcLat.Text = string.Empty;
        TxtVolcLong.Text = string.Empty;
        TxtVolcElevation.Text = string.Empty;
        TxtVolcDesc.Text = string.Empty;
        PickerVolcStatus.SelectedIndex = -1;

        _selectedEditVolcano = null;
        BtnUpdateVolcano.IsEnabled = false;
    }

    // ===== SENSORS & MANUAL INPUTS =====
    private async void OnSimulateVolcanoChanged(object sender, EventArgs e)
    {
        var selected = PickerSimulateVolcano.SelectedItem as Volcano;
        if (selected == null) return;

        var readings = await _db.GetLatestReadingsForVolcanoAsync(selected.Id);
        TxtSensorSO2.Text = readings.GetValueOrDefault("SO2", 0).ToString("F1");
        TxtSensorCO2.Text = readings.GetValueOrDefault("CO2", 0).ToString("F0");
        TxtSensorH2S.Text = readings.GetValueOrDefault("H2S", 0).ToString("F1");
        TxtSensorTemp.Text = readings.GetValueOrDefault("TEMPERATURE", 0).ToString("F1");
        TxtSensorSeismic.Text = readings.GetValueOrDefault("SEISMIC", 0).ToString("F1");
    }

    private async void OnManualSensorSaveClicked(object sender, EventArgs e)
    {
        var selected = PickerSimulateVolcano.SelectedItem as Volcano;
        if (selected == null)
        {
            await DisplayAlert("Error", "Pilih gunung terlebih dahulu.", "OK");
            return;
        }

        if (!double.TryParse(TxtSensorSO2.Text, out double so2) ||
            !double.TryParse(TxtSensorCO2.Text, out double co2) ||
            !double.TryParse(TxtSensorH2S.Text, out double h2s) ||
            !double.TryParse(TxtSensorTemp.Text, out double temp) ||
            !double.TryParse(TxtSensorSeismic.Text, out double seismic))
        {
            await DisplayAlert("Validasi", "Masukkan angka sensor yang valid.", "OK");
            return;
        }

        await SaveSensorReadingsAsync(selected.Id, so2, co2, h2s, temp, seismic);
        await DisplayAlert("Sukses", "Data sensor manual berhasil ditambahkan.", "OK");
    }

    private async void OnTriggerAutoSimulationClicked(object sender, EventArgs e)
    {
        var rand = new Random();
        foreach (var v in _volcanoes)
        {
            var readings = await _db.GetLatestReadingsForVolcanoAsync(v.Id);
            double baseSO2 = readings.GetValueOrDefault("SO2", 1.0);
            double baseCO2 = readings.GetValueOrDefault("CO2", 380.0);
            double baseH2S = readings.GetValueOrDefault("H2S", 0.5);
            double baseTemp = readings.GetValueOrDefault("TEMPERATURE", 80.0);
            double baseSeismic = readings.GetValueOrDefault("SEISMIC", 1.0);

            // Add small perturbations
            double newSO2 = Math.Max(0.1, baseSO2 + rand.NextDouble() * 4.0 - 2.0);
            double newCO2 = Math.Max(300.0, baseCO2 + rand.NextDouble() * 50.0 - 25.0);
            double newH2S = Math.Max(0.05, baseH2S + rand.NextDouble() * 1.2 - 0.6);
            double newTemp = Math.Max(40.0, baseTemp + rand.NextDouble() * 20.0 - 10.0);
            double newSeismic = Math.Clamp(baseSeismic + rand.NextDouble() * 1.5 - 0.75, 0, 10);

            await SaveSensorReadingsAsync(v.Id, newSO2, newCO2, newH2S, newTemp, newSeismic);
        }

        if (PickerSimulateVolcano.SelectedIndex >= 0)
        {
            OnSimulateVolcanoChanged(this, EventArgs.Empty);
        }

        await DisplayAlert("Auto Simulasi", "Berhasil menyimulasikan telemetri baru untuk semua gunung.", "OK");
    }

    private async Task SaveSensorReadingsAsync(int volcanoId, double so2, double co2, double h2s, double temp, double seismic)
    {
        var time = DateTime.Now;

        // Resolve or create sensors
        var sSO2 = await GetOrCreateSensorAsync(volcanoId, "SO2", "ppm");
        var sCO2 = await GetOrCreateSensorAsync(volcanoId, "CO2", "ppm");
        var sH2S = await GetOrCreateSensorAsync(volcanoId, "H2S", "ppm");
        var sTemp = await GetOrCreateSensorAsync(volcanoId, "TEMPERATURE", "°C");
        var sSeismic = await GetOrCreateSensorAsync(volcanoId, "SEISMIC", "index");

        await _db.InsertReadingAsync(new Reading { SensorId = sSO2.Id, Value = so2, Timestamp = time });
        await _db.InsertReadingAsync(new Reading { SensorId = sCO2.Id, Value = co2, Timestamp = time });
        await _db.InsertReadingAsync(new Reading { SensorId = sH2S.Id, Value = h2s, Timestamp = time });
        await _db.InsertReadingAsync(new Reading { SensorId = sTemp.Id, Value = temp, Timestamp = time });
        await _db.InsertReadingAsync(new Reading { SensorId = sSeismic.Id, Value = seismic, Timestamp = time });
    }

    private async Task<Sensor> GetOrCreateSensorAsync(int volcanoId, string type, string unit)
    {
        var sensor = await _db.GetSensorByTypeAsync(volcanoId, type);
        if (sensor == null)
        {
            sensor = new Sensor { VolcanoId = volcanoId, Type = type, Unit = unit };
            await _db.InsertSensorAsync(sensor);
        }
        return sensor;
    }

    // ===== USER MANAGEMENT =====
    private void LoadUsersAdminList()
    {
        AdminUserListContainer.Children.Clear();
        foreach (var u in _users)
        {
            var nameLabel = new Label
            {
                Text = $"{u.Name.ToUpper()} ({u.Role.ToUpper()})",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White
            };

            var emailLabel = new Label
            {
                Text = $"{u.Email} • Aktif: {(u.IsActive ? "YA" : "TIDAK")}",
                FontSize = 10,
                TextColor = u.IsActive ? Colors.Green : Colors.Red
            };

            var labelsStack = new VerticalStackLayout { Children = { nameLabel, emailLabel }, VerticalOptions = LayoutOptions.Center };

            var btnRole = new Button
            {
                Text = "ROLE",
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.Gold,
                FontSize = 9,
                FontAttributes = FontAttributes.Bold,
                Padding = new Thickness(4),
                AutomationId = u.Id.ToString()
            };
            btnRole.Clicked += OnToggleUserRoleClicked;

            var btnActive = new Button
            {
                Text = "STATUS",
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.LightGreen,
                FontSize = 9,
                FontAttributes = FontAttributes.Bold,
                Padding = new Thickness(4),
                AutomationId = u.Id.ToString()
            };
            btnActive.Clicked += OnToggleUserStatusClicked;

            var btnDelete = new Button
            {
                Text = "HAPUS",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#E10600"),
                FontSize = 9,
                FontAttributes = FontAttributes.Bold,
                Padding = new Thickness(4),
                AutomationId = u.Id.ToString()
            };
            btnDelete.Clicked += OnDeleteUserClicked;

            var buttonsStack = new HorizontalStackLayout { Children = { btnRole, btnActive, btnDelete }, Spacing = 2 };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Padding = new Thickness(10, 6)
            };
            grid.Children.Add(labelsStack);
            Grid.SetColumn(labelsStack, 0);
            grid.Children.Add(buttonsStack);
            Grid.SetColumn(buttonsStack, 1);

            var border = new Border
            {
                BackgroundColor = Color.FromArgb("#0D0D0D"),
                Stroke = new SolidColorBrush(Color.FromArgb("#222")),
                StrokeThickness = 1,
                Content = grid,
                Margin = new Thickness(0, 0, 0, 4)
            };

            AdminUserListContainer.Children.Add(border);
        }
    }

    private async void OnToggleUserRoleClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.AutomationId != null)
        {
            int id = int.Parse(btn.AutomationId);
            var user = _users.FirstOrDefault(u => u.Id == id);
            if (user != null)
            {
                if (user.Id == _currentUser?.Id)
                {
                    await DisplayAlert("Ditolak", "Anda tidak dapat mengubah role Anda sendiri.", "OK");
                    return;
                }
                user.Role = user.Role == "admin" ? "user" : "admin";
                await _db.UpdateUserAsync(user);
                await LoadDataAsync();
            }
        }
    }

    private async void OnToggleUserStatusClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.AutomationId != null)
        {
            int id = int.Parse(btn.AutomationId);
            var user = _users.FirstOrDefault(u => u.Id == id);
            if (user != null)
            {
                if (user.Id == _currentUser?.Id)
                {
                    await DisplayAlert("Ditolak", "Anda tidak dapat menonaktifkan akun Anda sendiri.", "OK");
                    return;
                }
                user.IsActive = !user.IsActive;
                await _db.UpdateUserAsync(user);
                await LoadDataAsync();
            }
        }
    }

    private async void OnDeleteUserClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.AutomationId != null)
        {
            int id = int.Parse(btn.AutomationId);
            var user = _users.FirstOrDefault(u => u.Id == id);
            if (user != null)
            {
                if (user.Id == _currentUser?.Id)
                {
                    await DisplayAlert("Ditolak", "Anda tidak dapat menghapus akun Anda sendiri.", "OK");
                    return;
                }
                bool confirm = await DisplayAlert("Konfirmasi", $"Hapus permanen akun {user.Name}?", "YA", "TIDAK");
                if (confirm)
                {
                    await _db.DeleteUserAsync(user);
                    await LoadDataAsync();
                }
            }
        }
    }

    // ===== THRESHOLDS CONFIG =====
    private void LoadAIThresholds()
    {
        TxtThresholdSO2.Text = Preferences.Get("threshold_so2", 15.0).ToString("F1");
        TxtThresholdTemp.Text = Preferences.Get("threshold_temp", 320.0).ToString("F1");
    }

    private async void OnSaveAIConfigClicked(object sender, EventArgs e)
    {
        if (double.TryParse(TxtThresholdSO2.Text, out double so2) &&
            double.TryParse(TxtThresholdTemp.Text, out double temp))
        {
            Preferences.Set("threshold_so2", so2);
            Preferences.Set("threshold_temp", temp);
            await DisplayAlert("Model AI", "Parameter ambang batas model kecerdasan buatan berhasil diterapkan secara global.", "OK");
        }
        else
        {
            await DisplayAlert("Validasi", "Masukkan angka ambang batas yang valid.", "OK");
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        Preferences.Remove("session_nama");
        Preferences.Remove("session_role");
        Preferences.Remove("session_userid");
        Preferences.Set("session_active", false);

        await Shell.Current.GoToAsync("//LoginPage");
    }
}