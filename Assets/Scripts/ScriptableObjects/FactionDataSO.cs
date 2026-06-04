using UnityEngine;

[CreateAssetMenu(fileName = "NewFactionData", menuName = "YapayDenge/Faction Data")]
public class FactionDataSO : ScriptableObject
{
    [Range(0, 100)]
    public int economyScore = 50;
    
    [Range(0, 100)]
    public int environmentScore = 50;
    
    [Range(0, 100)]
    public int societyScore = 50;
}
