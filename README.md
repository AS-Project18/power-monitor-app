# PC Power Monitor — Tray App

Aplikasi WPF (.NET, C#) yang jalan di background sebagai **system tray icon**
untuk estimasi konsumsi daya PC (CPU + GPU) dan biaya listriknya, tanpa alat
tambahan — murni baca sensor internal hardware.

## Cara menjalankan (di Windows, lewat Claude Code atau Visual Studio)

1. Pastikan **.NET SDK** terpasang (`dotnet --version` untuk cek).
2. Buka folder project ini, lalu:
   ```
   dotnet restore
   dotnet run
   ```
3. **Jalankan sebagai Administrator** (app sudah minta elevasi otomatis lewat
   `app.manifest`). LibreHardwareMonitorLib butuh akses driver kernel untuk
   baca sensor RAPL/MSR CPU dan sebagian sensor GPU — tanpa admin, sebagian
   angka bisa muncul 0.
4. Tidak ada window yang terbuka — app langsung muncul sebagai **icon di
   system tray**.

## Cara pakai

- **Klik kiri** icon tray → muncul popup kecil berisi watt CPU/GPU saat ini,
  akumulasi kWh hari ini, dan biayanya. Klik di luar popup untuk menutupnya.
- **Klik kanan** icon tray → menu:
  - **Pengaturan...** — ubah tarif per kWh, daya idle/maksimum CPU (untuk
    estimasi), interval polling, dan retensi data histori dalam satu jendela.
    Berlaku langsung (tanpa restart) dan tersimpan ke `config.json`.
  - **Lihat Riwayat...** — bar chart tren + rekap kWh & biaya, dengan tab
    **Harian** (14 hari terakhir) dan **Bulanan** (6 bulan terakhir, cocok
    buat dibandingkan sama siklus tagihan PLN). Juga bisa dibuka dari tombol
    "📊 Riwayat" di popup.
  - **Jalankan saat Windows Start** — daftarkan/hapus scheduled task supaya
    app otomatis jalan tiap login (tanpa prompt UAC, lihat `StartupService`).
  - **Diagnostik Sensor...** — dump mentah semua hardware & sensor yang
    terdeteksi LibreHardwareMonitorLib beserta nilainya. Pakai ini kalau ada
    angka watt yang 0 terus/aneh (lihat bagian Troubleshooting).
  - **Keluar** — menutup aplikasi.

## Struktur project

```
PowerMonitorApp/
├── App.xaml / App.xaml.cs         # entry point WPF, wiring services + tray
├── config.json                    # tarif, interval polling, retensi data
├── app.manifest                   # request admin privilege
├── Assets/
│   └── app.ico                    # icon exe + tray (ApplicationIcon di .csproj)
├── Models/
│   ├── PowerReading.cs            # data satu titik pembacaan
│   ├── DailySummary.cs            # rekap kWh per hari
│   ├── MonthlySummary.cs          # rekap kWh per bulan
│   └── AppConfig.cs               # konfigurasi aplikasi
├── Services/
│   ├── PowerSensorService.cs      # baca sensor CPU/GPU via LibreHardwareMonitorLib
│   ├── EnergyCalculatorService.cs # integrasi watt -> kWh terhadap waktu
│   ├── CostCalculatorService.cs   # kWh -> biaya (Rp)
│   ├── DatabaseService.cs         # log histori + rekap harian/bulanan + purge ke SQLite
│   ├── ConfigService.cs           # baca/tulis config.json
│   └── StartupService.cs          # daftar/hapus scheduled task auto-start
└── Tray/
    ├── TrayIconController.cs      # NotifyIcon, polling timer, popup, menu
    ├── PopupWindow.xaml(.cs)      # popup info watt/kWh/biaya
    ├── SettingsWindow.xaml(.cs)   # dialog pengaturan (tarif, daya idle/maks CPU, interval, retensi)
    ├── HistoryWindow.xaml(.cs)    # rekap harian & bulanan (tab + chart + list)
    └── DiagnosticsWindow.xaml(.cs) # dump sensor mentah untuk troubleshooting
```

## Yang perlu diketahui (keterbatasan)

Ini estimasi berbasis **CPU package power + GPU power** yang dilaporkan sensor
internal hardware. Komponen lain (motherboard, storage, fan, kerugian efisiensi
PSU, monitor eksternal) **tidak ikut terhitung**. Untuk PC desktop biasa, ini
biasanya jadi porsi terbesar dari beban kerja berat (gaming/render/compile),
tapi bukan angka total dari stopkontak — beda dengan kalau pakai smart plug.

Akumulasi "hari ini" dihitung ulang dari histori tersimpan di SQLite
(`DatabaseService.GetTotalKwhSince(DateTime.Today)`), jadi tetap akurat
walaupun app di-restart di tengah hari. Histori yang lebih tua dari
`RetentionDays` di `config.json` (default 90 hari) dihapus otomatis sekali
sehari supaya `power_monitor.db` tidak membengkak tanpa batas.

## Troubleshooting: watt CPU/GPU 0 atau tidak masuk akal

Popup membedakan tiga kondisi untuk CPU:
- **Angka biasa** (mis. "45.2 W") — sensor Power CPU terbaca normal.
- **"≈45.2 W"** (ada tanda `≈` + catatan kuning di bawah popup) — sensor
  Power CPU tidak terbaca/nyangkut di 0, jadi watt-nya **diestimasi**.
  Diatur lewat klik kanan tray → **Pengaturan...** (default: idle 15W,
  maksimum 65W — **sesuaikan dengan CPU kamu yang sebenarnya**, lihat
  penjelasan model di bawah). Ini fallback otomatis di `PowerSensorService`.
- **"N/A"** — sensor Power tidak ada dan sensor Load CPU juga tidak
  ditemukan, jadi tidak ada dasar sama sekali untuk estimasi.

**Model estimasi**: `watt = daya_idle + (Load% core tersibuk / 100) x (daya_maksimum - daya_idle)`.
Basisnya sengaja pakai **"CPU Core Max"** (load core yang paling sibuk),
bukan **"CPU Total"** (rata-rata semua thread) — karena workload sehari-hari
sering cuma nge-load 1-2 thread yang boost tinggi sementara thread lain
nganggur. Rata-rata dari situ jadi kelihatan kecil (mis. 6-10%) padahal daya
rielnya udah signifikan karena core yang aktif itu boost penuh; "Core Max"
jauh lebih dekat merepresentasikan kondisi itu. "Daya maksimum" di
Pengaturan boleh lebih tinggi dari TDP resmi CPU-nya — AMD di platform AM5
umumnya boost sampai PPT ~1.3x TDP (CPU 65W TDP bisa boost ke ~85-90W).
Tetap ini pendekatan kasar (linear interpolation, bukan model boost/kurva
daya asli), jadi kalibrasi manual di Pengaturan (bandingkan dengan software
lain seperti software AIO cooler/HWiNFO) akan selalu lebih akurat daripada
default.

GPU tidak punya fallback estimasi (belum diperlukan — NVML/ADL/iGPU driver
biasanya cukup andal); kalau GPU juga menunjukkan `N/A`, cek **Diagnostik
Sensor...**.

Akar masalah paling umum untuk CPU: pembacaan power CPU asli butuh akses
**RAPL (Intel) / SMU (AMD) lewat driver kernel**, sedangkan GPU biasanya
dibaca lewat library user-mode vendor (NVML dkk) yang tidak butuh driver
itu — jadi GPU bisa tetap kebaca normal walau CPU tidak. Beberapa chip AMD
generasi baru (contoh yang sudah dikonfirmasi: **Ryzen 7 8700F** — sensor
`Power Package` & `Temperature Tctl/Tdie` selalu 0.00 walau `Load` per-core
kebaca normal) belum punya dukungan SMU lengkap di LibreHardwareMonitorLib
(termasuk versi prerelease terbaru per Agustus 2026) — ini keterbatasan
upstream, bukan sesuatu yang bisa diperbaiki dari sisi aplikasi ini, makanya
ditangani lewat model estimasi di atas.

Langkah cek kalau ketemu kasus serupa:
1. Klik kanan tray → **Diagnostik Sensor...**, lihat apakah sensor `Power`
   untuk CPU ada tapi nilainya 0.00 terus (⇒ limitasi SMU seperti di atas,
   fallback estimasi otomatis aktif) atau memang tidak ada sensor `Power`
   sama sekali.
2. Kalau Windows **Memory Integrity / Core Isolation** aktif (Windows
   Security → Device security → Core isolation), driver Ring0 legacy bisa
   gagal load sama sekali — beda gejala dari kasus di atas: biasanya ini
   bikin *lebih banyak* sensor hilang (termasuk temperature/clock), bukan
   cuma Power yang nyangkut 0.
3. Angka GPU yang terlihat "besar" walau lagi idle biasanya **bukan bug** —
   GPU modern (terutama NVIDIA) tetap menarik daya idle yang cukup
   signifikan (VRAM refresh, display output, fan controller, dsb).
   Bandingkan dengan software lain (GPU-Z, HWiNFO) kalau ragu.

## Ide pengembangan lanjut

- **Tarif progresif**: kalau mau lebih presisi sesuai skema tarif PLN
  (blok konsumsi, biaya beban bulanan), `CostCalculatorService` bisa diperluas
  jadi tiered pricing daripada flat rate per kWh.

## Kontribusi

Kontribusi/PR dipersilakan. Beberapa panduan supaya konsisten dengan struktur
yang ada:

- **Pembagian tanggung jawab**: logic murni (baca sensor, hitung kWh/biaya,
  simpan/query data) tinggal di `Services/` dan tidak boleh bergantung ke
  WPF/WinForms — supaya tetap gampang ditest/dipakai ulang di luar UI tray.
  Kode UI (window, tray icon, event handling) tinggal di `Tray/`.
- **Jangan tambah dependency** kalau bisa diselesaikan dengan yang sudah ada
  (`LibreHardwareMonitorLib`, `Microsoft.Data.Sqlite`) — project ini sengaja
  dijaga ringan.
- **Styling UI**: semua window pakai tema gelap custom (lihat `Window.Resources`
  di tiap `.xaml`, palet warna intinya `#FF181818`/`#FF232323` untuk
  background, `#FF4FC3F7` untuk accent) — ikuti pola yang sama biar konsisten,
  jangan pakai kontrol default WPF yang temanya terang.
- **Testing manual**: karena ini GUI app, belum ada automated test. Sebelum
  submit PR, jalankan `dotnet build` (harus bersih tanpa warning) dan coba
  jalankan app-nya (`dotnet run`, sebagai Administrator) untuk pastikan
  perubahan kamu jalan — tray icon muncul, popup/jendela terkait kebuka
  normal, dan skenario yang kamu ubah beneran teruji.
- Kalau nambah field config baru di `AppConfig`, kasih default value yang
  aman (property initializer di C#) supaya `config.json` lama punya user lain
  tetap kompatibel tanpa perlu migrasi manual.

Alur standar: fork → branch baru dari `main` → commit → buka PR dengan
deskripsi singkat kenapa perubahannya diperlukan.

## Lisensi

[MIT](LICENSE) — bebas dipakai, dimodifikasi, dan didistribusikan ulang
(termasuk untuk keperluan komersial), asal notice lisensinya tetap disertakan.
