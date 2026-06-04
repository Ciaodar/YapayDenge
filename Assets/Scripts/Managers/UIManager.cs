using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.IO;

public class UIManager : MonoBehaviour
{
    [Header("Managers")] public FactionManager factionManager;

    [Header("Top Panel - Sliders")] [Tooltip("Slider'ların Max Value değeri 100 olmalıdır.")]
    public Slider economySlider;

    public Slider environmentSlider;
    public Slider societySlider;

    [Header("Event Card UI")] public TextMeshProUGUI eventTitleText;
    public TextMeshProUGUI eventDescriptionText;

    [Header("Choices UI")] public Transform choicesContainer; // Butonların instantiate edileceği parent
    public GameObject choiceButtonPrefab; // Buton prefabı

    [Header("Loading Narrative UI")] public GameObject loadingCanvas;

    public GameObject generalCanvas;
    public TextMeshProUGUI loadingNarrativeText;

    [Header("End Game UI")]
    public GameObject endGameCanvas;
    public TextMeshProUGUI endGameSummaryText;

    private List<GameObject> activeChoiceButtons = new List<GameObject>();

    // Karar ağacının (DecisionTreeController) hangi seçeneğin tıklandığını bilmesi için eklendi.
    public event System.Action<int> OnChoiceSelected;

    public void ShowLoadingScreen(string narrativeClue)
    {
        if (loadingCanvas != null)
        {
            generalCanvas.SetActive(false);
            loadingCanvas.SetActive(true);
        }

        if (loadingNarrativeText != null)
        {
            loadingNarrativeText.text = "Zaman geçerken...\n\n" + narrativeClue;
        }
    }

    public void HideLoadingScreen()
    {
        if (loadingCanvas != null)
        {
            loadingCanvas.SetActive(false);
            generalCanvas.SetActive(true);
        }
    }

    private void OnEnable()
    {
        if (factionManager != null)
        {
            factionManager.OnFactionChange += UpdateSliders;
        }
    }

    private void OnDisable()
    {
        if (factionManager != null)
        {
            factionManager.OnFactionChange -= UpdateSliders;
        }
    }

    private void UpdateSliders(int economy, int environment, int society)
    {
        if (economySlider != null) economySlider.value = economy;
        if (environmentSlider != null) environmentSlider.value = environment;
        if (societySlider != null) societySlider.value = society;
    }

    public void DisplayEvent(EventCardSO currentEvent)
    {
        if (currentEvent == null) return;

        if (eventTitleText != null) eventTitleText.text = currentEvent.title;
        if (eventDescriptionText != null) eventDescriptionText.text = currentEvent.description;

        ClearOldChoices();

        for (int i = 0; i < currentEvent.choices.Count; i++)
        {
            CreateChoiceButton(currentEvent.choices[i], i);
        }
    }

    private void ClearOldChoices()
    {
        foreach (GameObject btn in activeChoiceButtons)
        {
            Destroy(btn);
        }

        activeChoiceButtons.Clear();
    }

    private void CreateChoiceButton(Choice choice, int index)
    {
        if (choiceButtonPrefab == null || choicesContainer == null)
        {
            Debug.LogWarning("UIManager: Buton Prefab veya Container eksik!");
            return;
        }

        GameObject newBtnObj = Instantiate(choiceButtonPrefab, choicesContainer);
        activeChoiceButtons.Add(newBtnObj);

        // Prefabın içindeki Text componentini bulup yazısını güncelleme
        TextMeshProUGUI btnText = newBtnObj.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null)
        {
            btnText.text = choice.text;
        }

        // Butona tıklandığında ApplyChoice fonksiyonunu çağıracak event'i ekleme
        Button btn = newBtnObj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => OnChoiceButtonClicked(choice, index));
        }
    }

    private void OnChoiceButtonClicked(Choice choice, int index)
    {
        if (factionManager != null)
        {
            factionManager.ApplyChoice(choice);
            OnChoiceSelected?.Invoke(index);
        }
    }

    public void ShowEndGameScreen(string summary)
    {
        if (generalCanvas != null) generalCanvas.SetActive(false);
        if (loadingCanvas != null) loadingCanvas.SetActive(false);
        
        if (endGameCanvas != null)
        {
            endGameCanvas.SetActive(true);
        }
        
        if (endGameSummaryText != null)
        {
            endGameSummaryText.text = summary;
        }
    }

    public void PrintStory()
    {
        if (endGameSummaryText == null) return;
        
        string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        string filePath = Path.Combine(desktopPath, "YapayDenge_Hikayem.txt");
        
        try
        {
            File.WriteAllText(filePath, endGameSummaryText.text);
            Debug.Log($"Hikaye başarıyla kaydedildi: {filePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Hikaye kaydedilirken hata oluştu: {ex.Message}");
        }
    }

    public void LoadMainMenu()
    {
        // Ana menü sahnesinin adı 'MainMenu' olarak varsayılmıştır. Gerekirse güncelleyebilirsiniz.
        SceneManager.LoadScene("Menu");
    }
}
