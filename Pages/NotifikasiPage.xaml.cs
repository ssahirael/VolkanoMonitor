using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VolcanoMonitor.Models;
using VolcanoMonitor.Services;

namespace VolcanoMonitor.Pages;

public partial class NotifikasiPage : ContentPage
{
    private readonly DatabaseService _db;
    private List<Volcano> _volcanoes = new();
    private List<Alert> _alerts = new();
    private string _role = "user";

    public NotifikasiPage(DatabaseService db)
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

        // 1. Check role and display Admin Console
        _role = Preferences.Get("session_role", "user");
        AdminConsole.IsVisible = _role == "admin";

        // 2. Load Volcanoes for Picker
        _volcanoes = await _db.GetAllVolcanoesAsync();
        PickerVolcano.ItemsSource = _volcanoes;
        PickerVolcano.ItemDisplayBinding = new Binding("Name");

        // 3. Load Alerts Log
        await LoadAlertsListAsync();
    }

    private async Task LoadAlertsListAsync()
    {
        _alerts = await _db.GetAllAlertsAsync();
        AlertsListContainer.Children.Clear();

        if (_alerts.Count == 0)
        {
            var noAlertsLabel = new Label
            {
                Text = "Tidak ada peringatan aktif saat ini.",
                TextColor = Colors.Gray,
                FontSize = 12,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 20)
            };
            AlertsListContainer.Children.Add(noAlertsLabel);
            return;
        }

        foreach (var a in _alerts)
        {
            var volcano = _volcanoes.FirstOrDefault(v => v.Id == a.VolcanoId);
            string volcanoName = volcano != null ? volcano.Name.ToUpper() : "UNKNOWN";
            var statusColor = GetStatusColor(a.Level);

            // Left indicator bar
            var leftBar = new BoxView
            {
                WidthRequest = 4,
                Color = statusColor,
                VerticalOptions = LayoutOptions.Fill
            };

            // Main Details
            var headerLabel = new Label
            {
                Text = $"GUNUNG {volcanoName} — {a.Level}",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = statusColor
            };

            var timestampLabel = new Label
            {
                Text = $"Dirilis: {a.CreatedAt:dd/MM/yyyy HH:mm:ss}",
                FontSize = 9,
                TextColor = Colors.DimGray
            };

            var messageLabel = new Label
            {
                Text = a.Message,
                FontSize = 12,
                TextColor = Colors.LightGray,
                LineBreakMode = LineBreakMode.WordWrap
            };

            var infoContainer = new VerticalStackLayout
            {
                Spacing = 4,
                Padding = new Thickness(12, 10),
                Children = { headerLabel, timestampLabel, messageLabel }
            };

            // Grid for card layout
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };

            grid.Children.Add(leftBar);
            Grid.SetColumn(leftBar, 0);

            grid.Children.Add(infoContainer);
            Grid.SetColumn(infoContainer, 1);

            // Add delete button for Admin
            if (_role == "admin")
            {
                var colDef = grid.ColumnDefinitions;
                colDef.Add(new ColumnDefinition { Width = GridLength.Auto });

                var btnDelete = new Button
                {
                    Text = "🗑",
                    BackgroundColor = Colors.Transparent,
                    TextColor = Color.FromArgb("#E10600"),
                    FontSize = 14,
                    Padding = new Thickness(10),
                    VerticalOptions = LayoutOptions.Center,
                    AutomationId = a.Id.ToString()
                };
                btnDelete.Clicked += OnDeleteAlertClicked;

                grid.Children.Add(btnDelete);
                Grid.SetColumn(btnDelete, 2);
            }

            var cardBorder = new Border
            {
                BackgroundColor = Color.FromArgb("#0D0D0D"),
                Stroke = new SolidColorBrush(Color.FromArgb("#222")),
                StrokeThickness = 1,
                Content = grid,
                Margin = new Thickness(0, 0, 0, 4)
            };

            AlertsListContainer.Children.Add(cardBorder);
        }
    }

    private async void OnPostAlertClicked(object sender, EventArgs e)
    {
        var selectedVolcano = PickerVolcano.SelectedItem as Volcano;
        var selectedLevel = PickerVolcano.SelectedIndex >= 0 ? PickerLevel.SelectedItem as string : null;
        var message = TxtAlertMessage.Text?.Trim();

        if (selectedVolcano == null || string.IsNullOrEmpty(selectedLevel) || string.IsNullOrEmpty(message))
        {
            await DisplayAlert("Validasi", "Wajib memilih gunung, level status, dan menulis pesan.", "OK");
            return;
        }

        // 1. Insert alert
        var newAlert = new Alert
        {
            VolcanoId = selectedVolcano.Id,
            Level = selectedLevel,
            Message = message,
            CreatedAt = DateTime.Now
        };
        await _db.InsertAlertAsync(newAlert);

        // 2. Override status in Volcano table
        selectedVolcano.CurrentStatus = selectedLevel;
        await _db.UpdateVolcanoAsync(selectedVolcano);

        // 3. Clear inputs and reload
        TxtAlertMessage.Text = string.Empty;
        PickerVolcano.SelectedIndex = -1;
        PickerLevel.SelectedIndex = -1;

        await LoadDataAsync();
        await DisplayAlert("Sukses", $"Peringatan dini dipublikasikan. Status {selectedVolcano.Name} di-override menjadi {selectedLevel}.", "OK");
    }

    private async void OnDeleteAlertClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.AutomationId != null)
        {
            int alertId = int.Parse(btn.AutomationId);
            var alert = _alerts.FirstOrDefault(a => a.Id == alertId);
            if (alert != null)
            {
                bool confirm = await DisplayAlert("Konfirmasi", "Hapus log peringatan dini ini?", "YA", "TIDAK");
                if (confirm)
                {
                    await _db.DeleteAlertAsync(alert);
                    await LoadAlertsListAsync();
                }
            }
        }
    }

    private Color GetStatusColor(string status) => status switch
    {
        "NORMAL" => Color.FromArgb("#3DDC97"),
        "WASPADA" => Color.FromArgb("#FFD166"),
        "SIAGA" => Color.FromArgb("#FF9F1C"),
        "AWAS" => Color.FromArgb("#FF2E9F"),
        _ => Colors.Gray
    };
}