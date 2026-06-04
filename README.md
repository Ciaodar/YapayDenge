# YapayDenge (Artificial Balance)

**YapayDenge**, oyuncunun bir ülkeyi yönettiği, sürdürülebilirlik ve denge ilkelerini öğretmeyi amaçlayan, dram ve politika tonlarında ciddi (*serious game*) bir Unity oyunudur. Oyunda, geleneksel önceden yazılmış ("hardcoded") diyalog ağaçları yerine, **Gemini AI** tarafından dinamik olarak üretilen spesifik, derinlikli ve rol yapma (roleplay) odaklı karar ağaçları kullanılır.

## 🌟 Oyunun Amacı
Oyunda üç temel unsurun (Faction) dengede tutulması gerekir:
1. **Ekonomi (Economy)**
2. **Çevre (Environment)**
3. **Halk / Toplum (Society)**

Her unsur **0 ile 100** arasında bir değere sahiptir. Oyunun amacı sınırsız bir büyüme elde etmek değil, bu üç unsuru dengelemektir. 

**Oyun Sonu (Game Over) Koşulları:**
- Herhangi bir unsurun değerinin **0 veya daha altına** düşmesi (Çöküş durumu).
- Üç unsurun toplam puanının **170'in üzerine** çıkması. (Bu kural, sınırsız büyüme yerine dengeli bir ekosistem yönetimini zorunlu kılar).

## 🧠 Yapay Zeka Entegrasyonu (GenAI)

YapayDenge, dinamik hikaye anlatımı için **Google Gemini API** (`gemini-3.1-flash-lite`) kullanır. Yapay zeka sistemi, oyunun mevcut durumunu, geçmişte alınan kararları ve bilimsel dayanakları (*Ground Truth*) analiz ederek oyuncuya benzersiz politik krizler ve zorlu seçimler sunar.

### AI Mimarisi ve Karar Ağacı
Oyun, yapay zekadan her defasında **2 derinlikli (Depth-2) iç içe geçmiş (nested) JSON karar ağaçları** talep eder.
- **Dinamik Kurgu:** Jenerik olaylar yerine, spesifik karakterlerin (örneğin *Adalet Bakanı Charles*) dahil olduğu, yolsuzluk, isyan veya endüstriyel felaketler gibi derin rol yapma öğeleri barındıran olaylar kurgulanır.
- **Kesin Sonuçlar:** Her kararın Ekonomi, Çevre ve Halk üzerinde tam sayı (-10, +5 vb.) etkileri vardır. Doğru kararların her zaman bir bedeli vardır.
- **Akıcı Oynanış:** 2 derinlikli ağaç yapısı sayesinde oyuncu her tıklamada API beklemez. Yaprak düğüme (son karara) gelindiğinde, elde edilen uzun vadeli "ipucu" (prompt_clue) ile oyun arka planda yeni bir ağaç üretir.

## 🛠 Sistem Bileşenleri

- **`GenAIService`**: Gemini API ile iletişim kuran, gelişmiş prompt mühendisliği ve hata yönetimi (timeout, retry) barındıran ana servis.
- **`JsonToSoConverter`**: API'den dönen karmaşık JSON verisini ayrıştırıp, oyunun içinde kullanılabilir Unity `ScriptableObject` (`EventCardSO`) veri yapılarına dönüştüren adaptör. Unity'nin `JsonUtility` limitasyonlarına karşı güvenli parsing içerir.
- **`DecisionTreeController`**: Karar ağacının çalışma zamanındaki durumunu ve yaşam döngüsünü yönetir. Bellek optimizasyonu için oyuncunun seçmediği dalları anında yok eder (Budama / Pruning). Acil durumlar için (internet kesintisi, bozuk veri) *Fallback* mekanizmaları ile donatılmıştır.
- **`FactionManager`**: Puanları hesaplar, çürüme (entropy) mekaniğini işletir ve oyun sonu koşullarını denetler.
- **`UIManager`**: Dinamik olarak üretilen seçenekleri, bekleme (Zaman geçerken...) ekranlarını ve oyun sonu (End Game) arayüzlerini yönetir.

## 🚀 Kurulum

1. Bu projeyi Unity Hub üzerinden açın (Proje Unity sürümünüzle uyumlu olmalıdır).
2. Projenin kök dizininde (`Assets` klasörünün yanında) bir `.env` dosyası oluşturun ve içerisine `GEMINI_API_KEY=SİZİN_ANAHTARINIZ` şeklinde Gemini API anahtarınızı ekleyin. (Bu dosya `.gitignore` içinde olduğu için GitHub'a gitmeyecektir).
3. Oyunun ana sahnesini (`MainScene`) açın ve **Play** butonuna basarak ülkenizi yönetmeye başlayın!

## 📜 Hakkında
Bu proje, yapay zekanın (GenAI) bir oyunun core-loop'una (temel oynanış döngüsüne) ne kadar derin ve akıcı şekilde entegre edilebileceğini araştırmak amacıyla geliştirilmiştir. Sadece bir metin üretici olarak değil, matematiksel oyun mekaniklerine hizmet eden bir "Game Master" (Oyun Yöneticisi) olarak çalışmaktadır.
