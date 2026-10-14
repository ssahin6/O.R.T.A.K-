using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class GoogleTakvimEntegrasyonu
{
    static string[] Scopes = { CalendarService.Scope.CalendarEvents };
    static string ApplicationName = "Ortak Desktop";

    public async Task EtkinlikEkleAsync(string etkinlikAdi, DateTime baslangicZamani)
    {
        UserCredential credential;

        // 1. İndirdiğimiz credentials.json dosyasını okuyoruz
        using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
        {
            string credPath = "token.json";
            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, true));
        }

        // 2. Takvim servisini başlatıyoruz
        var service = new CalendarService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
        });

        // 3. Eklenecek etkinliğin detaylarını (Günü, Saati vb.) ayarlıyoruz
        Event yeniEtkinlik = new Event()
        {
            Summary = etkinlikAdi,
            Start = new EventDateTime()
            {
                DateTime = baslangicZamani,
                TimeZone = "Europe/Istanbul",
            },
            End = new EventDateTime() // Bitiş saatini varsayılan olarak 1 saat sonrası yapıyoruz
            {
                DateTime = baslangicZamani.AddHours(1),
                TimeZone = "Europe/Istanbul",
            },
        };

        // 4. Etkinliği takvime kaydediyoruz
        EventsResource.InsertRequest request = service.Events.Insert(yeniEtkinlik, "primary");
        Event createdEvent = await request.ExecuteAsync();

        Console.WriteLine($"Etkinlik Takvime Eklendi: {createdEvent.HtmlLink}");
    }

    public async Task<string> HaftalikPlaniGetirAsync()
    {
        UserCredential credential;

        // Aynı yetkilendirme dosyasıyla bağlanıyoruz
        using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
        {
            string credPath = "token.json";
            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, true));
        }

        var service = new CalendarService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
        });

        // Takvimden etkinlikleri çekeceğimiz ayarlar (Bugünden itibaren 7 gün)
        EventsResource.ListRequest request = service.Events.List("primary");
        request.TimeMin = DateTime.Now;
        request.TimeMax = DateTime.Now.AddDays(7); // Önümüzdeki 7 günü al
        request.ShowDeleted = false;
        request.SingleEvents = true;
        request.MaxResults = 15; // En fazla 15 etkinlik getir
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime; // Tarihe göre sırala

        Events events = await request.ExecuteAsync();

        // Gelen etkinlikleri güzel bir metin formatına çeviriyoruz
        if (events.Items != null && events.Items.Count > 0)
        {
            string planOzeti = "📅 Önümüzdeki 7 Günün Planı:\n";
            foreach (var eventItem in events.Items)
            {
                // Eğer etkinliğin belli bir saati varsa saatini al, yoksa sadece tarihini al
                string zaman = eventItem.Start.DateTime?.ToString("dd.MM.yyyy dddd HH:mm") ?? eventItem.Start.Date;
                planOzeti += $"• {zaman} -> {eventItem.Summary}\n";
            }
            return planOzeti;
        }
        else
        {
            return "Önümüzdeki 7 gün için planlanmış bir etkinlik bulunmuyor.";
        }
    }

   

}