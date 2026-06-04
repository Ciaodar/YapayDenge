using System.Threading.Tasks;
using UnityEngine;
using System.Collections.Generic;

public static class JsonToSoConverter
{
    // JSON'daki yapıyı karşılayacak ara veri yapıları (DTO)
    [System.Serializable]
    private class EventResponseDTO
    {
        public string event_id;
        public string title;
        public string description;
        public ChoiceDTO[] choices;
    }

    [System.Serializable]
    private class ChoiceDTO
    {
        public string text;
        public int economy_impact;
        public int environment_impact;
        public int society_impact;
        public string next_prompt_clue;
        public EventResponseDTO_Level2 next_event;
    }

    [System.Serializable]
    private class EventResponseDTO_Level2
    {
        public string event_id;
        public string title;
        public string description;
        public ChoiceDTO_Level2[] choices;
    }

    [System.Serializable]
    private class ChoiceDTO_Level2
    {
        public string text;
        public int economy_impact;
        public int environment_impact;
        public int society_impact;
        public string next_prompt_clue;
        public EventResponseDTO_Level3 next_event;
    }

    [System.Serializable]
    private class EventResponseDTO_Level3
    {
        public string event_id;
        public string title;
        public string description;
        public ChoiceDTO_Level3[] choices;
    }

    [System.Serializable]
    private class ChoiceDTO_Level3
    {
        public string text;
        public int economy_impact;
        public int environment_impact;
        public int society_impact;
        public string next_prompt_clue;
    }

    /// <summary>
    /// AI'dan gelen JSON'u işleyip ScriptableObject'e dönüştürür.
    /// </summary>
    public static EventCardSO Convert(string jsonString)
    {
        EventResponseDTO dto = null;
        try
        {
            Debug.Log($"[JSON to Parse]: {jsonString}");
            dto = JsonUtility.FromJson<EventResponseDTO>(jsonString);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"JSON Parse Hatası: {ex.Message}");
            return null;
        }

        if (dto == null || dto.choices == null)
        {
            Debug.LogError("JSON verisi alınamadı veya bozuk formatta.");
            return null;
        }

        return ConvertDTO(dto);
    }

    private static EventCardSO ConvertDTO(EventResponseDTO dto)
    {
        if (dto == null) return null;

        EventCardSO newCard = ScriptableObject.CreateInstance<EventCardSO>();
        newCard.event_id = dto.event_id;
        newCard.title = dto.title;
        newCard.description = dto.description;
        newCard.choices = new List<Choice>();

        if (dto.choices != null)
        {
            foreach (var c in dto.choices)
            {
                Choice choice = new Choice
                {
                    text = c.text,
                    economy_impact = c.economy_impact,
                    environment_impact = c.environment_impact,
                    society_impact = c.society_impact,
                    next_prompt_clue = c.next_prompt_clue,
                    next_event = ConvertLevel2(c.next_event) 
                };
                newCard.choices.Add(choice);
            }
        }
        return newCard;
    }

    private static EventCardSO ConvertLevel2(EventResponseDTO_Level2 dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.title)) return null;

        EventCardSO newCard = ScriptableObject.CreateInstance<EventCardSO>();
        newCard.event_id = dto.event_id;
        newCard.title = dto.title;
        newCard.description = dto.description;
        newCard.choices = new List<Choice>();

        if (dto.choices != null)
        {
            foreach (var c in dto.choices)
            {
                Choice choice = new Choice
                {
                    text = c.text,
                    economy_impact = c.economy_impact,
                    environment_impact = c.environment_impact,
                    society_impact = c.society_impact,
                    next_prompt_clue = c.next_prompt_clue,
                    next_event = ConvertLevel3(c.next_event)
                };
                newCard.choices.Add(choice);
            }
        }
        return newCard;
    }

    private static EventCardSO ConvertLevel3(EventResponseDTO_Level3 dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.title)) return null;

        EventCardSO newCard = ScriptableObject.CreateInstance<EventCardSO>();
        newCard.event_id = dto.event_id;
        newCard.title = dto.title;
        newCard.description = dto.description;
        newCard.choices = new List<Choice>();

        if (dto.choices != null)
        {
            foreach (var c in dto.choices)
            {
                Choice choice = new Choice
                {
                    text = c.text,
                    economy_impact = c.economy_impact,
                    environment_impact = c.environment_impact,
                    society_impact = c.society_impact,
                    next_prompt_clue = c.next_prompt_clue,
                    next_event = null
                };
                newCard.choices.Add(choice);
            }
        }
        return newCard;
    }
}
