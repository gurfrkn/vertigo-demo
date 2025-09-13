using UnityEngine;
using AssetKits.ParticleImage;

[System.Serializable]
public class SliceData
{
    public string id;
    public bool isBomb;
    [Range(0f, 1f)] public float rewardWeight = 1f;

    [Header("Reward (ignored if isBomb)")]
    public string rewardName;
    public Sprite rewardIcon;
    public int rewardAmount = 0;
    public ParticleImage rewardParticleImage;
}

[CreateAssetMenu(menuName = "RiskySpin/WheelConfig")]
public class WheelConfig : ScriptableObject
{
    public SliceData[] slices;
}
