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
    private Task<EventCardSO>[] prefetchTasks = null;
    private System.Threading.CancellationTokenSource[] prefetchCts = null;

    [Header("Game Settings")]
    [Tooltip("API beklerken oyuncuya gösterilecek siyah ekranın (yükleme animasyonunun) minimum milisaniye cinsinden süresi. (Örn: 4000 = 4 sn)")]
    [SerializeField] private int minLoadingScreenMs = 4000;

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
        Debug.Log("Yeni bir Karar Ağacı başlatılıyor...");

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

        currentEventCard = await FetchNewTreeAsync(context, eco, env, soc);

        if (uiManager != null)
        {
            uiManager.HideLoadingScreen();
            if (currentEventCard != null) uiManager.DisplayEvent(currentEventCard);
        }
    }

    private async Task<EventCardSO> FetchNewTreeAsync(string context, int eco, int env, int soc, System.Threading.CancellationToken ct = default)
    {
        // Geçmişi bağlama ekleyelim ki AI Context-Aware olsun
        string fullContext = context;
        if (eventHistory.Count > 0)
        {
            fullContext += "\n\nGeçmişte Yaşananlar ve Yapılan Seçimler:\n" + string.Join("\n", eventHistory);
        }

        string jsonResponse = await aiService.GenerateEventAsync(fullContext, eco, env, soc, ct);
        EventCardSO root = JsonToSoConverter.Convert(jsonResponse);

        if (root == null)
        {
            Debug.LogError("API'den geçerli bir JSON alınamadı ve dönüştürülemedi. root null geldi!");
            
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
            return fallbackCard;
        }
        return root;
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
        
        // 1.5. Eğer prefetch edilmiş görevler varsa, SEÇİLMEYENLERİ İPTAL ET
        if (prefetchCts != null)
        {
            for (int i = 0; i < prefetchCts.Length; i++)
            {
                if (i != choiceIndex && prefetchCts[i] != null)
                {
                    prefetchCts[i].Cancel();
                    prefetchCts[i].Dispose();
                    prefetchCts[i] = null;
                }
            }
        }

        EventCardSO nextCard = selectedChoice.next_event;

        // 2. Mevcut (eski) Scriptable Object'ini bellekten sil
        DestroyImmediate(currentEventCard);

        // 3. Yeni kök düğümümüz, seçtiğimiz dal oldu
        currentEventCard = nextCard;

        if (currentEventCard != null)
        {
            // Eğer yaprak düğüme (Depth 3) ulaştıysak (next_event'ler null ise), prefetch başlat.
            bool isLeaf = true;
            foreach (var c in currentEventCard.choices)
            {
                if (c.next_event != null) isLeaf = false;
            }

            if (isLeaf)
            {
                Debug.Log("Depth 2'ye ulaşıldı. Gelecek nesil için Prefetch görevleri başlatılıyor...");
                prefetchTasks = new Task<EventCardSO>[currentEventCard.choices.Count];
                prefetchCts = new System.Threading.CancellationTokenSource[currentEventCard.choices.Count];
                
                int currentEco = factionManager != null ? factionManager.CurrentEconomy : 50;
                int currentEnv = factionManager != null ? factionManager.CurrentEnvironment : 50;
                int currentSoc = factionManager != null ? factionManager.CurrentSociety : 50;

                for (int i = 0; i < currentEventCard.choices.Count; i++)
                {
                    Choice c = currentEventCard.choices[i];
                    // Seçimin etkilerini şimdiki duruma ekleyip tahmin yapalım
                    int predictedEco = Mathf.Clamp(currentEco + c.economy_impact, 0, 100);
                    int predictedEnv = Mathf.Clamp(currentEnv + c.environment_impact, 0, 100);
                    int predictedSoc = Mathf.Clamp(currentSoc + c.society_impact, 0, 100);
                    
                    prefetchCts[i] = new System.Threading.CancellationTokenSource();
                    prefetchTasks[i] = FetchNewTreeAsync(c.next_prompt_clue, predictedEco, predictedEnv, predictedSoc, prefetchCts[i].Token);
                }
            }

            uiManager.DisplayEvent(currentEventCard);
        }
        else
        {
            // Ağacın sonuna geldik (Son Karar Tıklandı)
            if (factionManager != null && factionManager.IsTwoFactionsZero())
            {
                TriggerEndGameSequence(selectedChoice.next_prompt_clue);
            }
            else
            {
                HandlePrefetchedTreeTransition(choiceIndex, selectedChoice.next_prompt_clue);
            }
        }
    }

    private async void HandlePrefetchedTreeTransition(int choiceIndex, string fallbackClue)
    {
        if (uiManager != null) uiManager.ShowLoadingScreen(fallbackClue);

        Task delayTask = Task.Delay(minLoadingScreenMs); // Animasyon için en az belirtilen süre bekle
        Task<EventCardSO> treeTask = null;

        if (prefetchTasks != null && choiceIndex < prefetchTasks.Length && prefetchTasks[choiceIndex] != null)
        {
            Debug.Log($"Prefetch görevi bekleniyor... (İndeks: {choiceIndex})");
            treeTask = prefetchTasks[choiceIndex];
        }
        else
        {
            Debug.LogWarning("Prefetch bulunamadı, normal API çağrısı yapılıyor.");
            int eco = factionManager != null ? factionManager.CurrentEconomy : 50;
            int env = factionManager != null ? factionManager.CurrentEnvironment : 50;
            int soc = factionManager != null ? factionManager.CurrentSociety : 50;
            treeTask = FetchNewTreeAsync(fallbackClue, eco, env, soc);
        }

        await Task.WhenAll(delayTask, treeTask);
        
        if (treeTask != null && treeTask.IsCompletedSuccessfully)
        {
            currentEventCard = treeTask.Result;
        }
        else
        {
            Debug.LogWarning("TreeTask failed or cancelled. Trying to fetch normally.");
            int eco = factionManager != null ? factionManager.CurrentEconomy : 50;
            int env = factionManager != null ? factionManager.CurrentEnvironment : 50;
            int soc = factionManager != null ? factionManager.CurrentSociety : 50;
            currentEventCard = await FetchNewTreeAsync(fallbackClue, eco, env, soc);
        }
        
        prefetchTasks = null; // Eski prefetch görevlerini temizle
        if (prefetchCts != null)
        {
            for (int i = 0; i < prefetchCts.Length; i++)
            {
                if (prefetchCts[i] != null) prefetchCts[i].Dispose();
            }
            prefetchCts = null;
        }

        if (uiManager != null)
        {
            uiManager.HideLoadingScreen();
            if (currentEventCard != null) uiManager.DisplayEvent(currentEventCard);
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
