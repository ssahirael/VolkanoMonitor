using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using VolcanoMonitor.Models;
using VolcanoMonitor.Services;

namespace VolcanoMonitor.Pages;

[QueryProperty(nameof(SelectedVolcanoIdString), "volcanoId")]
public partial class JelajahPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly NeuralNetworkService _nn;
    private readonly FuzzyLogicService _fuzzy;
    private readonly GeminiService _gemini;

    private List<Volcano> _allVolcanoes = new();
    private List<Volcano> _filteredVolcanoes = new();
    private Volcano? _selectedVolcano;
    private string _activeParameter = "SO2";
    private List<Reading> _historicalReadings = new();

    // Query Property backing field
    public string SelectedVolcanoIdString
    {
        set
        {
            if (int.TryParse(value, out int id))
            {
                _ = LoadVolcanoByIdAsync(id);
            }
        }
    }

    public JelajahPage(DatabaseService db, NeuralNetworkService nn, FuzzyLogicService fuzzy, GeminiService gemini)
    {
        InitializeComponent();
        _db = db;
        _nn = nn;
        _fuzzy = fuzzy;
        _gemini = gemini;
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
        await LoadAllVolcanoesAsync();
    }

    private async Task LoadAllVolcanoesAsync()
    {
        await _db.InitAsync();
        _allVolcanoes = await _db.GetAllVolcanoesAsync();
        _filteredVolcanoes = _allVolcanoes;

        RenderSelectorTabs();

        if (_selectedVolcano == null && _allVolcanoes.Count > 0)
        {
            await SelectVolcanoAsync(_allVolcanoes.First());
        }
    }

    private async Task LoadVolcanoByIdAsync(int id)
    {
        await _db.InitAsync();
        var volcano = await _db.GetVolcanoByIdAsync(id);
        if (volcano != null)
        {
            await SelectVolcanoAsync(volcano);
        }
    }

    private void RenderSelectorTabs()
    {
        VolcanoSelectorContainer.Children.Clear();
        foreach (var v in _filteredVolcanoes)
        {
            bool isActive = _selectedVolcano != null && _selectedVolcano.Id == v.Id;

            var btn = new Button
            {
                Text = v.Name.ToUpper(),
                BackgroundColor = isActive ? Colors.White : Color.FromArgb("#111"),
                TextColor = isActive ? Colors.Black : Colors.White,
                FontAttributes = FontAttributes.Bold,
                FontSize = 10,
                CornerRadius = 0,
                Padding = new Thickness(14, 0),
                HeightRequest = 32,
                AutomationId = v.Id.ToString()
            };

            btn.Clicked += (s, e) =>
            {
                var clickedVolcano = _allVolcanoes.FirstOrDefault(vol => vol.Id == int.Parse(btn.AutomationId));
                if (clickedVolcano != null)
                {
                    _ = SelectVolcanoAsync(clickedVolcano);
                }
            };

            VolcanoSelectorContainer.Children.Add(btn);
        }
    }

    private async Task SelectVolcanoAsync(Volcano v)
    {
        _selectedVolcano = v;
        RenderSelectorTabs();

        // 1. Details
        LblVolcanoName.Text = $"GUNUNG {v.Name.ToUpper()}";
        LblVolcanoStatus.Text = v.CurrentStatus;
        LblVolcanoStatus.BackgroundColor = GetStatusColor(v.CurrentStatus);
        LblVolcanoStatus.TextColor = v.CurrentStatus == "WASPADA" ? Colors.Black : Colors.White;
        LblVolcanoMeta.Text = $"LOKASI: {v.Location.ToUpper()} • ELEVASI: {v.Elevation}m";
        LblVolcanoDesc.Text = v.Description;

        LblHazardZone.Text = v.CurrentStatus switch
        {
            "NORMAL" => "Status Normal. Jarak aman di luar radius 1 km dari pusat kawah utama.",
            "WASPADA" => "Status Waspada. Radius bahaya ditetapkan 1.5 km. Masyarakat dilarang mendekati kawah aktif.",
            "SIAGA" => "Status Siaga. Jarak aman di luar radius 3 km untuk sektor Selatan-Tenggara, dan 5 km sektoral Barat Daya.",
            _ => "Status Awas (KRITIS). Jarak aman di luar radius 5 km sampai 7.5 km. Seluruh penduduk wajib segera dievakuasi!"
        };

        // 2. Load Telemetries & AI Predictions
        var readings = await _db.GetLatestReadingsForVolcanoAsync(v.Id);
        double so2 = readings.GetValueOrDefault("SO2", 0);
        double co2 = readings.GetValueOrDefault("CO2", 0);
        double h2s = readings.GetValueOrDefault("H2S", 0);
        double temp = readings.GetValueOrDefault("TEMPERATURE", 0);
        double seismic = readings.GetValueOrDefault("SEISMIC", 0);

        LblValSO2.Text = $"{so2:F1}";
        LblValCO2.Text = $"{co2:F0}";
        LblValH2S.Text = $"{h2s:F1}";
        LblValTemp.Text = $"{temp:F1}";
        LblValSeis.Text = $"{seismic:F1}";

        // Colors depending on value thresholds
        LblValSO2.TextColor = so2 > 15 ? Color.FromArgb("#E10600") : Colors.White;
        LblValCO2.TextColor = co2 > 1000 ? Color.FromArgb("#FF8C00") : Colors.White;
        LblValTemp.TextColor = temp > 200 ? Color.FromArgb("#E10600") : Colors.White;

        // Run AI engines
        var nnResult = _nn.Predict(so2, co2, h2s, temp, seismic);
        double fuzzyRisk = _fuzzy.EvaluateRisk(so2, co2, h2s, temp, seismic);
        string explanation = _fuzzy.GetExplanation(fuzzyRisk, nnResult.Level);

        LblNNResult.Text = $"{nnResult.Level} ({nnResult.Confidence * 100:F1}%)";
        LblFuzzyResult.Text = $"RISIKO: {fuzzyRisk:F1}%";
        LblLaymanExplanation.Text = explanation;

        // Save prediction result to DB
        await _db.SavePredictionAsync(new Prediction
        {
            VolcanoId = v.Id,
            NNResult = $"{nnResult.Level} ({nnResult.Confidence * 100:F1}%)",
            FuzzyRiskIndex = fuzzyRisk,
            Explanation = explanation,
            CreatedAt = DateTime.Now
        });

        // 3. Load historical charts data
        await LoadHistoricalTelemetryAsync();
    }

    private async Task LoadHistoricalTelemetryAsync()
    {
        if (_selectedVolcano == null) return;

        _historicalReadings = await _db.GetReadingsForVolcanoAsync(_selectedVolcano.Id, _activeParameter);
        LblChartTitle.Text = $"GRAFIK TREN {_activeParameter} (10 JAM TERAKHIR)";
        TelemetryChart.InvalidateSurface();
    }

    private void OnPaintTelemetryChart(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var w = e.Info.Width;
        var h = e.Info.Height;

        canvas.Clear(new SKColor(10, 10, 10)); // Pitch dark background for chart

        if (_historicalReadings.Count < 2)
        {
            using var font = new SKFont { Size = 24 };
            using var textPaint = new SKPaint
            {
                Color = SKColors.Gray,
                IsAntialias = true,
            };
            canvas.DrawText("Data historis tidak mencukupi", w / 2f, h / 2f, SKTextAlign.Center, font, textPaint);
            return;
        }

        var values = _historicalReadings.Select(r => (float)r.Value).ToList();
        float maxVal = values.Max() + 1.0f;
        float minVal = Math.Max(0.0f, values.Min() - 1.0f);
        if (Math.Abs(maxVal - minVal) < 1e-4) maxVal += 5f;

        // Draw grids
        using (var gridPaint = new SKPaint { Color = new SKColor(30, 30, 30), StrokeWidth = 1 })
        {
            for (int r = 1; r < 4; r++)
            {
                float gridY = h * r / 4f;
                canvas.DrawLine(0, gridY, w, gridY, gridPaint);
            }
        }

        // Draw Line & Area
        var path = new SKPath();
        var fillPath = new SKPath();
        float stepX = (float)w / (values.Count - 1);

        for (int i = 0; i < values.Count; i++)
        {
            float x = i * stepX;
            float y = h - ((values[i] - minVal) / (maxVal - minVal)) * (h - 24) - 12;

            if (i == 0)
            {
                path.MoveTo(x, y);
                fillPath.MoveTo(x, h);
                fillPath.LineTo(x, y);
            }
            else
            {
                path.LineTo(x, y);
                fillPath.LineTo(x, y);
            }
        }
        fillPath.LineTo(w, h);
        fillPath.Close();

        // Color theme Red or Gold depending on parameter
        var accentColor = _activeParameter == "TEMPERATURE" || _activeParameter == "SO2"
            ? new SKColor(225, 6, 0) // Red
            : new SKColor(255, 140, 0); // Orange

        using (var fillPaint = new SKPaint
        {
            Color = accentColor.WithAlpha(30),
            Style = SKPaintStyle.Fill
        })
        {
            canvas.DrawPath(fillPath, fillPaint);
        }

        using (var linePaint = new SKPaint
        {
            Color = accentColor,
            StrokeWidth = 3f,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        })
        {
            canvas.DrawPath(path, linePaint);
        }

        // Draw values text at start, peak, and end
        using var valFont = new SKFont { Size = 18 };
        using var valPaint = new SKPaint
        {
            Color = SKColors.LightGray,
            IsAntialias = true
        };
        canvas.DrawText($"Min: {minVal:F1}", 10, h - 10, SKTextAlign.Left, valFont, valPaint);
        canvas.DrawText($"Max: {maxVal:F1}", 10, 24, SKTextAlign.Left, valFont, valPaint);
    }

    private void OnParamToggleClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string param)
        {
            _activeParameter = param;

            // Update UI selections
            var buttons = new[] { BtnParamSO2, BtnParamCO2, BtnParamH2S, BtnParamTemp, BtnParamSeis };
            foreach (var b in buttons)
            {
                if (b == btn)
                {
                    b.BackgroundColor = Colors.White;
                    b.TextColor = Colors.Black;
                }
                else
                {
                    b.BackgroundColor = Color.FromArgb("#111");
                    b.TextColor = Colors.White;
                }
            }

            _ = LoadHistoricalTelemetryAsync();
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        PerformSearch();
    }

    private void OnSearchClicked(object sender, EventArgs e)
    {
        PerformSearch();
    }

    private void PerformSearch()
    {
        var text = TxtSearch.Text?.Trim().ToLower() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            _filteredVolcanoes = _allVolcanoes;
        }
        else
        {
            _filteredVolcanoes = _allVolcanoes.Where(v => v.Name.ToLower().Contains(text) || v.Location.ToLower().Contains(text)).ToList();
        }
        RenderSelectorTabs();
    }

    // ===== CHATBOT ACTIONS =====
    private async void OnOpenChatClicked(object sender, EventArgs e)
    {
        if (_selectedVolcano == null) return;
        ChatOverlay.IsVisible = true;
        LblChatTarget.Text = $"Kontekstual: Gunung {_selectedVolcano.Name}";

        ChatHistoryContainer.Children.Clear();

        // Add initial bot greeting
        AddChatBubble($"Halo! Saya VulkanoBot. Ada yang bisa saya bantu terkait status, data sensor terbaru, atau langkah mitigasi keselamatan untuk Gunung {_selectedVolcano.Name}?", false);

        // Load chat history from SQLite for context
        var userId = Preferences.Get("session_userid", 0);
        var history = await _db.GetChatLogsForUserAsync(userId);
        // Display last 5 chats if exist
        foreach (var chat in history.TakeLast(5))
        {
            AddChatBubble(chat.Question, true);
            AddChatBubble(chat.Answer, false);
        }

        await ScrollChatToBottom();
    }

    private void OnCloseChatClicked(object sender, EventArgs e)
    {
        ChatOverlay.IsVisible = false;
    }

    private async Task<string> BuildSensorContextAsync()
    {
        if (_selectedVolcano == null)
            return "Tidak ada data sensor tersedia.";

        try
        {
            var readings = await _db.GetLatestReadingsForVolcanoAsync(_selectedVolcano.Id);
            double so2 = readings.GetValueOrDefault("SO2", 0);
            double temp = readings.GetValueOrDefault("TEMPERATURE", 0);
            double seismic = readings.GetValueOrDefault("SEISMIC", 0);

            return $"Gunung: {_selectedVolcano.Name}\n" +
                   $"Status Aktivitas: {_selectedVolcano.CurrentStatus}\n" +
                   $"Data Sensor -> SO2: {so2:F1} ppm, Temp: {temp:F1} C, Seismik: {seismic:F1}\n" +
                   $"Lokasi: {_selectedVolcano.Location}";
        }
        catch
        {
            return "Data sensor tidak tersedia.";
        }
    }

    private async void OnSendChatClicked(object sender, EventArgs e)
    {
        // 1. Validasi input
        string question = TxtChatQuestion.Text?.Trim();
        if (string.IsNullOrEmpty(question) || _selectedVolcano == null) return;

        // 2. Bersihkan input & nonaktifkan tombol SEBELUM proses
        TxtChatQuestion.Text = string.Empty;
        SendButton.IsEnabled = false;
        TxtChatQuestion.IsEnabled = false;

        // 3. Tambahkan balon user
        AddChatBubble(question, isUser: true);

        // 4. Tambahkan balon loading SEBELUM try block
        //    (simpan referensi agar bisa diupdate setelah dapat jawaban)
        var loadingBubble = AddChatBubble(
            "Sedang memproses...", isUser: false);

        try
        {
            // 5. Ambil data sensor dari database untuk konteks
            string context = await BuildSensorContextAsync();

            // 6. Panggil Gemini (sudah ada timeout 30 detik di service)
            var geminiService = new Services.GeminiService();
            string answer = await geminiService.AskGeminiAsync(
                question, context);

            // 7. Update balon loading dengan jawaban bot
            loadingBubble.Text = answer;

            // Simpan history ke database
            var userId = Preferences.Get("session_userid", 0);
            await _db.InsertChatLogAsync(new ChatLog
            {
                UserId = userId,
                Question = question,
                Answer = answer,
                CreatedAt = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            // 8. Tampilkan error di balon bot (jangan crash diam-diam)
            loadingBubble.Text =
                $"[ERROR] {ex.GetType().Name}: {ex.Message}";
            System.Diagnostics.Debug.WriteLine(
                $"[Chat] Exception: {ex}");
        }
        finally
        {
            // 9. Selalu aktifkan kembali tombol kirim
            SendButton.IsEnabled = true;
            TxtChatQuestion.IsEnabled = true;
        }

        // 10. Scroll ke bawah (sudah terlindungi try/catch sendiri)
        await ScrollChatToBottom();
    }

    private Label AddChatBubble(string text, bool isUser)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 12,
            TextColor = Colors.White,
            LineBreakMode = LineBreakMode.WordWrap
        };

        var bubble = new Border
        {
            BackgroundColor = isUser ? Color.FromArgb("#1E1E1E") : Color.FromArgb("#E10600").WithAlpha(40),
            Stroke = new SolidColorBrush(isUser ? Color.FromArgb("#333") : Color.FromArgb("#E10600")),
            StrokeThickness = 1,
            Padding = 12,
            HorizontalOptions = isUser ? LayoutOptions.End : LayoutOptions.Start,
            Margin = new Thickness(isUser ? 40 : 0, 2, isUser ? 0 : 40, 2),
            Content = label
        };

        ChatHistoryContainer.Children.Add(bubble);
        return label;
    }

    private async Task ScrollChatToBottom()
    {
        try
        {
            await Task.Delay(150);
            double targetY = ChatHistoryContainer.Height;
            await ChatScroll.ScrollToAsync(0, targetY, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Scroll] {ex.Message}");
            // Swallow — jangan biarkan exception ini
            // menghentikan alur chatbot
        }
    }

    private Color GetStatusColor(string status)
    {
        return status switch
        {
            "NORMAL" => Colors.Green,
            "WASPADA" => Color.FromArgb("#FFD700"), // Yellow
            "SIAGA" => Color.FromArgb("#FF8C00"),  // Orange
            "AWAS" => Color.FromArgb("#E10600"),   // Red
            _ => Colors.Gray
        };
    }
}