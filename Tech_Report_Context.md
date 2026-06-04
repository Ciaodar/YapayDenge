# YapayDenge - Sistem Mimarisı ve Teknik Analiz Dokümanı (STD & Final Raporu Bağlamı)

Bu kapsamlı belge, **YapayDenge** projesinin ardındaki teknik altyapıyı, oyun tasarım felsefesini, Üretken Yapay Zeka (Generative AI) entegrasyon stratejilerini ve yazılım geliştirme sürecinde karşılaşılan mühendislik engellerinin nasıl aşıldığını detaylandırmak üzere hazırlanmıştır. Amacı, projenin Final Raporu, Yazılım Teknik Tanımlaması (STD) veya bitirme tezlerinde akademik ve endüstriyel standartlarda kullanılacak bir bağlam (context) sağlamaktır.

---

## 1. Proje Vizyonu ve "Ciddi Oyun" (Serious Game) Tasarımı

YapayDenge, eğlence odaklı klasik oyunların aksine, oyunculara karmaşık kriz yönetimi, sürdürülebilirlik, politik denge ve sosyoekonomik ödünleşim (trade-off) kavramlarını öğretmeyi amaçlayan bir "Ciddi Oyun" (Serious Game) projesidir. Oyuncu bir "Başkan" rolünü üstlenir ve üç temel fraksiyonu dengede tutmaya çalışır:
- **Ekonomi (Economy)**
- **Çevre (Environment)**
- **Halk / Toplum (Society)**

Oyunun temel felsefesi "Sınırsız büyüme felaket getirir" ilkesidir. Herhangi bir değer 0'a ulaştığında veya toplam değer 170'i aştığında (aşırı yüklenme/kapitalizm krizi) oyun sona erer. Doğru ve yanlış seçenek yoktur; her kazancın bedel olarak başka bir fraksiyondan eksilttiği acımasız bir matematik modeli vardır.

---

## 2. Sistem Mimarisi ve İletişim Akışı

YapayDenge, olay ağaçlarını önceden yazmak (hardcoding) yerine, çalışma zamanında (runtime) **Google Gemini API (gemini-3.1-flash-lite)** kullanarak sonsuz varyasyonda üretir. Sistem Modüler Mimari prensipleriyle (Decoupled Systems) tasarlanmıştır:

1. **`GenAIService.cs` (LLM İletişim Katmanı):**
   - Oyun içi fraksiyon verilerini ve geçmişte yapılan seçimleri (Context History) bir araya getirerek spesifik bir "Prompt" (İstem) inşa eder.
   - HTTP POST protokolü ile Gemini API'ye asenkron istek atar.
   - Dönen JSON verisini temizler ve ham metinden okunabilir JSON string yapısına çevirir.

2. **`JsonToSoConverter.cs` (Veri Dönüşüm ve Adaptör Katmanı):**
   - API'den dönen karmaşık JSON metnini, Unity'nin yerel C# nesnelerine (DTO - Data Transfer Object) serileştirir.
   - Bu DTO'ları bellekte yaşayabilen ve Unity hiyerarşisine oturan `EventCardSO` (ScriptableObject) yapılarına çevirir. (Adapter Pattern kullanılmıştır).

3. **`DecisionTreeController.cs` (Oyun Yöneticisi / Game Loop):**
   - Sistemdeki ana karar ağacını barındırır.
   - Oyuncunun tıklamalarını dinler, ağaçta ilerlemeyi sağlar ve dalların sonunda yeni bir API isteğinin tetiklenmesine karar verir.

4. **`FactionManager.cs` & `UIManager.cs` (Oyun Mantığı ve Arayüz):**
   - Puanların toplanması, çürüme (Entropy) hesaplamaları ve oyun sonu (End Game) kontrollerini yapar.
   - **Observer Pattern** (Olay Güdümlü - Event Driven) mimarisi ile çalışır. Puanlar değiştiğinde `OnFactionChange` olayı tetiklenir ve arayüzler (Slider'lar, Şehir Görselleri) Update fonksiyonu (polling) kullanmadan kendilerini günceller.

---

## 3. Generative AI (Üretken Yapay Zeka) Entegrasyon Stratejisi

Yapay zekanın oyun motoru içinde istikrarlı ve oyun kurallarına bağlı kalabilmesi için gelişmiş Prompt Mühendisliği (Prompt Engineering) kullanılmıştır:

- **Few-Shot Prompting ve JSON Şablonlaması:** LLM'ler doğal dil üretiminde iyidir ancak veri yapısı çıktısında hata yapabilirler. Bunu önlemek için promptun içine tam 2 derinlikli ve KESİNLİKLE her düğümünde 4'er seçenek barındıran katı bir JSON şablonu (template) verilerek model format uymaya zorlanmıştır.
- **Dinamik Kurallar (Dynamic Prompts):** Ekonomi, Çevre veya Halk 30'un altına indiğinde veya 80'in üstüne çıktığında, prompt dinamik olarak güncellenir ve LLM'e "Halk isyanın eşiğinde, onları yatıştırmak için çevre ve ekonomiyi katledecek krizler üret" gibi özel bağlamsal direktifler verilir.
- **Rol Yapma (Roleplay) Baskısı:** Modelin "Fabrika patladı" gibi jenerik laflar üretmesini engellemek için, kurgusal isimler (Adalet Bakanı Charles) ve somut siyasi entrikalar yaratması emredilmiştir.
- **Bilimsel Çerçeve (Ground Truth):** LLM'in tamamen hayal kurmasını engellemek için, oyunun içindeki 100 farklı bilimsel senaryo veritabanından her seferinde rastgele 5 tanesi seçilerek prompta "Bilimsel Referans (Ground Truth)" olarak eklenir ve kararların matematiksel ağırlıkları buna dayandırılır.

---

## 4. Teknik Mühendislik Engelleri ve Çözümleri

### 4.1. Gecikme (Latency) Maliyetine Karşı "Nested (İç İçe) JSON" Mimarisi
- **Sorun:** LLM API istekleri ortalama 3-6 saniye sürmektedir. Her karardan sonra 5 saniye beklemek oyun deneyimini (UX) yok eder.
- **Çözüm:** Sistem LLM'den sadece 1 olay değil, "2 Derinlikli" (Depth-2) tam teşekküllü bir karar ağacı ister. Bu ağaç; 1 ana olay, onun 4 seçeneği ve o 4 seçeneğin de 4'er alt olayı şeklinde dallanır. Böylece API'den tek seferde çekilen ağaç sayesinde oyuncu ardışık iki kararı SIFIR gecikme (0 ms) ile alır. Yükleme ekranı sadece ağacın yapraklarına ulaşıldığında devreye girer. Yükleme ekranı sıklığı %50 azaltılmıştır.

### 4.2. Unity `JsonUtility` Limitasyonu ve Hayalet Obje (Ghost Object) Bug'ı
- **Sorun:** Karar ağacının sonunu belirtmek için API `"next_event": null` döner. Ancak Unity'nin eski kütüphanesi `JsonUtility`, bu *null* sınıfı C# tarafında null bırakmak yerine, bütün alanları (`title`, `description`) boş olan sahte bir obje (Ghost Object) yaratır. Bu durum oyunun ağacın bittiğini anlayamamasına ve ekrana hiçbir butonu olmayan boş bir Canvas yansıtmasına sebep olur.
- **Çözüm:** Adaptör sınıfı (`JsonToSoConverter`) içerisine güçlü bir kontrol filtresi eklendi. Gelen DTO objesinin `title` alanı boş (null veya empty) ise, bunun Unity tarafından üretilmiş bir hayalet obje olduğu algılanır ve kod manuel olarak `null` döndürerek ağacı düzgün şekilde sonlandırır.

### 4.3. Dinamik Bellek Yönetimi ve Çöp Toplayıcı (Garbage Collector) Optimizasyonu
- **Sorun:** API her çağrıldığında bellekte onlarca yeni `EventCardSO` (ScriptableObject) yaratılır. Oyuncu bu ağaçtaki bir dalı seçtiğinde, diğer dallar (ve onların altındaki tüm veriler) bellekte sahipsiz kalarak Memory Leak (Bellek sızıntısı) yaratır.
- **Çözüm:** **Özyinelemeli Budama (Recursive Pruning)** algoritması yazılmıştır. `DecisionTreeController` içinde yer alan `DestroyTree` metodu, oyuncunun seçmediği yollardaki tüm verileri ağaç yapısında dolaşarak Unity'nin `DestroyImmediate` fonksiyonuyla anında RAM'den siler. Bu işlem Garbage Collector (Çöp Toplayıcı) yükünü sıfıra indirir.

### 4.4. Hata Toleransı (Fault Tolerance) ve Acil Durum Fallback Ağı
Ağ bağlantılı asenkron oyunların çökme riskine karşı 3 katmanlı güvenlik duvarı kurulmuştur:
1. **Network Timeout ve Retry:** `UnityWebRequest` nesnesine zorunlu 15 saniyelik zaman aşımı tanımlanmış ve geçici internet kopmalarına karşı `MAX_RETRIES` mantığıyla kendi kendini tekrar etme (Try-Catch döngüsü) sağlanmıştır.
2. **Markdown Temizleyici (`CleanJsonString`):** LLM'in kafası karışıp JSON verisini ```json ... ``` etiketleri arasında string formatında atması riskine karşı, gelen veri her halükarda regex ve substring metotlarıyla saf JSON formatına kırpılır.
3. **Emergency Fallback UI:** Tüm ağ bağlantıları kopsa veya LLM parse edilemeyecek kadar bozuk veri dönse bile, kodda `currentEventCard == null` durumuna düşüldüğü an yakalanır. Sistem dinamik olarak, oyunun çökmesini önleyen, oyuncuya "Sistem Bağlantı Hatası - Bakanlıktan veriler alınamıyor" şeklinde lokal bir olay kartı oluşturur ve ona "Tekrar Dene" şansı verir. Oyun döngüsü asla kilitlenmez.
