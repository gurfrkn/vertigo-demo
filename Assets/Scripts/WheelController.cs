using UnityEngine;
using DG.Tweening;
using AssetKits.ParticleImage;

public class WheelController : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private WheelConfig config;

    [Header("Visuals")]
    [SerializeField] private Transform wheelVisual;

    [Header("Spin")]
    [SerializeField] private float spinDuration = 2.0f;
    [SerializeField] private Vector2 extraSpinsRange = new Vector2(3f, 5f);
    [SerializeField] private AnimationCurve ease;

    public static WheelController Instance { get; private set; }

    private bool spinning;
    private float lastAngle;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void Spin()
    {
        if (spinning || wheelVisual == null || config == null || config.slices == null || config.slices.Length == 0)
        {
            Debug.Log("[Wheel] Missing refs or already spinning.");
            return;
        }

        int targetIndex = ChooseWeightedIndex();
        float targetCenter = GetSliceCenterAngle(targetIndex);
        float extra = Random.Range(extraSpinsRange.x, extraSpinsRange.y) * 360f;
        float finalAngle = targetCenter + extra;

        SpinTo(finalAngle, targetIndex); // DOTween versiyonu
    }

    int SliceCount => (config?.slices?.Length).GetValueOrDefault(0);
    float Step() => 360f / Mathf.Max(1, SliceCount);
    float GetSliceCenterAngle(int index) => (index + 0.5f) * Step();

    int ChooseWeightedIndex()
    {
        bool blockBombs = (UIManager.Instance.currentLevel % 5 == 0 || UIManager.Instance.currentLevel % 30 == 0);
        float total = 0f;
        for (int i = 0; i < SliceCount; i++)
        {
            if (blockBombs && config.slices[i].isBomb) continue;
            total += Mathf.Max(0.0001f, config.slices[i].rewardWeight);
        }

        float r = Random.Range(0f, total), acc = 0f;
        for (int i = 0; i < SliceCount; i++)
        {
            if (blockBombs && config.slices[i].isBomb) continue;
            acc += Mathf.Max(0.0001f, config.slices[i].rewardWeight);
            if (r <= acc) return i;
        }
        return SliceCount - 1;
    }

    // DOTween ile spin
    private void SpinTo(float finalAngle, int targetIndex)
    {
        spinning = true;
        float totalAngle = finalAngle - lastAngle;

        // DOTween rotasyon
        wheelVisual.DOLocalRotate(new Vector3(0, 0, -finalAngle), spinDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                spinning = false;
                lastAngle = finalAngle % 360f;

                SliceData slice = config.slices[targetIndex];
                bool isBomb = slice.isBomb;
                string rewardName = isBomb ? "BOMB" : (string.IsNullOrEmpty(slice.rewardName) ? "Reward" : slice.rewardName);
                Sprite rewardIcon = isBomb ? null : slice.rewardIcon;

                int lvl = UIManager.Instance.currentLevel;

                float mult = 1f + 0.10f * (lvl - 1);   // her level +%10 örnek
                if (lvl % 30 == 0) mult *= 3f;    // 30’un katlarında 3x (örnek)
                else if (lvl % 5 == 0) mult *= 1.5f;  // 5’in katlarında 1.5x (örnek)

                int amount = isBomb ? 0
                    : Mathf.RoundToInt(slice.rewardAmount * mult);


                UIManager ui = FindObjectOfType<UIManager>(true);
                if (ui != null) ui.OnSpinResolved(isBomb, rewardName, rewardIcon, amount, slice.rewardParticleImage);
                else Debug.LogError("[Wheel] UIManager not found in scene.");
            });
    }
}
