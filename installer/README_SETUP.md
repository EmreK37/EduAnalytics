# EduAnalytics Release Paketleme

Bu klasör setup üretimi için dosyaları içerir.

## Hazır paket üretimi

Repo kökünde:

```powershell
.\build-release.ps1
```

Çıktılar:

- `artifacts\publish\EduAnalytics`: yayınlanmış uygulama dosyaları
- `artifacts\installer\EduAnalytics-Portable-1.0.0.zip`: taşınabilir paket
- `artifacts\installer\EduAnalytics-Setup-1.0.0.exe`: Windows setup paketi

## Gereksinim

Uygulama self-contained yayınlanır, yani hedef bilgisayarda .NET kurulu olmak zorunda değildir.

Veritabanı için SQL Server Express LocalDB gerekir. LocalDB yoksa kurulum tamamlanır ama uygulama açılışta veritabanı hatası verebilir.

## Alternatif Inno Setup

Inno Setup kurulu bir bilgisayarda `installer\EduAnalytics.iss` dosyası derlenerek daha klasik bir setup paketi de üretilebilir.
