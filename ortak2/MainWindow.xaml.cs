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
                        client.Authenticate("YOUR_EMAIL_HERE", "YOUR_APP_PASSWORD_HERE");

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
            if (e.Result.Confidence > 0.01f)
            {
                string algilananKomut = e.Result.Text.ToLower();
                txtGirdi.Text = algilananKomut;

                if (algilananKomut == "ortak")
                {
                    txtCikti.Text += $"\n[SEN]: {algilananKomut}\n";
                    _ortakSesi.SpeakAsync("Sistemler devrede. Sizi dinliyorum.");
                    txtGirdi.Clear();
                }
            }

            Task.Delay(1500).ContinueWith(_ => Dispatcher.Invoke(() => SetVoiceSensorState("LISTENING...", "#ffaa14")));

            if (e.Result.Confidence > 0.5f && e.Result.Text.ToLower() != "ortak")
            {
                await IstekIsle();
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

                                if (pName == "chrome" || pName == "opera" || pName == "msedge")
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

                // --- GÜNCELLENEN KARAR MEKANİZMASI (SESSİZLİK TOLERANSI) ---
                if (tarayicidanSesGeliyorMu)
                {
                    _sessizlikSayaci = 0; // Ses duyulduğu an sayacı sıfırla

                    if (!_bizDurdurduk)
                    {
                        SpotifyMedyaKontrol(true); // Spotify'ı durdur
                        _bizDurdurduk = true;
                    }
                }
                else
                {
                    // Ses kesildiyse ve Spotify'ı biz durdurduysak bekleme sürecini başlat
                    if (_bizDurdurduk)
                    {
                        _sessizlikSayaci++;

                        // Zamanlayıcımız 500ms (0.5 saniye) olduğu için 6 döngü = 3 saniye eder.
                        // 3 Saniye boyunca kesintisiz sessizlik olursa müziği başlatır.
                        if (_sessizlikSayaci >= 6)
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
    }
}