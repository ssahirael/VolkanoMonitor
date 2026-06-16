using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VolcanoMonitor.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;

        // ⚠ Jika key tidak aktif, dapatkan key baru dari
        //   https://aistudio.google.com/apikey
        //   Key yang valid berawalan "AQ."
        private const string ApiKey =
            "AQ.Ab8RN6KuqKwPmLEIH7_GjgTfA7iLU__UkxZlbKPej-S4BYG3PA";

        // Endpoint v1 + model gemini-2.5-flash (TANPA ?key= di URL)
        private const string Endpoint =
            "https://generativelanguage.googleapis.com/v1/models/" +
            "gemini-2.5-flash:generateContent";

        public GeminiService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        // Jangan ubah signature method ini — dipanggil dari JelajahPage
        public async Task<string> AskGeminiAsync(
            string question, string context)
        {
            string systemPrompt =
                "Kamu adalah VulkanoBot, asisten AI spesialis " +
                "vulkanologi untuk aplikasi VULKANO. Berikan jawaban " +
                "yang akurat, informatif, dan mudah dipahami masyarakat " +
                "awam tentang aktivitas vulkanik, status gunung berapi, " +
                "dan mitigasi bencana. Gunakan bahasa Indonesia yang " +
                "ramah dan jelas. Jangan gunakan tanda bintang (*).";

            string finalPrompt =
                systemPrompt + "\n\n" +
                "Data sensor terkini:\n" + context + "\n\n" +
                "Pertanyaan pengguna: " + question;

            string response = await GetGeminiResponse(finalPrompt);
            return response.Replace("*", "");
        }

        private async Task<string> GetGeminiResponse(string userInput)
        {
            try
            {
                // Bangun request body
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            { new { text = userInput } }
                        }
                    }
                };

                string jsonPayload =
                    JsonSerializer.Serialize(requestBody);

                // Buat StringContent dan set ContentType secara eksplisit
                // (JANGAN pakai PostAsJsonAsync — tidak bisa set header)
                var content = new StringContent(
                    jsonPayload, Encoding.UTF8);
                content.Headers.ContentType =
                    new MediaTypeHeaderValue("application/json");

                // Buat HttpRequestMessage agar bisa tambah header
                using var request = new HttpRequestMessage(
                    HttpMethod.Post, Endpoint);
                request.Content = content;

                // ✅ KUNCI FIX: kirim API key via header x-goog-api-key
                // BUKAN via ?key= query param atau Authorization: Bearer
                request.Headers.Add("x-goog-api-key", ApiKey);

                // Kirim request
                var response =
                    await _httpClient.SendAsync(request);

                string responseBody =
                    await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine(
                    $"[Gemini] Status: {(int)response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Gemini] Error body: {responseBody}");
                    
                    // Fallback to local expert responder
                    return GenerateLocalFallbackResponse(userInput);
                }

                // Parse response
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                if (root.TryGetProperty(
                        "candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty(
                        "content", out var msgContent) &&
                    msgContent.TryGetProperty(
                        "parts", out var parts) &&
                    parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty(
                        "text", out var textElement))
                {
                    return textElement.GetString()
                           ?? "Maaf, respons VulkanoBot kosong.";
                }

                return GenerateLocalFallbackResponse(userInput);
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[Gemini] Timeout occurred. Using local fallback.");
                return GenerateLocalFallbackResponse(userInput);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Gemini] Exception: {ex.GetType().Name}: " +
                    $"{ex.Message}");
                return GenerateLocalFallbackResponse(userInput);
            }
        }

        private string GenerateLocalFallbackResponse(string userInput)
        {
            string question = "";
            string context = "";

            try
            {
                int qIdx = userInput.IndexOf("Pertanyaan pengguna:");
                if (qIdx != -1)
                {
                    question = userInput.Substring(qIdx + "Pertanyaan pengguna:".Length).Trim();
                }

                int cIdx = userInput.IndexOf("Data sensor terkini:\n");
                if (cIdx != -1 && qIdx != -1)
                {
                    int start = cIdx + "Data sensor terkini:\n".Length;
                    context = userInput.Substring(start, qIdx - start).Trim();
                }
            }
            catch
            {
                // Fallback parsing failed
            }

            if (string.IsNullOrEmpty(question))
            {
                question = userInput;
            }

            // Extract telemetry data from context
            string volcanoName = "Gunung Berapi";
            string status = "NORMAL";
            string location = "Indonesia";
            double so2 = 0.0;
            double temp = 0.0;
            double seismic = 0.0;

            try
            {
                // Parse lines of context:
                // Gunung: [Name]
                // Status Aktivitas: [Status]
                // Data Sensor -> SO2: [Val] ppm, Temp: [Val] C, Seismik: [Val]
                // Lokasi: [Location]
                var lines = context.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("Gunung:", StringComparison.OrdinalIgnoreCase))
                    {
                        volcanoName = line.Substring("Gunung:".Length).Trim();
                    }
                    else if (line.StartsWith("Status Aktivitas:", StringComparison.OrdinalIgnoreCase))
                    {
                        status = line.Substring("Status Aktivitas:".Length).Trim();
                    }
                    else if (line.Contains("Data Sensor ->"))
                    {
                        // Parse values
                        int so2Idx = line.IndexOf("SO2:");
                        int tempIdx = line.IndexOf("Temp:");
                        int seisIdx = line.IndexOf("Seismik:");
                        
                        if (so2Idx != -1)
                        {
                            int end = line.IndexOf("ppm", so2Idx);
                            if (end != -1)
                            {
                                string valStr = line.Substring(so2Idx + 4, end - (so2Idx + 4)).Trim();
                                double.TryParse(valStr, out so2);
                            }
                        }
                        if (tempIdx != -1)
                        {
                            int end = line.IndexOf("C", tempIdx);
                            if (end != -1)
                            {
                                string valStr = line.Substring(tempIdx + 5, end - (tempIdx + 5)).Trim();
                                double.TryParse(valStr, out temp);
                            }
                        }
                        if (seisIdx != -1)
                        {
                            string valStr = line.Substring(seisIdx + 8).Trim();
                            double.TryParse(valStr, out seismic);
                        }
                    }
                    else if (line.StartsWith("Lokasi:", StringComparison.OrdinalIgnoreCase))
                    {
                        location = line.Substring("Lokasi:".Length).Trim();
                    }
                }
            }
            catch
            {
                // Parsing context failed, use defaults
            }

            string qLower = question.ToLower();

            // Match keywords
            if (qLower.Contains("halo") || qLower.Contains("hi") || qLower.Contains("pagi") || qLower.Contains("siang") || qLower.Contains("sore") || qLower.Contains("malam") || qLower.Contains("helo") || qLower.Contains("assalamu"))
            {
                return $"Halo! Saya VulkanoBot, asisten AI pemantau aktivitas gunung berapi. Saya siap membantu menjawab pertanyaan Anda seputar status aktivitas, telemetri sensor, atau panduan mitigasi bencana untuk Gunung {volcanoName}. Ada yang ingin Anda tanyakan?";
            }

            if (qLower.Contains("status") || qLower.Contains("level") || qLower.Contains("kondisi"))
            {
                string statusExplanation = "";
                switch (status.ToUpper())
                {
                    case "AWAS":
                        statusExplanation = "Gunung dalam keadaan kritis dan erupsi dapat terjadi kapan saja. Ini adalah level tertinggi (Level IV).";
                        break;
                    case "SIAGA":
                        statusExplanation = "Terjadi peningkatan aktivitas seismik dan magmatik yang sangat signifikan (Level III). Erupsi cenderung segera terjadi.";
                        break;
                    case "WASPADA":
                        statusExplanation = "Menunjukkan aktivitas di atas level normal (Level II). Ada sedikit peningkatan aktivitas magmatik atau hembusan gas kawah.";
                        break;
                    default:
                        statusExplanation = "Gunung berada dalam kondisi tenang, stabil, dan aman tanpa indikasi peningkatan aktivitas vulkanik (Level I).";
                        break;
                }
                return $"Status aktivitas terbaru untuk Gunung {volcanoName} saat ini adalah {status.ToUpper()}. {statusExplanation} Harap selalu ikuti rekomendasi keselamatan dari pihak berwenang.";
            }

            if (qLower.Contains("so2") || qLower.Contains("gas") || qLower.Contains("belerang") || qLower.Contains("racun") || qLower.Contains("co2"))
            {
                string evaluation = so2 > 20.0 ? "sangat tinggi dan berbahaya bagi kesehatan paru-paru" : "relatif normal dan berada dalam ambang batas aman";
                return $"Kadar emisi gas Belerang Dioksida (SO₂) terbaru di Gunung {volcanoName} tercatat sebesar {so2:F1} ppm. Konsentrasi ini tergolong {evaluation}. Hindari menghirup gas kawah secara langsung dan selalu gunakan masker pelindung pernapasan di sekitar lereng.";
            }

            if (qLower.Contains("suhu") || qLower.Contains("panas") || qLower.Contains("temperatur"))
            {
                string thermalState = temp > 250.0 ? "sangat tinggi, menunjukkan tanda pergerakan magma yang aktif menuju permukaan" : "dalam batas wajar aktivitas fumarola kawah";
                return $"Suhu kawah Gunung {volcanoName} saat ini terdeteksi sebesar {temp:F1} °C. Kondisi termal ini dinilai {thermalState}. Selalu waspadai peningkatan suhu kawah yang mendadak.";
            }

            if (qLower.Contains("seismik") || qLower.Contains("gempa") || qLower.Contains("getaran") || qLower.Contains("tremor"))
            {
                string seismicState = seismic > 6.0 ? "cukup intensif dengan tremor vulkanik kontinu yang kuat" : "relatif rendah dan stabil";
                return $"Indeks aktivitas seismik Gunung {volcanoName} saat ini berada pada angka {seismic:F1}. Kegempaan dinilai {seismicState}. Gempa bumi mikro vulkanik terus dipantau secara berkala.";
            }

            if (qLower.Contains("mitigasi") || qLower.Contains("rekomendasi") || qLower.Contains("aman") || qLower.Contains("bahaya") || qLower.Contains("evakuasi") || qLower.Contains("jarak") || qLower.Contains("radius"))
            {
                switch (status.ToUpper())
                {
                    case "AWAS":
                        return $"PANDUAN KESELAMATAN (STATUS AWAS): Gunung {volcanoName} berada pada level bahaya tertinggi. Rekomendasi utama: \n1. Kosongkan seluruh area dalam radius minimal 6-7 km dari puncak.\n2. Segera evakuasi diri ke pos pengungsian BPBD terdekat melalui jalur evakuasi yang ditentukan.\n3. Gunakan masker wajah dan pelindung mata untuk menghindari abu vulkanik pekat.";
                    case "SIAGA":
                        return $"PANDUAN KESELAMATAN (STATUS SIAGA): Gunung {volcanoName} menunjukkan aktivitas kritis. Rekomendasi: \n1. Dilarang melakukan aktivitas pendakian atau wisata dalam radius 5 km dari puncak.\n2. Siapkan Tas Siaga Bencana berisi dokumen berharga, senter, air minum, obat-obatan, dan masker.\n3. Pantau terus update resmi dari BNPB atau Pos Pengamatan.";
                    case "WASPADA":
                        return $"PANDUAN KESELAMATAN (STATUS WASPADA): Aktivitas Gunung {volcanoName} berada di atas normal. Rekomendasi: \n1. Hindari mendekati kawah aktif dalam radius 2-3 km.\n2. Pendaki dihimbau untuk tetap waspada terhadap potensi hembusan gas beracun secara tiba-tiba.\n3. Siapkan masker pernapasan.";
                    default:
                        return $"PANDUAN KESELAMATAN (STATUS NORMAL): Gunung {volcanoName} aman dikunjungi. Rekomendasi: \n1. Aktivitas masyarakat dan wisata berjalan normal.\n2. Tetap ikuti arahan petugas jika cuaca ekstrem terjadi di area puncak.\n3. Selalu laporkan setiap aktivitas mencurigakan.";
                }
            }

            // Default fallback response
            return $"Halo! Saat ini Gunung {volcanoName} yang berlokasi di {location} memiliki status {status.ToUpper()}. Data pemantauan sensor terkini mencatat:\n- Emisi gas SO₂: {so2:F1} ppm\n- Suhu Termal Kawah: {temp:F1} °C\n- Aktivitas Seismik: {seismic:F1} Indeks\n\nUntuk keselamatan Anda, harap patuhi rekomendasi zona aman dan selalu gunakan masker jika tercium bau belerang yang menyengat.";
        }
    }
}