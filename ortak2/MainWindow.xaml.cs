using System;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using System.Speech.Synthesis;
using System.Speech.Recognition;
using MailKit.Net.Imap;
using MailKit.Search;
using System.Net.NetworkInformation;
using System.Windows.Media;
using MailKit;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ortak2
{
    public partial class MainWindow : Window
    {
        private readonly OrtakCore _ortak;
        private readonly DispatcherTimer _zamanlayici;
        private DispatcherTimer _networkTimer;

        private PerformanceCounter _cpuSayaci;
        private PerformanceCounter _ramSayaci;

        private readonly SpeechSynthesizer _ortakSesi;
        private SpeechRecognitionEngine _ortakKulak;

        // --- OTOMATİK SES KONTROL DEĞİŞKENLERİ ---
        private DispatcherTimer _audioMonitorTimer;
        private bool _bizDurdurduk = false;
        private int _sessizlikSayaci = 0;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        private const uint WM_APPCOMMAND = 0x0319;
        private const int APPCOMMAND_MEDIA_PLAY = 46 << 16;
        private const int APPCOMMAND_MEDIA_PAUSE = 47 << 16;

        public MainWindow()
        {
            InitializeComponent();
            _ortak = new OrtakCore();
            txtCikti.Text = "O.R.T.A.K. Systems Online. Standing by for your command, sir...\n\n";

            // Ses Motoru Kurulumu
            _ortakSesi = new SpeechSynthesizer { Rate = 1, Volume = 100 };
            _ortakSesi.SetOutputToDefaultAudioDevice();
            _ortakSesi.SpeakAsync("Systems online. Standing by for your command, sir.");

            SesTanimayiBaslat();
            SensorleriBaslat();

            // Zamanlayıcılar
            _zamanlayici = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _zamanlayici.Tick += ArayuzGuncelle;
            _zamanlayici.Start();
            ArayuzGuncelle(null, null);

            StartNetworkMonitoring();
            _ = MailleriKontrolEt();

            // Otomatik Ses Takip Sistemini Başlat (Her 500 milisaniyede bir kontrol eder)
            _audioMonitorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _audioMonitorTimer.Tick += AudioMonitorTimer_Tick;
            _audioMonitorTimer.Start();
            TakvimiArayuzeYukle();
        }

        private void SensorleriBaslat()
        {
            try
            {
                _cpuSayaci = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramSayaci = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                _cpuSayaci.NextValue();
                _ramSayaci.NextValue();
            }
            catch (Exception ex)
            {
                txtCikti.Text += $"[SYSTEM WARNING]: Performance counters failed. ({ex.Message})\n\n";
            }
        }

        private void SesTanimayiBaslat()
        {
            try
            {
                _ortakKulak = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("en-US"));

                Choices komutlar = new Choices(new string[] {
                    "ortak", "play music", "search book",
                    "open instagram", "open youtube", "system report",
                    "open google", "close google",
                    "open steam", "close steam",
                    "open epic games", "close epic games", "start epic games",
                    "open opera", "close opera",
                    "open visual studio", "close visual studio",
                    "open task manager", "close task manager",
                    "open control panel",
                    "open coding mode",
                    "open music mode",
                    "pause music",
                    "resume music",
                });

                GrammarBuilder gb = new GrammarBuilder { Culture = new System.Globalization.CultureInfo("en-US") };
                gb.Append(komutlar);
                _ortakKulak.LoadGrammar(new Grammar(gb));
                _ortakKulak.SetInputToDefaultAudioDevice();

                _ortakKulak.AudioLevelUpdated += (s, e) =>
                {
                    if (e.AudioLevel > 10)
                        Dispatcher.Invoke(() => txtCikti.Text += $"-Voice detected: {e.AudioLevel}- ");
                };

                _ortakKulak.SpeechDetected += Ortak_SesAlgilandi;
                _ortakKulak.SpeechRecognized += Ortak_SesDuydu;
                _ortakKulak.SpeechRecognitionRejected += Ortak_SesAnlasilmadi;

                _ortakKulak.RecognizeAsync(RecognizeMode.Multiple);
                SetVoiceSensorState("LISTENING...", "#ffaa14");
            }
            catch (Exception ex)
            {
                txtCikti.Text += $"[SYSTEM WARNING]: Voice recognition failed. Error: {ex.Message}\n\n";
                SetVoiceSensorState("SENSOR ERROR", "#ff0000");
            }
        }

        private void ArayuzGuncelle(object sender, EventArgs e)
        {
            txtSaat.Text = DateTime.Now.ToString("HH:mm");
            txtTarih.Text = DateTime.Now.ToString("dd MMMM yyyy").ToUpper();

            if (_cpuSayaci != null && _ramSayaci != null)
            {
                try
                {
                    float cpuDeger = _cpuSayaci.NextValue();
                    float ramDeger = _ramSayaci.NextValue();
                    txtCpu.Text = $"% {cpuDeger:0.0}";
                    pbCpu.Value = cpuDeger;
                    txtRam.Text = $"% {ramDeger:0.0}";
                    pbRam.Value = ramDeger;
                }
                catch { }
            }

            try
            {
                DriveInfo drive = new DriveInfo("C");
                if (drive.IsReady)
                {
                    float diskUsagePercent = (float)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100;
                    txtDisk.Text = $"% {diskUsagePercent:0.0}";
                    pbDisk.Value = diskUsagePercent;
                }
            }
            catch { }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private async void txtGirdi_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string yazilanKomut = txtGirdi.Text.ToLower();

                // 1. TAKVİM EKLEME KONTROLÜ
                if (yazilanKomut.Contains("dersim var") || yazilanKomut.Contains("toplantım var") || yazilanKomut.Contains("planla"))
                {
                    await TakvimPlanlaAsync(yazilanKomut);
                    txtGirdi.Clear();
                    return;
                }

                // 2. TAKVİMİ OKUMA (GÖSTERME) KONTROLÜ
                if (yazilanKomut.Contains("planlarımı göster") || yazilanKomut.Contains("haftalık plan") || yazilanKomut.Contains("neler var"))
                {
                    _ortakSesi.SpeakAsync("Checking your calendar right away, sir.");

                    GoogleTakvimEntegrasyonu takvim = new GoogleTakvimEntegrasyonu();
                    string haftalikPlan = await takvim.HaftalikPlaniGetirAsync();

                    txtCikti.Text += $"\n{haftalikPlan}\n";

                    _ortakSesi.SpeakAsync("I have displayed your schedule for the next seven days on the screen, sir.");

                    txtGirdi.Clear();
                    return;
                }

                await IstekIsle();
            }
        }

        private async Task IstekIsle()
        {
            string istek = txtGirdi.Text.Trim();
            if (string.IsNullOrEmpty(istek)) return;

            txtCikti.Text += $"You: {istek}\n";
            txtGirdi.Clear();
            scrollCikti.ScrollToEnd();

            string cevap = await _ortak.KomutIsle(istek);

            txtCikti.Text += $"O.R.T.A.K: {cevap}\n\n";
            scrollCikti.ScrollToEnd();

            _ortakSesi.SpeakAsyncCancelAll();
            _ortakSesi.SpeakAsync(cevap);
        }

        private async Task MailleriKontrolEt()
        {
            try
            {
                txtMailListe.Text = "Connecting to server...";
                string sonMailler = "";
                int okunmamisSayisi = 0;

                await Task.Run(() =>
                {
                    System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                    using (var client = new ImapClient())
                    {
                        client.Connect("imap.gmail.com", 993, MailKit.Security.SecureSocketOptions.SslOnConnect);
                        client.Authenticate("ssahin.2152@gmail.com", "mkqu vyap twxm ruip");

                        client.Inbox.Open(FolderAccess.ReadOnly);
                        var okunmamislar = client.Inbox.Search(SearchQuery.NotSeen);
                        okunmamisSayisi = okunmamislar.Count;

                        int gosterilecekSayi = Math.Min(3, okunmamisSayisi);
                        for (int i = okunmamisSayisi - 1; i >= okunmamisSayisi - gosterilecekSayi; i--)
                        {
                            var mesaj = client.Inbox.GetMessage(okunmamislar[i]);
                            string kimden = mesaj.From[0].Name;
                            string konu = mesaj.Subject;

                            konu = konu.Length > 25 ? konu.Substring(0, 25) + "..." : konu;
                            kimden = kimden.Length > 15 ? kimden.Substring(0, 15) + "..." : kimden;

                            sonMailler += $"- {kimden}: {konu}\n";
                        }
                        client.Disconnect(true);
                    }
                });

                txtMailSayisi.Text = $"Unread: {okunmamisSayisi} Mails";
                txtMailListe.Text = okunmamisSayisi > 0 ? sonMailler : "All messages read.";
            }
            catch (Exception)
            {
                txtMailSayisi.Text = "Connection Error!";
                txtMailListe.Text = "Could not reach server.";
            }
        }

        private void StartNetworkMonitoring()
        {
            _networkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _networkTimer.Tick += async (s, e) => await UpdateNetworkStatusAsync();
            _networkTimer.Start();
        }

        private async Task UpdateNetworkStatusAsync()
        {
            if (NetworkInterface.GetIsNetworkAvailable())
            {
                long pingTime = await GetPingAsync("8.8.8.8");
                if (pingTime >= 0)
                {
                    txtNetworkStatus.Text = "SECURE";
                    txtNetworkStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#00ff96");
                    txtLatency.Text = $"{pingTime} ms";
                    txtLatency.Foreground = pingTime > 100 ? Brushes.Orange : (Brush)new BrushConverter().ConvertFrom("#00ff96");
                    return;
                }
            }
            NetworkErrorState();
        }

        private void NetworkErrorState()
        {
            txtNetworkStatus.Text = "OFFLINE";
            txtNetworkStatus.Foreground = Brushes.Red;
            txtLatency.Text = "--- ms";
            txtLatency.Foreground = Brushes.Red;
        }

        private async Task<long> GetPingAsync(string address)
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = await ping.SendPingAsync(address, 1000);
                    return reply.Status == IPStatus.Success ? reply.RoundtripTime : -1;
                }
            }
            catch { return -1; }
        }

        private void Ortak_SesAlgilandi(object sender, SpeechDetectedEventArgs e)
        {
            Dispatcher.Invoke(() => SetVoiceSensorState("PROCESSING VOICE...", "#00ff96"));
        }

        private async void Ortak_SesDuydu(object sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result.Confidence > 0.50f)
            {
                string algilananKomut = e.Result.Text.ToLower();
                txtGirdi.Text = algilananKomut;

                txtCikti.Text += $"\n[TEST] {algilananKomut} - Score: {e.Result.Confidence}\n";

                if (algilananKomut == "ortak")
                {
                    txtCikti.Text += $"\n[YOU]: {algilananKomut}\n";
                    _ortakSesi.SpeakAsync("Systems are online. I am listening, sir.");
                    txtGirdi.Clear();
                }
            }

            Task.Delay(1500).ContinueWith(_ => Dispatcher.Invoke(() => SetVoiceSensorState("LISTENING...", "#ffaa14")));

            if (e.Result.Confidence > 0.5f && e.Result.Text.ToLower() != "ortak")
            {
                await IstekIsle();
            }
        }

        private async Task<string> ArkaPlandaOllamayaSor(string prompt)
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(5);

                string url = "http://localhost:11434/api/generate";

                var istekGövdesi = new
                {
                    model = "qwen2.5:14b",
                    prompt = prompt,
                    stream = false
                };

                string json = JsonSerializer.Serialize(istekGövdesi);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                string jsonCevap = await response.Content.ReadAsStringAsync();

                using (JsonDocument doc = JsonDocument.Parse(jsonCevap))
                {
                    return doc.RootElement.GetProperty("response").GetString();
                }
            }
        }

        private async Task TakvimPlanlaAsync(string algilananCumle)
        {
            _ortakSesi.SpeakAsync("Adding it to your calendar immediately. Please wait, sir.");

            try
            {
                string gunSozlugu = $"bugün: {DateTime.Now.ToString("yyyy-MM-dd")}\n";
                gunSozlugu += $"yarın: {DateTime.Now.AddDays(1).ToString("yyyy-MM-dd")}\n";
                for (int i = 0; i <= 7; i++)
                {
                    gunSozlugu += $"{DateTime.Now.AddDays(i).ToString("dddd").ToLower()}: {DateTime.Now.AddDays(i).ToString("yyyy-MM-dd")}\n";
                }

                string prompt = $@"Kullanıcı metni: '{algilananCumle}'

Sen bir tarih çözümleyicisin. Kullanıcının bahsettiği günün tarihini KESİNLİKLE aşağıdaki sözlükten EŞLEŞTİREREK bul. Asla kendi kendine tarih hesaplama veya uydurma:
{gunSozlugu}

Lütfen metinden etkinliğin adını ve saatini bul (Saat belirtilmemişse 09:00 yap). 
SADECE şu formatta yanıt ver, başka hiçbir kelime ekleme:
ETKİNLİK: [Etkinlik Adı]
TARİH: [Sözlükten Bulduğun Tarih HH:mm]";

                string ollamaYaniti = await ArkaPlandaOllamayaSor(prompt);
                ollamaYaniti = ollamaYaniti.Trim();

                txtCikti.Text += $"\n[OLLAMA CALENDAR PARSE]:\n{ollamaYaniti}\n";

                string[] satirlar = ollamaYaniti.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                if (satirlar.Length >= 2)
                {
                    string etkinlikAdi = satirlar[0].Replace("ETKİNLİK:", "").Trim();
                    string tarihMetni = satirlar[1].Replace("TARİH:", "").Trim();

                    DateTime planlananTarih = DateTime.Parse(tarihMetni);

                    GoogleTakvimEntegrasyonu takvim = new GoogleTakvimEntegrasyonu();
                    await takvim.EtkinlikEkleAsync(etkinlikAdi, planlananTarih);

                    _ortakSesi.SpeakAsync($"The event '{etkinlikAdi}' has been successfully added to your calendar, sir.");
                    txtCikti.Text += $"\n[SYSTEM]: Successfully added to calendar!\n";
                }
                else
                {
                    _ortakSesi.SpeakAsync("I couldn't extract a valid date from your sentence. Please specify the time and day more clearly, sir.");
                }
            }
            catch (Exception ex)
            {
                _ortakSesi.SpeakAsync("An error occurred while accessing the calendar. Please check your connection, sir.");
                txtCikti.Text += $"\n[ERROR]: {ex.Message}\n";
            }
        }

        private void Ortak_SesAnlasilmadi(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                SetVoiceSensorState("UNKNOWN VOICE", "#ff3366");
                Task.Delay(1500).ContinueWith(_ => Dispatcher.Invoke(() => SetVoiceSensorState("LISTENING...", "#ffaa14")));
            });
        }

        private void SetVoiceSensorState(string statusText, string hexColor)
        {
            var color = (Color)ColorConverter.ConvertFromString(hexColor);
            var brush = new SolidColorBrush(color);

            txtVoiceStatus.Text = statusText;
            txtVoiceStatus.Foreground = brush;
            imgVoiceSensor.Fill = brush;
            effectVoiceSensor.Color = color;
        }

        private void AudioMonitorTimer_Tick(object sender, EventArgs e)
        {
            bool tarayicidanSesGeliyorMu = false;

            try
            {
                using (var enumerator = new MMDeviceEnumerator())
                using (var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
                {
                    var sessions = device.AudioSessionManager.Sessions;

                    for (int i = 0; i < sessions.Count; i++)
                    {
                        using (var session = sessions[i])
                        {
                            try
                            {
                                var proc = Process.GetProcessById((int)session.GetProcessID);
                                string pName = proc.ProcessName.ToLower();

                                List<string> yokSayilacakUygulamalar = new List<string> { "spotify", "ortak2", "fc26", "audiodg", "Spotify" };

                                if (!yokSayilacakUygulamalar.Contains(pName))
                                {
                                    if (session.AudioMeterInformation.MasterPeakValue > 0.01f)
                                    {
                                        tarayicidanSesGeliyorMu = true;
                                        break;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }

                if (tarayicidanSesGeliyorMu)
                {
                    _sessizlikSayaci = 0;

                    if (!_bizDurdurduk)
                    {
                        SpotifyMedyaKontrol(true);
                        _bizDurdurduk = true;
                    }
                }
                else
                {
                    if (_bizDurdurduk)
                    {
                        _sessizlikSayaci++;

                        if (_sessizlikSayaci >= 3)
                        {
                            SpotifyMedyaKontrol(false);
                            _bizDurdurduk = false;
                            _sessizlikSayaci = 0;
                        }
                    }
                }
            }
            catch { }
        }

        private void SpotifyMedyaKontrol(bool pause)
        {
            var processes = Process.GetProcessesByName("Spotify");
            if (processes.Length == 0) return;

            int command = pause ? APPCOMMAND_MEDIA_PAUSE : APPCOMMAND_MEDIA_PLAY;

            foreach (var proc in processes)
            {
                if (proc.MainWindowHandle != IntPtr.Zero)
                {
                    SendMessage(proc.MainWindowHandle, WM_APPCOMMAND, proc.MainWindowHandle, (IntPtr)command);
                    break;
                }
            }
        }

        private async void TakvimiArayuzeYukle()
        {
            try
            {
                txtTakvimArayuzu.Text = "Fetching calendar data...";

                GoogleTakvimEntegrasyonu takvim = new GoogleTakvimEntegrasyonu();
                string planlar = await takvim.HaftalikPlaniGetirAsync();

                txtTakvimArayuzu.Text = planlar;
            }
            catch (Exception)
            {
                txtTakvimArayuzu.Text = "Calendar connection error.";
            }
        }
    }
}