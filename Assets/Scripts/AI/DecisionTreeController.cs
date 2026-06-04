using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class DecisionTreeController : MonoBehaviour
{
    [Header("Dependencies")] public UIManager uiManager;
    public FactionManager factionManager;

    private GenAIService aiService;
    private EventCardSO currentEventCard;
    private List<string> eventHistory = new List<string>();

    private void Start()
    {
        aiService = new GenAIService();
        FetchNewTree("Oyunun başlangıcı. Sakin ve dengeli bir gün.");
    }

    private void OnEnable()
    {
        if (uiManager != null)
        {
            uiManager.OnChoiceSelected += SelectBranch;
        }
    }

    private void OnDisable()
    {
        if (uiManager != null)
        {
            uiManager.OnChoiceSelected -= SelectBranch;
        }
    }

    private async void FetchNewTree(string context)
    {
        Debug.Log("Yeni bir Karar Ağacı API'den tek seferde (Nested) çekiliyor...");

        if (uiManager != null)
        {
            if (context != null && context.StartsWith("Oyunun başlangıcı"))
            {
                uiManager.ShowLoadingScreen("Başkanım, asistanınız genel durumu kontrol ediyor, lütfen bekleyin.");
            }
            else
            {
                uiManager.ShowLoadingScreen(context ?? "Yeni gelişmeler değerlendiriliyor...");
            }
        }

        int eco = factionManager != null ? factionManager.CurrentEconomy : 50;
        int env = factionManager != null ? factionManager.CurrentEnvironment : 50;
        int soc = factionManager != null ? factionManager.CurrentSociety : 50;
        // Geçmişi bağlama ekleyelim ki AI Context-Aware olsun
        string fullContext = context;
        if (eventHistory.Count > 0)
        {
            fullContext += "\n\nGeçmişte Yaşananlar ve Yapılan Seçimler:\n" + string.Join("\n", eventHistory);
        }

        string jsonResponse = await aiService.GenerateEventAsync(fullContext, eco, env, soc);
        currentEventCard = JsonToSoConverter.Convert(jsonResponse);

        if (uiManager != null)
        {
            uiManager.HideLoadingScreen();
        }

        if (currentEventCard != null && uiManager != null)
        {
            uiManager.DisplayEvent(currentEventCard);
        }
        else if (currentEventCard == null)
        {
            Debug.LogError("API'den geçerli bir JSON alınamadı ve dönüştürülemedi. currentEventCard null geldi!");
            
            // Acil durum Fallback EventCard üret
            EventCardSO fallbackCard = ScriptableObject.CreateInstance<EventCardSO>();
            fallbackCard.title = "Sistem Bağlantı Hatası";
            fallbackCard.description = "Bakanlıktan gelen istihbarat raporları okunamıyor. Güvenli hattan tekrar bağlanmayı deneyin.";
            fallbackCard.choices = new System.Collections.Generic.List<Choice>
            {
                new Choice 
                { 
                    text = "Tekrar Dene", 
                    economy_impact = 0, environment_impact = 0, society_impact = 0, 
                    next_prompt_clue = "Sistemler yeniden başlatıldı, bağlantı aranıyor.", 
                    next_event = null 
                }
            };
            
            currentEventCard = fallbackCard;
            if (uiManager != null) uiManager.DisplayEvent(currentEventCard);
        }
    }

    private void SelectBranch(int choiceIndex)
    {
        if (currentEventCard == null || currentEventCard.choices.Count <= choiceIndex)
        {
            Debug.LogError("Geçersiz seçim!");
            return;
        }

        Choice selectedChoice = currentEventCard.choices[choiceIndex];

        // Geçmişe ekleyelim
        string historyEntry = $"- Olay: {currentEventCard.title} -> Karar: {selectedChoice.text}";
        eventHistory.Add(historyEntry);

        // Token tasarrufu için geçmişi son 10 olayla sınırlandıralım
        if (eventHistory.Count > 10)
        {
            eventHistory.RemoveAt(0);
        }

        // 1. Seçilmeyen diğer dalları ve içlerindeki tüm Nested EventCardSO'ları yok et (Budama / Pruning)
        for (int i = 0; i < currentEventCard.choices.Count; i++)
        {
            if (i != choiceIndex)
            {
                DestroyTree(currentEventCard.choices[i].next_event);
            }
        }

        EventCardSO nextCard = selectedChoice.next_event;

        // 2. Mevcut (eski) Scriptable Object'ini bellekten sil
        DestroyImmediate(currentEventCard);

        // 3. Yeni kök düğümümüz, seçtiğimiz dal oldu
        currentEventCard = nextCard;

        if (currentEventCard != null)
        {
            // Ağaçta hala dal var, doğrudan göster
            uiManager.DisplayEvent(currentEventCard);
        }
        else
        {
            // Ağacın sonuna geldik (3. seçimin ardından)
            if (factionManager != null && factionManager.IsTwoFactionsZero())
            {
                TriggerEndGameSequence(selectedChoice.next_prompt_clue);
            }
            else
            {
                // Elimizdeki clue ile yeni bir ağaç (3 depth) daha oluştur
                FetchNewTree(selectedChoice.next_prompt_clue);
            }
        }
    }

    private async void TriggerEndGameSequence(string context)
    {
        Debug.Log("Oyun Bitti Döngüsü Başladı...");

        if (uiManager != null)
        {
            uiManager.ShowLoadingScreen("Maalesef yönetecek bir ülke kalmadı. Oyun Bitti!");
        }

        // 5 saniye bekleme süresini başlat (AI bu sürede hikayeyi çekecek)
        Task delayTask = Task.Delay(5000);

        // AI'dan hikaye özetini çek
        Task<string> summaryTask = aiService.GenerateStorySummaryAsync(context);

        // Her ikisinin de bitmesini bekle
        await Task.WhenAll(delayTask, summaryTask);

        string summary = summaryTask.Result;

        if (uiManager != null)
        {
            uiManager.HideLoadingScreen();
            uiManager.ShowEndGameScreen(summary);
        }
    }

    /// <summary>
    /// Recursive (özyinelemeli) olarak kullanılmayan Scriptable Object'leri bellekten fiziksel olarak siler.
    /// Memory Leak önlemek için kritik bir fonksiyondur.
    /// </summary>
    private void DestroyTree(EventCardSO node)
    {
        if (node == null) return;

        foreach (var c in node.choices)
        {
            DestroyTree(c.next_event);
        }

        DestroyImmediate(node);
    }
}
