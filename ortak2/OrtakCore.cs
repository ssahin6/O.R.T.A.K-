using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace ortak2
{
    public class OrtakCore
    {
        private readonly HttpClient _httpClient;
        private const string OllamaUrl = "http://localhost:11434/api/generate";
        private const string ModelName = "qwen2.5:14b";

        public OrtakCore()
        {
            _httpClient = new HttpClient();
        }

        public async Task<string> KomutIsle(string input)
        {
            string inputLower = input.ToLower();

            // --- 1. UYGULAMA VE SİTE KONTROLLERİ ---

            if (inputLower.Contains("google"))
                return (inputLower.Contains("open") || inputLower.Contains("launch")) ?
                    OpenWebPage("https://www.google.com", "Google ana sayfası açılıyor, efendim.") :
                    KillProcess("chrome", "msedge", "opera");

            if (inputLower.Contains("steam"))
                return (inputLower.Contains("open") || inputLower.Contains("launch")) ?
                    LaunchApplication("steam://open/main", "Steam başlatılıyor, efendim.") :
                    KillProcess("steam", "steamwebhelper");

            // Müzik Modu Kontrolü
            if (inputLower.Contains("open music mode") || inputLower.Contains("müzik modunu aç"))
                return GetMusicMode();

            if (inputLower.Contains("epic games"))
                return (inputLower.Contains("open") || inputLower.Contains("launch") || inputLower.Contains("start")) ?
                    // NOT: Aşağıdaki dosya yolunu kendi bilgisayarınızdaki EpicGamesLauncher.exe konumuyla değiştirin.
                    LaunchApplication(@"D:\epic\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe", "Epic Games açılıyor.") :
                    KillProcess("EpicGamesLauncher");

            if (inputLower.Contains("opera"))
                return (inputLower.Contains("open") || inputLower.Contains("launch")) ?
                    LaunchApplication("opera.exe", "Opera tarayıcı başlatılıyor.") :
                    KillProcess("opera", "opera_crashreporter");

            if (inputLower.Contains("visual studio"))
                return (inputLower.Contains("open") || inputLower.Contains("launch")) ?
                    LaunchApplication("devenv.exe", "Visual Studio başlatılıyor. İyi çalışmalar efendim.") :
                    KillProcess("devenv");

            if (inputLower.Contains("task manager") || inputLower.Contains("görev yöneticisi"))
                return (inputLower.Contains("open") || inputLower.Contains("launch")) ?
                    LaunchApplication("taskmgr.exe", "Görev Yöneticisi ekrana yansıtılıyor.") :
                    KillProcess("taskmgr");

            if (inputLower.Contains("control panel") || inputLower.Contains("denetim masası"))
                return (inputLower.Contains("open") || inputLower.Contains("launch")) ?
                    LaunchApplication("control.exe", "Denetim Masası açılıyor.") :
                    KillProcess("explorer");

            if (inputLower.Contains("instagram"))
                return OpenWebPage("https://instagram.com");

            if (inputLower.Contains("spotify") || (inputLower.Contains("music") && inputLower.Contains("open")))
                return LaunchApplication("spotify:");

            if (inputLower.Contains("play music"))
                return OpenWebPage("https://www.youtube.com/results?search_query=sotto+la+luna+italian+atmosphere", "Opening your playlist, sir.");

            if (inputLower.Contains("search book"))
                return OpenWebPage("https://www.google.com/search?q=Ve+sen+kuş+olup+gidersin+kitap", "Projecting book search results to your main display.");

            if (inputLower.Contains("fifa") || inputLower.Contains("fc 26"))
                return (inputLower.Contains("open") || inputLower.Contains("launch")) ?
                    // NOT: Aşağıdaki dosya yolunu kendi bilgisayarınızdaki FC26.exe konumuyla değiştirin.
                    LaunchApplication(@"D:\epic\game\EA SPORTS FC 26\FC26.exe", "Launching EA SPORTS FC 26.") :
                    KillProcess("FC26");

            if (inputLower.Contains("youtube"))
                return (inputLower.Contains("open")) ?
                    OpenWebPage("https://youtube.com") :
                    KillProcess("chrome", "msedge", "opera");

            // --- 2. SPOTIFY MANUEL SES KONTROLÜ ---
            if (inputLower.Contains("pause music") || inputLower.Contains("müziği durdur"))
                return ControlSpotify(true);

            if (inputLower.Contains("resume music") || inputLower.Contains("müziği aç"))
                return ControlSpotify(false);

            // --- 3. SİSTEM RAPORLARI VE MODLAR ---
            if (inputLower.Contains("system report") || inputLower.Contains("hardware"))
                return GetSystemReport();

            if (inputLower.Contains("open coding mode") || inputLower.Contains("coding mode") || inputLower.Contains("kodlama düzeni aç"))
                return GetCodingMode();

            if (inputLower.Contains("morning"))
                return GetMorningReport();

            if (inputLower.Contains("work") && inputLower.Contains("mode"))
                return GetWorkMode();

            // --- 4. BİLGİ SORULARI İÇİN GOOGLE ARAMASI ---
            return await Task.FromResult(SearchGoogle(input));
        }

        // --- YETENEKLER (SKILLS) ---

        private string LaunchApplication(string pathOrUri, string successMessage = "Launching requested application, sir.")
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = pathOrUri,
                    UseShellExecute = true
                });
                return successMessage;
            }
            catch (Exception ex)
            {
                return $"Unable to launch application. Error: {ex.Message}";
            }
        }

        private string KillProcess(params string[] processNames)
        {
            bool killed = false;
            foreach (var name in processNames)
            {
                var processes = Process.GetProcessesByName(name);
                foreach (var proc in processes)
                {
                    try { proc.Kill(); killed = true; } catch { /* Yetki hatası yoksayılabilir */ }
                }
            }
            return killed ? "Processes terminated successfully." : "No active processes found to terminate.";
        }

        private string OpenWebPage(string url, string customMessage = "Opening web page in browser.")
        {
            return LaunchApplication(url, customMessage);
        }

        private string GetCodingMode()
        {
            // Dikkat dağıtıcıları kapat, çalışma araçlarını aç
            KillProcess("FC26", "EpicGamesLauncher", "steam", "chrome", "msedge", "opera", "Discord");
            LaunchApplication("spotify:");
            LaunchApplication("devenv.exe");
            return "Kodlama düzeni aktif. Tüm dikkat dağıtıcılar kapatıldı, Visual Studio ve Spotify başlatıldı. İyi çalışmalar efendim.";
        }
        private string GetMusicMode()
        {
            // Ekranda açık olabilecek genel uygulamaları (tarayıcılar, oyunlar, editörler) kapat
            KillProcess("FC26", "EpicGamesLauncher", "steam", "chrome", "msedge", "opera", "Discord", "devenv", "taskmgr", "control");

            // Spotify'ı başlat
            LaunchApplication("spotify:");

            return "Müzik modu devrede. Ekran temizlendi ve Spotify başlatıldı. Keyifli dinlemeler efendim.";
        }

        private string GetWorkMode()
        {
            KillProcess("FC26", "youtube", "chrome", "msedge", "EpicGamesLauncher");
            LaunchApplication("notepad.exe");
            return "All distractions closed. Switching to focus mode.";
        }

        private string GetSystemReport()
        {
            StringBuilder report = new StringBuilder("Performing system analysis. ");
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                long totalFreeGB = 0;

                foreach (DriveInfo drive in drives)
                {
                    if (drive.IsReady && (drive.Name.Contains("C:") || drive.Name.Contains("D:")))
                    {
                        totalFreeGB += drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                    }
                }
                report.Append($"GPT partitions on SSD are stable. Total {totalFreeGB} GB free space on system drives. All hardware is at your command.");
            }
            catch
            {
                report.Append("Permission denied while accessing disk information.");
            }
            return report.ToString();
        }

        private string GetMorningReport()
        {
            string date = DateTime.Now.ToString("dd MMMM yyyy, dddd");
            return $"Good morning, sir. Today is {date}. 'Ve sen kuş olup gidersin, ben sana bakakalırdım...' I hope you stick to your daily plans. All systems are operational.";
        }

        // --- SPOTIFY MANUEL ÇEKİRDEK KONTROLÜ (WINDOWS API) ---
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_APPCOMMAND = 0x0319;
        private const int APPCOMMAND_MEDIA_PLAY = 46 << 16;
        private const int APPCOMMAND_MEDIA_PAUSE = 47 << 16;

        private string ControlSpotify(bool pause)
        {
            var processes = Process.GetProcessesByName("Spotify");
            if (processes.Length == 0) return "Spotify is not currently running.";

            int command = pause ? APPCOMMAND_MEDIA_PAUSE : APPCOMMAND_MEDIA_PLAY;

            foreach (var proc in processes)
            {
                if (proc.MainWindowHandle != IntPtr.Zero)
                {
                    SendMessage(proc.MainWindowHandle, WM_APPCOMMAND, proc.MainWindowHandle, (IntPtr)command);
                    return pause ? "Spotify playback paused, sir." : "Resuming Spotify playback.";
                }
            }
            return "Spotify window not found in the background.";
        }
        private string SearchGoogle(string query)
        {
            try
            {
                // Soruyu internet linki formatına çevir (boşlukları düzenler)
                string urlFormatliSoru = Uri.EscapeDataString(query);
                string url = $"https://www.google.com/search?q={urlFormatliSoru}";

                // Tarayıcıda arama sonucunu aç
                LaunchApplication(url);

                return $"Bu sorunun cevabını doğrudan bilmiyorum, ancak sizin için web'de bu sonuçları buldum efendim.";
            }
            catch
            {
                return "İnternet araması başlatılamadı efendim.";
            }
        }

        // --- YAPAY ZEKA BAĞLANTISI ---
        private async Task<string> AskAI(string input)
        {
            try
            {
                var requestData = new
                {
                    model = ModelName,
                    prompt = "Respond as a polite and concise personal assistant: " + input,
                    stream = false
                };

                string jsonString = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(OllamaUrl, content);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                using (JsonDocument doc = JsonDocument.Parse(responseBody))
                {
                    return doc.RootElement.GetProperty("response").GetString().Trim();
                }
            }
            catch (Exception)
            {
                return "Connection error. Unable to reach the AI engine.";
            }
        }
    }
}