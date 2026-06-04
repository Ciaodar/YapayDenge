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
    // Gemini API bilgileri
    private const string API_URL =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent";

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

        // Bulunamazsa proje dizinindeki .env dosyasından okumayı dene
        try
        {
            // Application.dataPath normalde Assets klasörüdür, .env bir üstte (kök dizinde) bulunur
            string envPath = System.IO.Path.Combine(Application.dataPath, "../.env");
            if (System.IO.File.Exists(envPath))
            {
                string[] lines = System.IO.File.ReadAllLines(envPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("GEMINI_API_KEY="))
                    {
                        return line.Substring("GEMINI_API_KEY=".Length).Trim();
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($".env dosyası okunurken hata oluştu: {e.Message}");
        }

        Debug.LogError("GEMINI_API_KEY bulunamadı! Lütfen proje kök dizinine .env dosyası ekleyin veya sistem değişkenlerine tanımlayın.");
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

    private string GetRandomGroundTruthRules()
    {
        if (allGroundTruths == null || allGroundTruths.Length == 0) return "";

        System.Random rnd = new System.Random();

        List<GroundTruthScenario> selectedScenarios = new List<GroundTruthScenario>(allGroundTruths);

        // Tüm 100 kuralı kendi içinde karıştır (sıralama ön yargısını kırmak için)
        int sn = selectedScenarios.Count;
        while (sn > 1)
        {
            sn--;
            int k = rnd.Next(sn + 1);
            var value = selectedScenarios[sn];
            selectedScenarios[sn] = selectedScenarios[k];
            selectedScenarios[k] = value;
        }

        // SADECE İLK 5 TANESİNİ AL (Token tasarrufu için)
        int takeCount = Mathf.Min(5, selectedScenarios.Count);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("\nÖNEMLİ BİLİMSEL REFERANSLAR (GROUND TRUTH):");
        sb.AppendLine(
            $"Aşağıdaki {takeCount} olay ve etkilerini bilimsel 'Ground Truth' olarak kabul etmelisin. Seçenekleri üretirken veya puanlarken bilimsel verilere dayanmalısın:");
        for (int i = 0; i < takeCount; i++)
        {
            var s = selectedScenarios[i];
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

    public async Task<string> GenerateEventAsync(string context, int eco, int env, int soc)
    {
        string groundTruthRules = GetRandomGroundTruthRules();
        string dynamicPromptRules = "";
        string choiceCountRule =
            "HER BİR OLAY (event) İÇİN KESİNLİKLE 4 FARKLI SEÇENEK ('choices') ÜRETMELİSİN!";

        if (eco <= 0 || env <= 0 || soc <= 0)
        {
            dynamicPromptRules =
                "KRİTİK DURUM: Factionlardan biri SIFIRLANDI! Ülke çöküşte. Diğer tüm değerleri de hızla sıfıra çekecek felaket senaryoları ve seçenekler üret.\n" +
                "ÇÖKÜŞ DURUMU: Oyuncuya çaresizliği hissettirmek için olayları SADECE 1 TEK SEÇENEK (çaresiz bir kabul) bırakarak kurgulamalısın. Çöküş anında normaldeki '4 seçenek' kuralı iptaldir.";
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
- Derinlik 1: İlk olay (Root)
- Derinlik 2: İlk olayın seçimlerindeki (choices) 'next_event' olayları.
SADECE Derinlik 2'deki 'next_event' değerleri null olmalıdır!
TÜM 'choice' objelerinde (next_event null olsa bile) 'next_prompt_clue' alanı KESİNLİKLE bulunmalıdır. 'next_prompt_clue' alanı, API yüklenirken oyuncunun siyah bir ekranda okuyacağı ara sahne hikayesidir (Flavor Text). Bu nedenle en az 20, en fazla 35 kelime uzunluğunda, alınan o kararın şehre yansıyan uzun vadeli, atmosferik ve dramatik sonucunu betimleyen edebi bir paragraf olmalıdır.
{choiceCountRule}

{dynamicPromptRules}

JSON Yapısı Örneği (TAM 2 Seviye Derinlik ve 4 Seçenek):
{{
  ""event_id"": ""ROOT_01"",
  ""title"": ""Derinlik 1 Olayı (Başlangıç)"",
  ""description"": ""Buradan hikaye başlıyor."", 
  ""choices"": [
    {{
      ""text"": ""Derinlik 1 - Seçim 1"",
      ""economy_impact"": 5,
      ""environment_impact"": -5,
      ""society_impact"": 0,
      ""next_prompt_clue"": ""İkinci aşamaya geçiş ipucu."",
      ""next_event"": {{
         ""event_id"": ""D2_A"",
         ""title"": ""Derinlik 2 - A Olayı (SON)"",
         ""description"": ""Birinci seçimin sonucu."",
         ""choices"": [
             {{
                ""text"": ""Son Karar 1"",
                ""economy_impact"": -5,
                ""environment_impact"": 10,
                ""society_impact"": 5,
                ""next_prompt_clue"": ""Yeni ağaç bağlamı."",
                ""next_event"": null
             }},
             {{
                ""text"": ""Son Karar 2"",
                ""economy_impact"": 10,
                ""environment_impact"": -5,
                ""society_impact"": 0,
                ""next_prompt_clue"": ""Yeni ağaç bağlamı."",
                ""next_event"": null
             }},
             {{
                ""text"": ""Son Karar 3"",
                ""economy_impact"": 0,
                ""environment_impact"": 0,
                ""society_impact"": 0,
                ""next_prompt_clue"": ""Yeni ağaç bağlamı."",
                ""next_event"": null
             }},
             {{
                ""text"": ""Son Karar 4"",
                ""economy_impact"": 5,
                ""environment_impact"": 5,
                ""society_impact"": -10,
                ""next_prompt_clue"": ""Yeni ağaç bağlamı."",
                ""next_event"": null
             }}
         ]
      }}
    }},
    {{
      ""text"": ""Derinlik 1 - Seçim 2"",
      ""economy_impact"": -10,
      ""environment_impact"": 10,
      ""society_impact"": 5,
      ""next_prompt_clue"": ""İkinci aşamaya geçiş ipucu."",
      ""next_event"": {{
         ""event_id"": ""D2_B"",
         ""title"": ""Derinlik 2 - B Olayı (SON)"",
         ""description"": ""İkinci seçimin sonucu."",
         ""choices"": [
             {{
                ""text"": ""Son Karar 1"",
                ""economy_impact"": 5,
                ""environment_impact"": -5,
                ""society_impact"": 5,
                ""next_prompt_clue"": ""Yeni ağaç bağlamı."",
                ""next_event"": null
             }},
             {{
                ""text"": ""Son Karar 2"",
                ""economy_impact"": 0,
                ""environment_impact"": 5,
                ""society_impact"": -5,
                ""next_prompt_clue"": ""Yeni ağaç bağlamı."",
                ""next_event"": null
             }},
             {{
                ""text"": ""Son Karar 3"",
                ""economy_impact"": 0,
                ""environment_impact"": 0,
                ""society_impact"": 0,
                ""next_prompt_clue"": ""Yeni ağaç bağlamı."",
                ""next_event"": null
             }},
             {{
                ""text"": ""Son Karar 4"",
                ""economy_impact"": -5,
                ""environment_impact"": -5,
                ""society_impact"": 10,
                ""next_prompt_clue"": ""Yeni ağaç bağlamı."",
                ""next_event"": null
             }}
         ]
      }}
    }},
    {{
      ""text"": ""Derinlik 1 - Seçim 3"",
      ""economy_impact"": 0,
      ""environment_impact"": 0,
      ""society_impact"": 0,
      ""next_prompt_clue"": ""İkinci aşamaya geçiş ipucu."",
      ""next_event"": {{
         ""event_id"": ""D2_C"",
         ""title"": ""Derinlik 2 - C Olayı (SON)"",
         ""description"": ""Üçüncü seçimin sonucu."",
         ""choices"": [
             {{
                ""text"": ""Son Karar 1"",
                ""economy_impact"": 5,
                ""environment_impact"": -5,
                ""society_impact"": 5,
                ""next_prompt_clue"": ""Yeni ağaç bağlamı."",
                ""next_event"": null
             }},
             {{
                ""text"": ""Son Karar 2"",
                ""economy_impact"": 0,
                ""environment_impact"": 5,
                ""society_impact"": -5,
                ""next_prompt_clue"": ""Yeni ağaç bağlamı."",
                ""next_event"": null
             }},
             {{
                ""text"": ""Son Karar 3"",
                ""economy_impact"": 0,
                ""environment_impact"": 0,
                ""society_impact"": 0,
                ""next_prompt_clue"": ""Yeni ağaç bağlamı."",
                ""next_event"": null
             }},
             {{
                ""text"": ""Son Karar 4"",
                ""economy_impact"": -5,
                ""environment_impact"": 0,
                ""society_impact"": 5,
                ""next_prompt_clue"": ""Yeni ağaç bağlamı."",
                ""next_event"": null
             }}
         ]
      }}
    }},
    {{
      ""text"": ""Derinlik 1 - Seçim 4"",
      ""economy_impact"": 5,
      ""environment_impact"": 5,
      ""society_impact"": -10,
      ""next_prompt_clue"": ""İkinci aşamaya geçiş ipucu."",
      ""next_event"": {{
         ""event_id"": ""D2_D"",
         ""title"": ""Derinlik 2 - D Olayı (SON)"",
         ""description"": ""Dördüncü seçimin sonucu."",
         ""choices"": [
             {{
                ""text"": ""Son Karar 1"",
                ""economy_impact"": 5,
                ""environment_impact"": -5,
                ""society_impact"": 5,
                ""next_prompt_clue"": ""Yeni ağaç bağlamı."",
                ""next_event"": null
             }},
             {{
                ""text"": ""Son Karar 2"",
                ""economy_impact"": 0,
                ""environment_impact"": 5,
                ""society_impact"": -5,
                ""next_prompt_clue"": ""Yeni ağaç bağlamı."",
                ""next_event"": null
             }},
             {{
                ""text"": ""Son Karar 3"",
                ""economy_impact"": 0,
                ""environment_impact"": 0,
                ""society_impact"": 0,
                ""next_prompt_clue"": ""Yeni ağaç bağlamı."",
                ""next_event"": null
             }},
             {{
                ""text"": ""Son Karar 4"",
                ""economy_impact"": -5,
                ""environment_impact"": 0,
                ""society_impact"": 5,
                ""next_prompt_clue"": ""Yeni ağaç bağlamı."",
                ""next_event"": null
             }}
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

        for (int i = 0; i < MAX_RETRIES; i++)
        {
            try
            {
                string responseString = await SendWebRequestAsync(API_URL, jsonPayload);

                // Gemini response'unu parse et
                GeminiResponse geminiResponse = JsonUtility.FromJson<GeminiResponse>(responseString);

                if (geminiResponse != null && geminiResponse.candidates != null && geminiResponse.candidates.Length > 0)
                {
                    string aiText = geminiResponse.candidates[0].content.parts[0].text;
                    Debug.Log($"[API RAW RESPONSE]: {aiText}");

                    // LLM bazen JSON'ı ```json ... ``` markdown blokları arasına alabilir. Bu kısımları temizle:
                    aiText = CleanJsonString(aiText);
                    Debug.Log($"[API CLEANED RESPONSE]: {aiText}");

                    return aiText;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"API Hatası (Deneme {i + 1}/{MAX_RETRIES}): {ex.Message}");
                await Task.Delay(1000);
            }
        }

        Debug.LogError(
            "API'ye ulaşılamadı veya tüm denemeler başarısız oldu. Fallback (Varsayılan) senaryo yükleniyor.");
        return GetFallbackJson();
    }

    public async Task<string> GenerateStorySummaryAsync(string context)
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

        for (int i = 0; i < MAX_RETRIES; i++)
        {
            try
            {
                string responseString = await SendWebRequestAsync(API_URL, jsonPayload);
                GeminiResponse geminiResponse = JsonUtility.FromJson<GeminiResponse>(responseString);

                if (geminiResponse != null && geminiResponse.candidates != null && geminiResponse.candidates.Length > 0)
                {
                    string aiText = geminiResponse.candidates[0].content.parts[0].text;
                    return aiText.Trim();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Özet API Hatası (Deneme {i + 1}/{MAX_RETRIES}): {ex.Message}");
                await Task.Delay(1000);
            }
        }

        return
            "Yıllar süren mücadeleye rağmen ülke, alınan ağır kararların altında ezildi. Ekonomik çöküş, çevresel felaketler ve halkın bitmeyen isyanları sonucunda geriye yönetilecek hiçbir şey kalmadı. Tarih, bu dönemi bir felaketler silsilesi olarak hatırlayacak.";
    }

    private Task<string> SendWebRequestAsync(string url, string jsonBody)
    {
        var tcs = new TaskCompletionSource<string>();

        var request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("X-goog-api-key", API_KEY);
        request.timeout = 15; // 15 saniye zaman aşımı

        var operation = request.SendWebRequest();

        operation.completed += (AsyncOperation op) =>
        {
            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                tcs.SetException(new Exception(request.error + "\n" + request.downloadHandler.text));
            }
            else
            {
                tcs.SetResult(request.downloadHandler.text);
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
