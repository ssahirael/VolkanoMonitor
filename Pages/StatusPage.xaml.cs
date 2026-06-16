using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VolcanoMonitor.Models;
using VolcanoMonitor.Services;

namespace VolcanoMonitor.Pages;

public partial class StatusPage : ContentPage
{
    private readonly DatabaseService _db;
    private List<Volcano> _allVolcanoes = new();
    private string _currentFilter = "ALL";
    private Volcano? _featuredVolcano;

    public StatusPage(DatabaseService db)
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
        var name = Preferences.Get("session_nama", "Pengguna");
        LblGreeting.Text = $"Halo, {name} 👋";
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        await _db.InitAsync();
        _allVolcanoes = await _db.GetAllVolcanoesAsync();

        LblStatCount.Text = $"{_allVolcanoes.Count} Gunung";
        LblTotalCount.Text = $"{_allVolcanoes.Count} stasiun";

        // Hero: highest risk volcano
        _featuredVolcano = _allVolcanoes
            .OrderByDescending(v => GetStatusPriority(v.CurrentStatus))
            .FirstOrDefault();

        if (_featuredVolcano != null)
        {
            var readings = await _db.GetLatestReadingsForVolcanoAsync(_featuredVolcano.Id);
            double so2 = readings.GetValueOrDefault("SO2", 0);
            double co2 = readings.GetValueOrDefault("CO2", 0);
            double temp = readings.GetValueOrDefault("TEMPERATURE", 0);
            double seis = readings.GetValueOrDefault("SEISMIC", 0);

            LblHeroLevel.Text = $"LEVEL {GetStatusRoman(_featuredVolcano.CurrentStatus)} — {_featuredVolcano.CurrentStatus}";
            LblHeroName.Text = $"Gunung {_featuredVolcano.Name}";
            LblHeroTitle.Text = _featuredVolcano.CurrentStatus == "AWAS" ? "⚠️ Status Darurat Aktif" : "Status Terkini";
            LblHeroDesc.Text = $"{_featuredVolcano.Description[..Math.Min(100, _featuredVolcano.Description.Length)]}…";

            // Sensor summary from most critical volcano
            LblSO2Val.Text = $"{so2:F1}";
            LblCO2Val.Text = $"{co2:F0}";
            LblTempVal.Text = $"{temp:F1}";
            LblSeisVal.Text = $"{seis:F1}";

            LblSO2Val.TextColor = so2 > 15 ? Color.FromArgb("#FF2E9F") : Colors.White;
            LblTempVal.TextColor = temp > 200 ? Color.FromArgb("#FF2E9F") : Colors.White;
        }

        RenderEventList();
    }

    private void RenderEventList()
    {
        EventListContainer.Children.Clear();

        var list = _currentFilter == "ALL"
            ? _allVolcanoes
            : _allVolcanoes.Where(v => v.CurrentStatus.Equals(_currentFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var v in list)
        {
            var statusColor = GetStatusColor(v.CurrentStatus);

            var badge = new Border
            {
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                BackgroundColor = statusColor.WithAlpha(0.2f),
                Stroke = new SolidColorBrush(statusColor),
                StrokeThickness = 1,
                Padding = new Thickness(8, 3),
                HorizontalOptions = LayoutOptions.Start,
                Content = new Label { Text = v.CurrentStatus, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = statusColor }
            };

            var nameLabel = new Label { Text = v.Name.ToUpper(), FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Colors.White };
            var locLabel = new Label { Text = $"{v.Location} • {v.Elevation} m", FontSize = 11, TextColor = Color.FromArgb("#B9A7D9") };

            var btnDetail = new Button
            {
                Text = "Detail →",
                BackgroundColor = Color.FromArgb("#FF2E9F"),
                TextColor = Colors.White,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                CornerRadius = 16,
                Padding = new Thickness(12, 6),
                VerticalOptions = LayoutOptions.Center,
                AutomationId = v.Id.ToString()
            };
            btnDetail.Clicked += OnEventDetailClicked;

            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
            var infoStack = new VerticalStackLayout { Spacing = 4, Children = { nameLabel, locLabel, badge } };
            row.Children.Add(infoStack);
            Grid.SetColumn(infoStack, 0);
            row.Children.Add(btnDetail);
            Grid.SetColumn(btnDetail, 1);

            var card = new Border
            {
                BackgroundColor = Color.FromArgb("#12FFFFFF"),
                Stroke = new SolidColorBrush(Color.FromArgb("#26FFFFFF")),
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(16, 12),
                Content = row
            };
            EventListContainer.Children.Add(card);
        }
    }

    private void OnFilterTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string filter)
        {
            _currentFilter = filter;
            var pills = new[] { PillAll, PillNormal, PillWaspada, PillSiaga, PillAwas };
            var params_ = new[] { "ALL", "NORMAL", "WASPADA", "SIAGA", "AWAS" };
            for (int i = 0; i < pills.Length; i++)
            {
                bool active = params_[i] == filter;
                pills[i].BackgroundColor = active ? Color.FromArgb("#FF2E9F") : Color.FromArgb("#18FFFFFF");
                pills[i].Stroke = new SolidColorBrush(active ? Color.FromArgb("#FF2E9F") : Color.FromArgb("#26FFFFFF"));
                pills[i].StrokeThickness = active ? 0 : 1;
                var lbl = pills[i].Content as Label;
                if (lbl != null) lbl.TextColor = active ? Colors.White : Color.FromArgb("#B9A7D9");
            }
            RenderEventList();
        }
    }

    private async void OnKetukUntukPeta(object sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync("//MapPage");

    private async void OnHeroCardTapped(object sender, TappedEventArgs e)
    {
        if (_featuredVolcano != null)
            await Shell.Current.GoToAsync($"//JelajahPage?volcanoId={_featuredVolcano.Id}");
    }

    private async void OnHeroDetailClicked(object sender, EventArgs e)
    {
        if (_featuredVolcano != null)
            await Shell.Current.GoToAsync($"//JelajahPage?volcanoId={_featuredVolcano.Id}");
    }

    private async void OnEventDetailClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.AutomationId != null)
            await Shell.Current.GoToAsync($"//JelajahPage?volcanoId={btn.AutomationId}");
    }

    private Color GetStatusColor(string status) => status switch
    {
        "NORMAL" => Color.FromArgb("#3DDC97"),
        "WASPADA" => Color.FromArgb("#FFD166"),
        "SIAGA" => Color.FromArgb("#FF9F1C"),
        "AWAS" => Color.FromArgb("#FF2E9F"),
        _ => Colors.Gray
    };

    private int GetStatusPriority(string s) => s switch { "AWAS" => 4, "SIAGA" => 3, "WASPADA" => 2, _ => 1 };

    private string GetStatusRoman(string s) => s switch { "AWAS" => "IV", "SIAGA" => "III", "WASPADA" => "II", _ => "I" };
}