# Numbers Blast

Block Blast tarzı, sayı birleştirmeli bir bulmaca prototipi: yerleştirilen blok, eş değerli
komşularını kendine katar (zincirleme devam eder), dolan satır/sütunlar temizlenip skor verir.
Çekirdek oyunun yanında, inandırıcı bir AI rakibe karşı sahte gerçek-zamanlı bir multiplayer
modu içerir.

<p align="center">
  <img src="Docs/menu.png" width="180">&nbsp;
  <img src="Docs/solo.png" width="180">&nbsp;
  <img src="Docs/matchmaking.png" width="180">&nbsp;
  <img src="Docs/vsai.png" width="180">
</p>

**Unity 6000.3.8f1 (6.3 LTS)** · UGUI + yeni Input System · **Android, dikey** (Editor'de fare ile de oynanır)

## Nasıl çalıştırılır

1. Depoyu **Unity 6000.3.8f1** ile açın.
2. `Assets/Scenes/Game.unity` sahnesini açıp **Play**'e basın.
3. Testler: *Window ▸ General ▸ Test Runner* — 25 EditMode + 5 PlayMode.

## Mimari

Tek assembly; klasörler namespace'lerle bire bir eşleşir.

```
Core          Değişmez veri + enum'lar
Data          ScriptableObject'ler (board, parça şekilleri, tutorial adımları)
Gameplay      Saf mantık, UI'sız (board, tray, hamle pipeline'ı, skor)
App           Kompozisyon kökü: GameSessionController + oturum yardımcıları
Presentation  Görseller, animasyonlar, ortak yerleştirme önizlemesi
Input         PieceDragController (fare ve dokunma tek yol)
UI            Menü, skorboard, tur/sayaç, tutorial, oyun sonu, eşleştirme
Tutorial      3 adımlı zorunlu tutorial
Opponent      Maç döngüsü, hamle değerlendirici, insansı sunum
Settings      SFX / müzik / titreşim + ayarlar paneli
```

Tek doğruluk kaynağı **`PlacementService.ApplyMove`** (yerleştir → merge → temizle):
önizleme, gerçek hamle, AI değerlendirmesi ve fail-state aynı pipeline'ı kullanır —
gördüğünüz önizleme ile sonuç yapısal olarak ayrışamaz. `Gameplay` katmanı hiçbir
UI/Input tipini referans etmez.

## Öne çıkan kararlar

- **Önce merge, sonra clear** — sıra kuralın parçasıdır ve önizleme aynısını gösterir;
  skor yalnız satır/sütun temizliğinden gelir (kesişim bir kez sayılır).
- **Tray içeriği modeldedir** (`TrayModel`); parça tüketimi tek noktada ve yalnızca hamle
  doğrulandıktan sonra yapılır.
- **Part 2:** iki turda da görünen 20 sn sayaç, ekranda görünür timeout cezası, tamamen
  ekran üzerinde oynayan rakip; ayarlar maçı durdurmaz.
- **AI** oyuncuyla aynı pipeline'la değerlendirir; maç içi rubber-band maçları yakın tutar,
  bariz en iyi hamle asla pas geçilmez, sahte gezinme asla gerçek hamleden iyi görünmez.
- DI framework'ü, service locator, event bus, kullanılmayan interface yok.

Kararların gerekçeleri ve detaylı anlatım: **[README.pdf](README.pdf)**

## Bilinen notlar

- Tutorial yalnızca ilk açılışta çalışır; menüden tekrar oynatılabilir.
- Palet dışındaki merge değerleri kararlı bir golden-ratio tonu alır.
- Rakip turu tipik ~4–5 sn sürer; her zaman gösterdiği sayacın çok içinde biter.

## Gelecek geliştirmeler

Rubber-band eşiğine bağlı zorluk seçici · rakip turu için opsiyonel süre tavanı ·
uçan skor yazıları için havuzlama.
