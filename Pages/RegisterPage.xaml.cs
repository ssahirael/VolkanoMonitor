using System;
using VolcanoMonitor.Models;
using VolcanoMonitor.Services;

namespace VolcanoMonitor.Pages;

public partial class RegisterPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly FaceRecognitionService _faceService;
    private string _selectedRole = "user";

    public RegisterPage(DatabaseService db, FaceRecognitionService faceService)
    {
        InitializeComponent();
        _db = db;
        _faceService = faceService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Disabled;
    }

    private void OnRoleChanged(object sender, CheckedChangedEventArgs e)
    {
        if (sender is RadioButton rb && rb.IsChecked)
        {
            _selectedRole = rb.Value?.ToString() ?? "user";
            LabelRoleInfo.Text = _selectedRole == "admin"
                ? "Peran: Admin / Petugas Vulkanologi"
                : "Peran: Warga Sipil";
        }
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        var nama = EntryNama.Text?.Trim();
        var email = EntryEmail.Text?.Trim();
        var pass = EntryPassword.Text;

        if (string.IsNullOrEmpty(nama) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass))
        {
            await DisplayAlert("Validasi", "Semua field wajib diisi.", "OK");
            return;
        }

        if (!email.Contains("@") || !email.Contains("."))
        {
            await DisplayAlert("Validasi", "Format email tidak valid.", "OK");
            return;
        }

        await _db.InitAsync();

        var existingUser = await _db.GetUserByEmailAsync(email);
        if (existingUser != null)
        {
            await DisplayAlert("Error", "Email sudah terdaftar.", "OK");
            return;
        }

        var hashedPassword = HashHelper.ComputeSha256Hash(pass);

        // Biometric Face Capture
        await DisplayAlert("Verifikasi Wajah", "Silakan ambil foto wajah Anda untuk keamanan biometrik.", "Lanjut");

        var photo = await MediaPicker.Default.CapturePhotoAsync();
        if (photo == null)
        {
            await DisplayAlert("Registrasi Dibatalkan", "Foto wajah wajib untuk registrasi.", "OK");
            return;
        }

        using var stream = await photo.OpenReadAsync();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        byte[] photoBytes = ms.ToArray();

        var embedding = await _faceService.DetectAndEmbedAsync(photoBytes);
        if (embedding == null)
        {
            await DisplayAlert("Error", "Wajah tidak terdeteksi atau gambar kurang jelas. Silakan ulangi.", "OK");
            return;
        }

        var faceEmbeddingBytes = _faceService.SerializeEmbedding(embedding);

        var newUser = new User
        {
            Name = nama,
            Email = email,
            PasswordHash = hashedPassword,
            Role = _selectedRole,
            IsActive = true,
            FaceEmbedding = faceEmbeddingBytes
        };

        await _db.InsertUserAsync(newUser);

        // Store session data in preferences
        Preferences.Set("session_nama", nama);
        Preferences.Set("session_role", _selectedRole);
        Preferences.Set("session_userid", newUser.Id);
        Preferences.Set("session_active", false);

        // Direct to face scan page for registration bio verification
        await Shell.Current.GoToAsync("//FaceScanPage");
    }

    private async void OnGoToLogin(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("//LoginPage");
}