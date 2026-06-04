using UnityEngine;

public class CityVisualizer : MonoBehaviour
{
    [Header("Managers")]
    public FactionManager factionManager;

    [Header("Visual Renderers")]
    public SpriteRenderer economyRenderer;
    public SpriteRenderer environmentRenderer;
    public SpriteRenderer societyRenderer;

    [System.Serializable]
    public struct FactionSprites
    {
        public Sprite badState;    // 0-30
        public Sprite normalState; // 31-70
        public Sprite goodState;   // 71-100
    }

    [Header("Sprites")]
    public FactionSprites economySprites;
    public FactionSprites environmentSprites;
    public FactionSprites societySprites;

    private void OnEnable()
    {
        if (factionManager != null)
        {
            factionManager.OnFactionChange += UpdateVisuals;
        }
    }

    private void OnDisable()
    {
        if (factionManager != null)
        {
            factionManager.OnFactionChange -= UpdateVisuals;
        }
    }

    private void UpdateVisuals(int economy, int environment, int society)
    {
        UpdateRenderer(economyRenderer, economySprites, economy);
        UpdateRenderer(environmentRenderer, environmentSprites, environment);
        UpdateRenderer(societyRenderer, societySprites, society);
    }

    private void UpdateRenderer(SpriteRenderer renderer, FactionSprites sprites, int score)
    {
        if (renderer == null) return;

        if (score <= 30)
        {
            renderer.sprite = sprites.badState;
        }
        else if (score <= 70)
        {
            renderer.sprite = sprites.normalState;
        }
        else
        {
            renderer.sprite = sprites.goodState;
        }
    }
}
