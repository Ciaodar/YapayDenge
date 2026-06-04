using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct Choice
{
    [TextArea(2, 4)]
    public string text;
    
    [Header("Faction Impacts")]
    public int economy_impact;
    public int society_impact;
    public int environment_impact;
    
    [Header("AI Prompt")]
    [TextArea(2, 4)]
    public string next_prompt_clue;
    
    [Header("Nested Event")]
    [Tooltip("Bu seçenek seçildiğinde doğrudan tetiklenecek bir sonraki olay.")]
    public EventCardSO next_event;
}

[CreateAssetMenu(fileName = "NewEventCard", menuName = "YapayDenge/Event Card")]
public class EventCardSO : ScriptableObject
{
    public string event_id;
    public string title; // Teknik ID yerine oyuncuya gösterilecek başlık
    
    [TextArea(3, 10)]
    public string description;
    
    public List<Choice> choices = new List<Choice>();
}
