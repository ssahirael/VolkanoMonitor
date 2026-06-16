using System;
using System.Security.Permissions;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace VolcanoMonitor.Pages;

public partial class FaceScanPage : ContentPage
{
    private IDispatcherTimer? _timer;
    private int _progress = 0;

    private readonly string[] _steps =
    {
        "Mendeteksi wajah...",
        "Menganalisis biometrik...",
        "Memverifikasi identitas...",
        "Memeriksa database...",
        "Otorisasi sukses! ✅"
    };

    public FaceScanPage() => InitializeComponent();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Disabled;
    }

    private async void OnStartScan(object sender, EventArgs e)
    {
        BtnScan.IsEnabled = false;
        _progress = 0;

        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Verifikasi Wajah", "Izin kamera diperlukan", "OK");
            BtnScan.IsEnabled = true;
            return;
        }

        try
        {
            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo == null)
            {
                await DisplayAlert("Verifikasi Wajah", "Verifikasi wajah wajib", "OK");
                BtnScan.IsEnabled = true;
                return;
            }
            LabelStatus.Text = "Foto wajah berhasil diambil. Memproses...";
            StartSimulation();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Gagal memindai wajah: {ex.Message}", "OK");
            BtnScan.IsEnabled = true;
        }
    }

    private void StartSimulation()
    {
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(80);
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        _progress++;
        var pct = _progress / 100.0;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            ProgressScan.Progress = pct;
            LabelPercent.Text = $"{_progress}%";

            int stepIdx = Math.Min(_progress / 20, _steps.Length - 1);
            LabelStatus.Text = _steps[stepIdx];

            if (_progress >= 50) LabelFaceIcon.Text = "🔍";
            if (_progress >= 80) LabelFaceIcon.Text = "✅";
        });

        if (_progress >= 100)
        {
            _timer?.Stop();
            Preferences.Set("session_active", true);
            Shell.Current.FlyoutBehavior = FlyoutBehavior.Locked;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Task.Delay(500);
                await Shell.Current.GoToAsync("//StatusPage");
            });
        }
    }
}