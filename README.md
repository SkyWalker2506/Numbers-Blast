# Numbers Blast

Mavis Games **Senior Game Developer Case** için geliştirilmiş, Block Blast tarzında bir sayı birleştirme
bulmaca oyunu. Bloklar 1–4 arası sayılar taşır; yerleştirilen bir blok, kendisiyle aynı değerdeki
komşularını yutar (değerlerin toplamı alınır, zincirleme tepkimelerle) ve tamamen dolan
satırlar/sütunlar temizlenerek puan kazandırır. Bölüm 1 (çekirdek oyun) tamamlandı; opsiyonel
Bölüm 2 (inandırıcı bir yapay zekâya karşı sahte gerçek zamanlı çok oyunculu mod) de uygulandı.

<p align="center">
  <img src="Docs/menu.png" width="180">&nbsp;
  <img src="Docs/solo.png" width="180">&nbsp;
  <img src="Docs/matchmaking.png" width="180">&nbsp;
  <img src="Docs/vsai.png" width="180">
</p>

**Motor:** Unity 6000.3.8f1 (6.3 LTS), UGUI + TextMeshPro, yeni Input System · **Hedef:** Android, dikey (portrait) — Editör'de fare ile çalışır.

## Nasıl çalıştırılır

1. Bu depoyu **Unity 6000.3.8f1** ile açın.
2. `Assets/Scenes/Game.unity` sahnesini açın ve **Play** tuşuna basın.
3. Testler: *Window ▸ General ▸ Test Runner* — 24 EditMode + 5 PlayMode, tümü geçiyor.

## Mimari

Tek assembly; klasörler namespace'lerle 1:1 eşleşir.

```
Core          Immutable veriler + enum'lar (MoveResult, MergeStep, GameState, …)
Data          ScriptableObject'ler (tahta yapılandırması, parça şekilleri, öğretici adımları)
Gameplay      Saf mantık, UI'dan bağımsız: BoardModel, TrayModel, PlacementService,
              MergeResolver, LineClearResolver, ScoreService, PieceFactory
App           Composition root: GameSessionController + odaklanmış oturum yardımcıları
              (SessionHud, GameOverSequence, InputGate) + PlayerProgress
Presentation  Tahta/parça/tepsi görünümleri, animasyonlar, ortak yerleştirme önizlemesi
Input         PieceDragController (UGUI pointer olayları — fare ve dokunmatik tek yolu paylaşır)
UI            Menü, skor tablosu, tur/zamanlayıcı, öğretici katmanı, oyun sonu, matchmaking
Tutorial      3 adımlı zorunlu öğretici denetleyicisi
Opponent      TurnController (tek maç döngüsü), tur zamanlayıcısı, hamle değerlendirici,
              eylem planlayıcı, insansı sunum
Settings      SFX / müzik / titreşim + ayarlar paneli
```

**Tek pipeline.** `PlacementService.ApplyMove(board, piece, anchor)` — parçayı yerleştir, tüm
birleşmeleri çözümle, ardından dolu satırları/sütunları temizle — single source of truth'tur; canlı
sürükleme önizlemesi (geçici bir çalışma tahtası üzerinde), gerçek hamle, yapay zekânın aday
değerlendirmesi ve oyun sonu (fail-state) kontrolü hep bunu kullanır. Önizleme, yapay zekâ ve
gerçek hamle asla birbirinden farklı sonuç veremez. Saf `Gameplay` katmanı hiçbir UI veya input
tipine referans vermez; dolayısıyla mantık sınırı teamülle değil, namespace ile güvence altına alınmıştır.

## Tasarım tercihleri

- **Önce birleştir, sonra temizle** — sabit bir sırayla: parçayı yaz → birleşmeleri çözümle
  (4 komşulu, zincirleme) → satır/sütun temizlemelerini değerlendir. Bir birleşme, tamamlanmış
  görünen bir satırı yeniden boşaltabilir — kural budur ve önizleme de bunu gösterir. Birleşmeler
  puan getirmez; temizlemeler, temizlenen benzersiz değerlerin toplamı kadar puan getirir
  (satır∩sütun kesişimindeki hücre yalnızca bir kez sayılır).
- **Parça üretimi**, bir parçanın içindeki komşu hücrelere asla eşit değerler koymaz; böylece bir
  parça doğduğu anda kendi kendine birleşemez. Tepsi 3 parça tutar ve yalnızca tamamen
  boşaldığında yeniden dolar.
- **Bölüm 2, küçük tutarlılıklardan örülmüş bir illüzyondur:** ortak tahta ve tepsi, sahte bir
  "Finding opponent…" bağlanma ekranı, **her iki turda da görünür 20 saniyelik geri sayım** ile
  dönüşümlü turlar (rakibinki kendi renk tonunda akar), ekranda beliren "Time's up! −5%"
  uyarısıyla %5'lik süre aşımı cezası ve tamamen ekran üzerinde oynayan bir rakip — ortak
  tepsiden bir parça alır, aday hücrelerin üzerinde gezinir, duraksar, ara sıra yanlış yere bırakıp
  yeniden dener, sonra yerleştirir.
- **Yapay zekâ mükemmel hamleler değil, iyi hamleler yapmaya çalışır.** Her aday hamle aynı hamle
  pipeline'ıyla puanlanır; ardından en iyi adaylar arasından yapılan ağırlıklı-rastgele seçim onu
  insansı tutar. Maç içi bir rubber-band mekanizması, bu seçim aralığını anlık skor
  farkına göre genişletir ya da daraltır, böylece maçlar başa baş kalır; bariz biçimde en iyi hamle
  (bir satır/sütun temizleme) asla kaçırılmaz ve yapay zekâ, gerçekte yapacağı hamleden daha iyi
  görünecek bir hücre üzerinde asla sahte gezinme yapmaz.
- **Ayarlar maçı asla duraklatmaz** — gerçek bir çevrimiçi rakipte olduğu gibi saat işlemeye devam
  eder; panel yalnızca sizin girdinizi kilitler.
- DI framework'ü yok, service locator yok, event bus yok, kullanılmayan interface yok — bu ölçekte
  hepsi gereksiz tören olurdu. Projedeki her sınıf fiilen kullanılmaktadır.

## Bilinen sorunlar / notlar

- 3 adımlı öğretici yalnızca ilk açılışta çalışır (kalıcı olarak kaydedilir); menüden yeniden
  oynatılabilir.
- Elle tanımlanmış 8 renkli paletin ötesine geçen birleşmiş değerler, altın orana dayalı sabit bir
  renk tonu alır; böylece yüksek birleşme değerleri ayırt edilebilir ve okunur kalır.
- Rakibin turu genellikle ~3–4 saniye sürer; şanssız rastgele atışlar bunu birkaç saniye daha
  uzatabilir, ancak eylem listesi sonludur ve her zaman 20 saniyelik geri sayımın rahatça içinde
  tamamlanır.

## Gelecekteki iyileştirmeler

- Yapay zekânın rubber-band eşiğine bağlanmış bir zorluk seçici (eşik zaten Inspector'dan
  ayarlanabilir).
- Rakibin tur süresine isteğe bağlı kesin bir üst sınır.
- Uçan puan etiketleri için pooling (temizleme parıltıları zaten pool'lanıyor).
