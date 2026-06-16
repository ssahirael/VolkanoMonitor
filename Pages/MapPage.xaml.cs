using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using VolcanoMonitor.Models;
using VolcanoMonitor.Services;

namespace VolcanoMonitor.Pages
{
    public partial class MapPage : ContentPage
    {
        private readonly DatabaseService _db;
        private List<Volcano> _allVolcanoes = new();

        public MapPage(DatabaseService db)
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

            await LoadMapDataAsync();
        }

        private async Task LoadMapDataAsync()
        {
            try
            {
                MapLoadingOverlay.IsVisible = true;
                await _db.InitAsync();
                _allVolcanoes = await _db.GetAllVolcanoesAsync();

                // 1. Calculate map summary: "4 AKTIF • 1 KRITIS"
                int activeCount = _allVolcanoes.Count(v => v.CurrentStatus == "WASPADA" || v.CurrentStatus == "SIAGA");
                int criticalCount = _allVolcanoes.Count(v => v.CurrentStatus == "AWAS");
                
                LblMapSummary.Text = $"{activeCount} AKTIF • {criticalCount} KRITIS";

                // 2. Generate Leaflet HTML
                var sbMarkers = new System.Text.StringBuilder();
                foreach (var v in _allVolcanoes)
                {
                    var color = v.CurrentStatus switch
                    {
                        "AWAS" => "#E10600",
                        "SIAGA" => "#FF8C00",
                        "WASPADA" => "#FFD700",
                        _ => "#3DDC97"
                    };
                    string latStr = v.Latitude.ToString("F5", System.Globalization.CultureInfo.InvariantCulture);
                    string lngStr = v.Longitude.ToString("F5", System.Globalization.CultureInfo.InvariantCulture);
                    sbMarkers.AppendLine($"        addMarker({v.Id}, {latStr}, {lngStr}, '{v.Name.Replace("'", "\\'")}', '{v.CurrentStatus}', '{color}');");
                }

                var html = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"" />
                    <link rel=""stylesheet"" href=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"" />
                    <script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js""></script>
                    <style>
                        html, body, #map {{ height: 100%; width: 100%; margin: 0; padding: 0; background-color: #0A0A0A; }}
                        .leaflet-popup-content-wrapper {{ background: #111; color: #fff; border: 1px solid #333; font-family: system-ui, -apple-system, sans-serif; border-radius: 8px; }}
                        .leaflet-popup-tip {{ background: #111; }}
                    </style>
                </head>
                <body>
                    <div id=""map""></div>
                    <script>
                        var map = L.map('map', {{ zoomControl: false }}).setView([-7.5, 111.0], 6);
                        L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
                            maxZoom: 19,
                            attribution: '&copy; OpenStreetMap'
                        }}).addTo(map);

                        var markers = {{}};
                        function addMarker(id, lat, lng, name, status, color) {{
                            var marker = L.circleMarker([lat, lng], {{
                                radius: 10,
                                fillColor: color,
                                color: '#ffffff',
                                weight: 2,
                                opacity: 1,
                                fillOpacity: 0.8
                            }}).addTo(map);
                            marker.bindPopup(""<div style='text-align: center;'><b style='font-size: 13px; color: "" + color + ""'>"" + name.toUpperCase() + ""</b><br><span style='font-size: 10px; font-weight: bold;'>STATUS: "" + status + ""</span></div>"");
                            
                            // Center map on marker click
                            marker.on('click', function(e) {{
                                map.setView([lat, lng], 10);
                            }});

                            markers[id] = marker;
                        }}

                        window.focusMarker = function(id, lat, lng) {{
                            var marker = markers[id];
                            if (marker) {{
                                map.setView([lat, lng], 10);
                                marker.openPopup();
                            }} else {{
                                map.setView([lat, lng], 10);
                            }}
                        }};

                        // Inject markers
                        {sbMarkers}
                    </script>
                </body>
                </html>
                ";

                MapWebView.Source = new HtmlWebViewSource { Html = html };

                // 3. Render Volcano Cards at the bottom
                RenderVolcanoCards();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MapPage Error: {ex.Message}");
            }
            finally
            {
                MapLoadingOverlay.IsVisible = false;
            }
        }

        private void RenderVolcanoCards()
        {
            VolcanoCardsContainer.Children.Clear();
            foreach (var v in _allVolcanoes)
            {
                var statusColor = GetStatusColor(v.CurrentStatus);

                var badge = new Border
                {
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                    BackgroundColor = statusColor.WithAlpha(0.15f),
                    Stroke = new SolidColorBrush(statusColor),
                    StrokeThickness = 1,
                    Padding = new Thickness(6, 2),
                    HorizontalOptions = LayoutOptions.Start,
                    Content = new Label { Text = v.CurrentStatus, FontSize = 8, FontAttributes = FontAttributes.Bold, TextColor = statusColor }
                };

                var nameLbl = new Label { Text = v.Name.ToUpper(), FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Colors.White };
                var locLbl = new Label { Text = v.Location, FontSize = 10, TextColor = Color.FromArgb("#B9A7D9") };

                var cardStack = new VerticalStackLayout
                {
                    Spacing = 4,
                    Children = { nameLbl, locLbl, badge }
                };

                var card = new Border
                {
                    BackgroundColor = Color.FromArgb("#12FFFFFF"),
                    Stroke = new SolidColorBrush(Color.FromArgb("#26FFFFFF")),
                    StrokeThickness = 1,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                    Padding = new Thickness(14, 10),
                    WidthRequest = 140,
                    Content = cardStack
                };

                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += async (s, e) =>
                {
                    try
                    {
                        string latStr = v.Latitude.ToString("F5", System.Globalization.CultureInfo.InvariantCulture);
                        string lngStr = v.Longitude.ToString("F5", System.Globalization.CultureInfo.InvariantCulture);
                        // Call JS function to center map and open popup
                        await MapWebView.EvaluateJavaScriptAsync($"window.focusMarker({v.Id}, {latStr}, {lngStr});");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error focusing marker: {ex.Message}");
                    }
                };
                card.GestureRecognizers.Add(tapGesture);

                VolcanoCardsContainer.Children.Add(card);
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
}