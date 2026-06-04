using System;
using UnityEngine;

public class FactionManager : MonoBehaviour
{
    [Header("Faction Data Reference")]
    [Tooltip(
        "Dilerseniz bu değerleri Scriptable Object üzerinden de referanslayabilirsiniz, şu an sınıf içinde tutulmaktadır.")]
    public FactionDataSO factionData;

    [Header("Current Scores")] [SerializeField]
    private int currentEconomy;

    [SerializeField] private int currentEnvironment;
    [SerializeField] private int currentSociety;

    public int CurrentEconomy => currentEconomy;
    public int CurrentEnvironment => currentEnvironment;
    public int CurrentSociety => currentSociety;

    [Header("Game Settings")] [Tooltip("Her tur sonunda tüm factionlardan düşülecek çürüme (Entropy) puanı.")]
    public int entropyCostPerTurn = 2;

    private const int MAX_TOTAL_SCORE = 170;
    private const int MIN_SCORE = 0;
    private const int MAX_SCORE = 100;

    // Observer deseni için eklendi: Ekonomi, Çevre ve Halk puanlarını yayınlar.
    public event Action<int, int, int> OnFactionChange;

    private void Start()
    {
        InitializeScores();
    }

    private void InitializeScores()
    {
        currentEconomy = 50;
        currentEnvironment = 50;
        currentSociety = 50;

        if (factionData != null)
        {
            currentEconomy = factionData.economyScore;
            currentEnvironment = factionData.environmentScore;
            currentSociety = factionData.societyScore;
        }

        // Başlangıç değerlerini dinleyicilere bildir.
        OnFactionChange?.Invoke(currentEconomy, currentEnvironment, currentSociety);
    }

    public void ApplyChoice(Choice selectedChoice)
    {
        // Puanları güncelle (Seçim etkisi + Entropy) ve 0-100 arasına hapset
        currentEconomy = Mathf.Clamp(currentEconomy + selectedChoice.economy_impact - entropyCostPerTurn, MIN_SCORE,
            MAX_SCORE);
        currentEnvironment = Mathf.Clamp(currentEnvironment + selectedChoice.environment_impact - entropyCostPerTurn,
            MIN_SCORE, MAX_SCORE);
        currentSociety = Mathf.Clamp(currentSociety + selectedChoice.society_impact - entropyCostPerTurn, MIN_SCORE,
            MAX_SCORE);

        int totalScore = currentEconomy + currentEnvironment + currentSociety;

        // Denge kuralı kontrolü
        // Artık herhangi biri sıfırlandığında oyun anında bitmiyor, yapay zeka çöküş senaryoları üretiyor.
        Debug.Log(
            $"Seçim uygulandı. Güncel Durum: Eko({currentEconomy}), Çevre({currentEnvironment}), Halk({currentSociety}) - Toplam: {totalScore}");
        // Başarılı bir işlem sonrası yeni durumu dinleyicilere bildir.
        OnFactionChange?.Invoke(currentEconomy, currentEnvironment, currentSociety);
    }

    public bool IsTwoFactionsZero()
    {
        return (currentEconomy <= 0 && currentEnvironment <= 0) ||
               (currentEconomy <= 0 && currentSociety <= 0) ||
               (currentEnvironment <= 0 && currentSociety <= 0);
    }
}
