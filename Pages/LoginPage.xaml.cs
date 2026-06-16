using System;
using VolcanoMonitor.Services;

namespace VolcanoMonitor.Pages;

public partial class LoginPage : ContentPage
{
    private readonly DatabaseService _db;

    public LoginPage(DatabaseService db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Disabled;
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var input = EntryNama.Text?.Trim(); // Used as Email or Name
        var password = EntryPassword.Text;

        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(password))
        {
            await DisplayAlert("Validasi", "Email / Nama Pengguna dan Password wajib diisi.", "OK");
            return;
        }

        await _db.InitAsync();

        // Find user by email or by name
        var user = await _db.GetUserByEmailAsync(input);
        if (user == null)
        {
            // Try by name as fallback
            var allUsers = await _db.GetAllUsersAsync();
            user = allUsers.FirstOrDefault(u => u.Name.Equals(input, StringComparison.OrdinalIgnoreCase));
        }

        if (user == null)
        {
            await DisplayAlert("Error", "Akun tidak ditemukan.", "OK");
            return;
        }

        if (!user.IsActive)
        {
            await DisplayAlert("Akses Ditolak", "Akun Anda dinonaktifkan oleh Administrator.", "OK");
            return;
        }

        var hashedInput = HashHelper.ComputeSha256Hash(password);
        if (user.PasswordHash != hashedInput)
        {
            await DisplayAlert("Error", "Password salah.", "OK");
            return;
        }

        // Set session
        Preferences.Set("session_nama", user.Name);
        Preferences.Set("session_role", user.Role);
        Preferences.Set("session_userid", user.Id);
        Preferences.Set("session_active", false); // active is set to true after face scan

        // Proceed to Face Scan
        await Shell.Current.GoToAsync("//FaceScanPage");
    }

    private async void OnGoToRegister(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//RegisterPage");
    }
}