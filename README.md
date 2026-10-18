# PlayArchive

Windows için WPF oyun kütüphanesi ve oyun oturumu takip uygulaması. Yerel oyunları tarar, kütüphaneyi yönetir ve oyun sürelerini SQLite veritabanında saklar.

## Çalıştırma

Windows 10 (19041 veya üzeri) ve .NET 9 SDK gerekir. Proje klasöründe:

```powershell
dotnet run --project PlayArchive.csproj
```

NuGet bağımlılıkları ilk çalıştırmada geri yüklenir. Kullanıcıya ait yerel veritabanı `%LOCALAPPDATA%\PlayArchive\PlayArchive.db` konumunda oluşturulur. Veritabanı, derleme çıktıları ve yerel inceleme raporları bu depoya dahil edilmemiştir.
