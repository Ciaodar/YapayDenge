using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.Text.RegularExpressions;

public class GenAIService
{
    private readonly string[] FALLBACK_MODELS = new string[]
    {
        "gemini-3.5-flash",
        "gemini-3-flash-preview",
        "gemini-3.1-flash-lite",
        "gemini-2.5-flash",
        "gemini-2.5-flash-lite",
        "gemini-2.0-flash",
        "gemini-2.0-flash-lite"
    };

    private readonly string API_KEY;
    private const int MAX_RETRIES = 3;

    private GroundTruthScenario[] allGroundTruths;

    public GenAIService()
    {
        API_KEY = LoadApiKey();
        LoadGroundTruth();
    }

    private string LoadApiKey()
    {
        // Önce Environment Variables (Sistem değişkenleri) kontrol et
        string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrEmpty(apiKey)) return apiKey;

        // Unity Resources klasöründen (Assets/Resources/API_KEY.txt) oku
        TextAsset keyAsset = Resources.Load<TextAsset>("API_KEY");
        if (keyAsset != null && !string.IsNullOrWhiteSpace(keyAsset.text))
        {
            return keyAsset.text.Trim();
        }

        Debug.LogError("GEMINI_API_KEY bulunamadı! Lütfen Assets/Resources/API_KEY.txt dosyasını oluşturun veya sistem değişkenlerine tanımlayın.");
        return "";
    }

    private void LoadGroundTruth()
    {
        TextAsset jsonAsset = Resources.Load<TextAsset>("GroundTruth");
        if (jsonAsset != null)
        {
            string wrappedJson = "{\"scenarios\":" + jsonAsset.text + "}";
            GroundTruthData data = JsonUtility.FromJson<GroundTruthData>(wrappedJson);
            if (data != null && data.scenarios != null)
            {
                allGroundTruths = data.scenarios;
                Debug.Log($"Loaded {allGroundTruths.Length} Ground Truth scenarios.");
            }
        }
        else
        {
            Debug.LogError("GroundTruth.json not found in Resources!");
        }
    }

    private string GetRelevantGroundTruthRules(string context)
    {
        if (allGroundTruths == null || allGroundTruths.Length == 0) return "";

        // Context metnini kelimelere ayır (Tokenize)
        string[] contextTokens = context.ToLower().Split(new char[] { ' ', '.', ',', '!', '?', ';', ':', '\n', '\r', '\"', '\'' }, StringSplitOptions.RemoveEmptyEntries);
        HashSet<string> contextWords = new HashSet<string>(contextTokens);

        // Her bir senaryoyu skorla (Basit TF-IDF / Anahtar Kelime Eşleşmesi)
        var scoredScenarios = new List<KeyValuePair<GroundTruthScenario, int>>();
        foreach (var scenario in allGroundTruths)
        {
            int score = 0;
            string textToMatch = (scenario.action_name + " " + scenario.scientific_basis).ToLower();
            string[] scenarioTokens = textToMatch.Split(new char[] { ' ', '.', ',', '!', '?', ';', ':', '\n', '\r', '\"', '\'' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var token in scenarioTokens)
            {
                // Bağlaçlar ve kısa kelimeleri (ve, bir, vb.) elemek için 3 harften büyük olanları baz alıyoruz
                if (token.Length > 3 && contextWords.Contains(token))
                {
                    score++;
                }
            }
            // Biraz da rastgelelik ekleyelim ki her zaman birebir aynı kelime eşleşmesi aynı kartı getirmesin
            score += UnityEngine.Random.Range(0, 2);
            
            scoredScenarios.Add(new KeyValuePair<GroundTruthScenario, int>(scenario, score));
        }

        // Skorlara göre büyükten küçüğe sırala
        scoredScenarios.Sort((x, y) => y.Value.CompareTo(x.Value));

        // En alakalı SADECE İLK 5 TANESİNİ AL (Token tasarrufu için)
        int takeCount = Mathf.Min(5, scoredScenarios.Count);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("\nÖNEMLİ BİLİMSEL REFERANSLAR (GROUND TRUTH):");
        sb.AppendLine(
            $"Aşağıdaki {takeCount} olay ve etkilerini bilimsel 'Ground Truth' olarak kabul etmelisin. Seçenekleri üretirken veya puanlarken bilimsel verilere dayanmalısın:");
        for (int i = 0; i < takeCount; i++)
        {
            var s = scoredScenarios[i].Key;
            sb.AppendLine(
                $"- Senaryo: {s.action_name} | Ekonomi: {s.economy_impact}, Çevre: {s.environment_impact}, Halk: {s.society_impact}. Neden: {s.scientific_basis}");
        }

        return sb.ToString();
    }

    // Gemini API Request ve Response DTO'ları
    [Serializable]
    private class GeminiRequest
    {
        public GeminiContent[] contents;
    }

    [Serializable]
    private class GeminiContent
    {
        public GeminiPart[] parts;
    }

    [Serializable]
    private class GeminiPart
    {
        public string text;
    }

    [Serializable]
    private class GeminiResponse
    {
        public GeminiCandidate[] candidates;
    }

    [Serializable]
    private class GeminiCandidate
    {
        public GeminiContent content;
    }

    [Serializable]
    public class GroundTruthScenario
    {
        public string scenario_id;
        public string action_name;
        public string scientific_basis;
        public int economy_impact;
        public int environment_impact;
        public int society_impact;
    }

    [Serializable]
    private class GroundTruthData
    {
        public GroundTruthScenario[] scenarios;
    }

    public async Task<string> GenerateEventAsync(string context, int eco, int env, int soc, System.Threading.CancellationToken cancellationToken = default)
    {
        string groundTruthRules = GetRelevantGroundTruthRules(context);
        string dynamicPromptRules = "";
        string choiceCountRule =
            "Ağaç KESİNLİKLE 2 seviye (Depth 2) derinliğinde olmalıdır:\n" +
            "- Derinlik 1 (Root): 4 seçim.\n" +
            "- Derinlik 2: KESİNLİKLE SADECE 2 seçim.";

        if (eco <= 0 || env <= 0 || soc <= 0)
        {
            dynamicPromptRules =
                "KRİTİK DURUM: Factionlardan biri SIFIRLANDI! Ülke çöküşte. Diğer tüm değerleri de hızla sıfıra çekecek felaket senaryoları ve seçenekler üret.\n" +
                "ÇÖKÜŞ DURUMU: Oyuncuya çaresizliği hissettirmek için olayları SADECE 1 TEK SEÇENEK (çaresiz bir kabul) bırakarak kurgulamalısın. Çöküş anında normaldeki '4-2 seçenek' kuralı iptaldir.";
        }
        else
        {
            if (eco > 80)
                dynamicPromptRules += "- Ekonomi çok yüksek. Ekonomiyi ciddi şekilde düşürecek krizler yarat.\n";
            if (eco < 30)
                dynamicPromptRules +=
                    "- Ekonomi çok düşük. Ekonomiyi toparlamak için (ama diğerlerine ağır zarar verecek) zorlu fırsatlar sun.\n";

            if (env > 80) dynamicPromptRules += "- Çevre çok temiz. Çevreyi ciddi şekilde kirletecek krizler yarat.\n";
            if (env < 30)
                dynamicPromptRules +=
                    "- Çevre çok kirli. Çevreyi temizlemek için (ama diğerlerine ağır zarar verecek) zorlu fırsatlar sun.\n";

            if (soc > 80)
                dynamicPromptRules += "- Halk çok mutlu. Halkın huzurunu ciddi şekilde bozacak krizler yarat.\n";
            if (soc < 30)
                dynamicPromptRules +=
                    "- Halk isyanın eşiğinde. Halkı yatıştırmak için (ama diğerlerine ağır zarar verecek) zorlu fırsatlar sun.\n";
        }

        string prompt = $@"Sen bir hikaye ve rol yapma (roleplay) oyunu yöneticisisin. Oyuncu bir ülkeyi yöneten başkandır.
Mevcut Durum -> Ekonomi: {eco}/100, Çevre: {env}/100, Halk: {soc}/100.
Son bağlam: {context}.

{groundTruthRules}

Lütfen bu bağlama uygun, oyunu ilerletecek spesifik ve derinliği olan yeni bir olay (event) üret. 
Seçimlerin ekonomi, çevre ve halk üzerinde tam sayı etkileri olmalıdır (örn: 10, -5, 0).

ÇOK ÖNEMLİ (ROLEPLAY VE HİKAYE DERİNLİĞİ): 
- Asla 'Sanayi bölgesinde sorun çıktı' veya 'Halk mutsuz' gibi jenerik, yüzeysel ve genelgeçer ifadeler kullanma!
- Olayları çok spesifik, kurgusal karakter isimleri (örneğin 'Adalet Bakanı Charles', 'Çevre Aktivisti Elena', 'Sendika Lideri Marcus' gibi) kullanarak ve somut politik/dramatik krizler etrafında kurgula.
- Örneğin: 'Adalet Bakanı Charles'ın şehrin en büyük sanayi şirketinden rüşvet aldığı sızdırıldı!' gibi doğrudan olay örgüsüne giren, politik entrika barındıran durumlar yarat.
- Oyuncunun alacağı kararlar da 'Alttan al ve halkı yatıştır', 'Charles'ı halkın önünde tutuklat', 'Medyaya yayın yasağı getir' gibi spesifik ve rol yapma hissiyatını güçlendiren aksiyonlar olmalıdır.

Oyunun genel tonu dram, entrika ve acımasız politika üzerine kuruludur.

Yanıt Türkçe olmalıdır.

Oyuncunun dengede kalabilmesi için uygun ve mantıklı seçenekler vermelisin ancak bunları altın tepside sunma. Doğrular her zaman bedel ödetmelidir.
Kritik noktalarda oyunun bitmesine sebep olabilecek seçenekler de ekleyebilirsin.

Description kısmında maksimum 30 kelime, choices kısmında ise her birinin text kısmında maksimum 12 kelime olmasına dikkat et.

Yanıtın SADECE aşağıdaki yapıda geçerli bir JSON olmalıdır. Başında veya sonunda (```json vb.) markdown etiketleri KULLANMA. Sadece JSON verisini döndür.
Bunu bir KARAR AĞACI olarak düşün. Ağaç KESİNLİKLE 2 seviye (Depth 2) derinliğinde olmalıdır:
- Derinlik 1: İlk olay (Root). 4 seçenek.
- Derinlik 2: İkinci olaylar. (Leaf). 2 seçenek.
SADECE Derinlik 2'deki 'next_event' değerleri null olmalıdır!
TÜM 'choice' objelerinde (next_event null olsa bile) 'next_prompt_clue' alanı KESİNLİKLE bulunmalıdır. 'next_prompt_clue' alanı, API yüklenirken oyuncunun siyah bir ekranda okuyacağı ara sahne hikayesidir (Flavor Text). Bu nedenle en az 20, en fazla 35 kelime uzunluğunda, alınan o kararın şehre yansıyan uzun vadeli, atmosferik ve dramatik sonucunu betimleyen edebi bir paragraf olmalıdır.
{choiceCountRule}

{dynamicPromptRules}

JSON Yapısı Örneği (TAM 2 Seviye Derinlik: Root 4 Seçenek, Her Leaf 2 Seçenek):
{{
  ""event_id"": ""ROOT_01"",
  ""title"": ""Derinlik 1 Olayı (Başlangıç)"",
  ""description"": ""Buradan hikaye başlıyor."", 
  ""choices"": [
    {{
      ""text"": ""Derinlik 1 - Seçim 1"",
      ""economy_impact"": 5, ""environment_impact"": -5, ""society_impact"": 0,
      ""next_prompt_clue"": ""İkinci aşamaya geçiş ipucu."",
      ""next_event"": {{
         ""event_id"": ""D2_A"",
         ""title"": ""Derinlik 2 - A Olayı (SON)"",
         ""description"": ""Birinci seçimin sonucu."",
         ""choices"": [
             {{ ""text"": ""Son Karar 1"", ""economy_impact"": -5, ""environment_impact"": 10, ""society_impact"": 5, ""next_prompt_clue"": ""Yeni ağaç bağlamı."", ""next_event"": null }},
             {{ ""text"": ""Son Karar 2"", ""economy_impact"": 10, ""environment_impact"": -5, ""society_impact"": 0, ""next_prompt_clue"": ""Yeni ağaç bağlamı."", ""next_event"": null }}
         ]
      }}
    }},
    {{
      ""text"": ""Derinlik 1 - Seçim 2"",
      ""economy_impact"": -10, ""environment_impact"": 10, ""society_impact"": 5,
      ""next_prompt_clue"": ""İkinci aşamaya geçiş ipucu."",
      ""next_event"": {{
         ""event_id"": ""D2_B"",
         ""title"": ""Derinlik 2 - B Olayı (SON)"",
         ""description"": ""İkinci seçimin sonucu."",
         ""choices"": [
             {{ ""text"": ""Son Karar 1"", ""economy_impact"": 5, ""environment_impact"": -5, ""society_impact"": 5, ""next_prompt_clue"": ""Yeni ağaç bağlamı."", ""next_event"": null }},
             {{ ""text"": ""Son Karar 2"", ""economy_impact"": 0, ""environment_impact"": 5, ""society_impact"": -5, ""next_prompt_clue"": ""Yeni ağaç bağlamı."", ""next_event"": null }}
         ]
      }}
    }},
    {{
      ""text"": ""Derinlik 1 - Seçim 3"",
      ""economy_impact"": 0, ""environment_impact"": 0, ""society_impact"": 0,
      ""next_prompt_clue"": ""İkinci aşamaya geçiş ipucu."",
      ""next_event"": {{
         ""event_id"": ""D2_C"",
         ""title"": ""Derinlik 2 - C Olayı (SON)"",
         ""description"": ""Üçüncü seçimin sonucu."",
         ""choices"": [
             {{ ""text"": ""Son Karar 1"", ""economy_impact"": 5, ""environment_impact"": -5, ""society_impact"": 5, ""next_prompt_clue"": ""Yeni ağaç bağlamı."", ""next_event"": null }},
             {{ ""text"": ""Son Karar 2"", ""economy_impact"": 0, ""environment_impact"": 5, ""society_impact"": -5, ""next_prompt_clue"": ""Yeni ağaç bağlamı."", ""next_event"": null }}
         ]
      }}
    }},
    {{
      ""text"": ""Derinlik 1 - Seçim 4"",
      ""economy_impact"": 5, ""environment_impact"": 5, ""society_impact"": -10,
      ""next_prompt_clue"": ""İkinci aşamaya geçiş ipucu."",
      ""next_event"": {{
         ""event_id"": ""D2_D"",
         ""title"": ""Derinlik 2 - D Olayı (SON)"",
         ""description"": ""Dördüncü seçimin sonucu."",
         ""choices"": [
             {{ ""text"": ""Son Karar 1"", ""economy_impact"": 5, ""environment_impact"": -5, ""society_impact"": 5, ""next_prompt_clue"": ""Yeni ağaç bağlamı."", ""next_event"": null }},
             {{ ""text"": ""Son Karar 2"", ""economy_impact"": 0, ""environment_impact"": 5, ""society_impact"": -5, ""next_prompt_clue"": ""Yeni ağaç bağlamı."", ""next_event"": null }}
         ]
      }}
    }}
  ]
}}";

        GeminiRequest requestBody = new GeminiRequest
        {
            contents = new GeminiContent[]
            {
                new GeminiContent
                {
                    parts = new GeminiPart[]
                    {
                        new GeminiPart { text = prompt }
                    }
                }
            }
        };

        string jsonPayload = JsonUtility.ToJson(requestBody);

        // Agresif Fallback Mekanizması: Modelleri sırayla dener. Max 2 tur atar (toplam 14 deneme).
        int maxGlobalRetries = 2; 
        for (int retryRound = 0; retryRound < maxGlobalRetries; retryRound++)
        {
            foreach (string modelName in FALLBACK_MODELS)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent";
                try
                {
                    string responseString = await SendWebRequestAsync(apiUrl, jsonPayload, cancellationToken);

                    // Gemini response'unu parse et
                    GeminiResponse geminiResponse = JsonUtility.FromJson<GeminiResponse>(responseString);

                    if (geminiResponse != null && geminiResponse.candidates != null && geminiResponse.candidates.Length > 0)
                    {
                        string aiText = geminiResponse.candidates[0].content.parts[0].text;
                        Debug.Log($"[API RAW RESPONSE ({modelName})]: {aiText}");

                        // LLM bazen JSON'ı ```json ... ``` markdown blokları arasına alabilir. Bu kısımları temizle:
                        aiText = CleanJsonString(aiText);
                        Debug.Log($"[API CLEANED RESPONSE]: {aiText}");

                        return aiText;
                    }
                }
                catch (System.OperationCanceledException)
                {
                    Debug.Log($"[GenAI] {modelName} isteği iptal edildi.");
                    throw; // Döngüyü sonlandır
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GenAI] {modelName} modelinde hata: {ex.Message}. Sıradaki modele geçiliyor...");
                    await Task.Delay(500, cancellationToken); // Flood'u önlemek için kısa bekleme
                }
            }
            Debug.LogWarning($"Tüm modeller denendi. Biraz beklenip baştan başlanıyor... (Tur: {retryRound + 1}/{maxGlobalRetries})");
            await Task.Delay(1500, cancellationToken);
        }

        Debug.LogError(
            "API'ye ulaşılamadı veya tüm denemeler başarısız oldu. Fallback (Varsayılan) senaryo yükleniyor.");
        return GetFallbackJson();
    }

    public async Task<string> GenerateStorySummaryAsync(string context, System.Threading.CancellationToken cancellationToken = default)
    {
        string prompt =
            $@"Oyun bitti. Oyuncunun yönettiği ülke, yaptığı yanlış seçimler sonucunda tamamen çöktü ve yönetilecek bir şey kalmadı.
Aşağıda oyuncunun son anlarına ait bağlam (context) bulunuyor:
{context}

Lütfen bu bağlamı kullanarak, oyuncunun ülkeyi nasıl çöküşe sürüklediğini, yaptığı hamleleri;
yaşadığı olayların özetini; halkın, ekonominin ve çevrenin gelişimi ve durumunu anlatan, 
düşündürücü ve sürdürülebilirlik ilkelerine vurgu yapan profesyonel bir hikaye özeti yaz. Oyuncunun hatalarını anlat ve neleri düzeltebileceğinden bahset. 
Bu özet oyun sonu ekranında (End Game Screen) gösterilecek. Asla JSON vs formatlama yapma, doğrudan düz metin (text) olarak hikayeyi anlat.";

        GeminiRequest requestBody = new GeminiRequest
        {
            contents = new GeminiContent[]
            {
                new GeminiContent
                {
                    parts = new GeminiPart[]
                    {
                        new GeminiPart { text = prompt }
                    }
                }
            }
        };

        string jsonPayload = JsonUtility.ToJson(requestBody);

        // End Game Summary için de Agresif Fallback Mekanizması
        int maxGlobalRetries = 2;
        for (int retryRound = 0; retryRound < maxGlobalRetries; retryRound++)
        {
            foreach (string modelName in FALLBACK_MODELS)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent";
                try
                {
                    string responseString = await SendWebRequestAsync(apiUrl, jsonPayload, cancellationToken);

                    GeminiResponse geminiResponse = JsonUtility.FromJson<GeminiResponse>(responseString);

                    if (geminiResponse != null && geminiResponse.candidates != null && geminiResponse.candidates.Length > 0)
                    {
                        return geminiResponse.candidates[0].content.parts[0].text;
                    }
                }
                catch (System.OperationCanceledException)
                {
                    Debug.Log($"[GenAI - Summary] {modelName} isteği iptal edildi.");
                    throw; // Döngüyü tamamen sonlandır
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GenAI - Summary] {modelName} modelinde hata: {ex.Message}. Sıradaki modele geçiliyor...");
                    await Task.Delay(500, cancellationToken);
                }
            }
            await Task.Delay(1500, cancellationToken);
        }

        return
            "Yıllar süren mücadeleye rağmen ülke, alınan ağır kararların altında ezildi. Ekonomik çöküş, çevresel felaketler ve halkın bitmeyen isyanları sonucunda geriye yönetilecek hiçbir şey kalmadı. Tarih, bu dönemi bir felaketler silsilesi olarak hatırlayacak.";
    }

    private Task<string> SendWebRequestAsync(string url, string jsonBody, System.Threading.CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<string>();

        var request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("X-goog-api-key", API_KEY);
        request.timeout = 60; // 3 Derinlikli ağaç üretimi uzun süreceği için zaman aşımını 60 saniyeye çıkardık

        var operation = request.SendWebRequest();
        
        System.Threading.CancellationTokenRegistration registration = default;
        if (cancellationToken != default)
        {
            registration = cancellationToken.Register(() =>
            {
                if (!tcs.Task.IsCompleted)
                {
                    request.Abort();
                    tcs.TrySetCanceled();
                }
            });
        }

        operation.completed += (AsyncOperation op) =>
        {
            if (cancellationToken != default)
            {
                registration.Dispose();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled();
                return;
            }

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                tcs.TrySetException(new Exception(request.error + "\n" + request.downloadHandler.text));
            }
            else
            {
                tcs.TrySetResult(request.downloadHandler.text);
            }

            request.Dispose();
        };

        return tcs.Task;
    }

    private string CleanJsonString(string rawString)
    {
        // Baştaki ve sondaki gereksiz boşlukları/markdown'ları temizle
        string cleanString = rawString.Trim();
        if (cleanString.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            cleanString = cleanString.Substring(7);
        }
        else if (cleanString.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            cleanString = cleanString.Substring(3);
        }

        if (cleanString.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            cleanString = cleanString.Substring(0, cleanString.Length - 3);
        }

        return cleanString.Trim();
    }

    private string GetFallbackJson()
    {
        return @"{
            ""event_id"": ""FALLBACK_EVENT"",
            ""description"": ""Hükümet binasında bir iletişim arızası meydana geldi. Danışmanlardan haber alınamıyor."",
            ""choices"": [
                {
                    ""text"": ""Bekle ve Gör"",
                    ""economy_impact"": -5,
                    ""environment_impact"": 0,
                    ""society_impact"": -5,
                    ""next_prompt_clue"": ""İletişim ağları yavaş yavaş onarılıyor.""
                }
            ]
        }";
    }
}
