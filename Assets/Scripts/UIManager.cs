using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;
using AssetKits.ParticleImage;

public class UIManager : MonoBehaviour
{
    // persist
    private const string PREF_SAVE = "RiskySpin_SaveState";

    [System.Serializable] private class RewardSave { public string key; public int total; public int count; }

    [System.Serializable]
    private class SaveState
    {
        public int level = 1;
        public List<RewardSave> rewards = new List<RewardSave>();
    }

    public static UIManager Instance { get; private set; }

    [SerializeField] private GameObject canvas;

    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Color rewardTextActiveColor;
    [SerializeField] private Color rewardTextDeActiveColor;
    [SerializeField] private float moveDistance = 200f;   
    [SerializeField] private float duration = 1.5f;
    private Vector3 initialPos;

    // UI refs
    [Header("Zone Texts")]
    [SerializeField] private TextMeshProUGUI superZoneText;
    [SerializeField] private TextMeshProUGUI safeZoneText;

    [Header("Reward List (Left)")]
    [SerializeField] private Transform rewardListContent;
    [SerializeField] private GameObject rewardRowPrefab;

    [Header("Level Bar (Top)")]
    [SerializeField] private Transform levelBarContent;
    [SerializeField] private GameObject levelCellPrefab;
    [SerializeField] private int levelWindowSize = 10;

    [Header("allSpinBasesAndIndicators")]
    [SerializeField] private GameObject[] normalSpinBaseIndicators;
    [SerializeField] private GameObject[] superZoneSpinBaseIndicators;
    [SerializeField] private GameObject[] safeZoneSpinBaseIndicators;

    // state
    public int currentLevel = 1;
    private int windowStart = 1;

    private Button spinButton;

    // rows cache (one per reward key)
    private class RewardRow
    {
        public GameObject go;
        public TextMeshProUGUI text;
        public Image icon;
        public int total;
        public int count;
    }
    private readonly Dictionary<string, RewardRow> rows = new Dictionary<string, RewardRow>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        if (rewardText != null)
            initialPos = rewardText.rectTransform.anchoredPosition;

        if (spinButton == null)
        {
            GameObject btnObj = GameObject.FindGameObjectWithTag("spinButton");
            if (btnObj != null)
                spinButton = btnObj.GetComponent<Button>();
        }

        spinButton.onClick.AddListener(OnSpinClicked);

    }

    private void Start() {

        LoadState();
    }


    private void OnSpinClicked()
    {
        WheelController.Instance.Spin();
    }

    public void OnSpinResolved(bool isBomb, string rewardName, Sprite rewardIcon, int amount, ParticleImage rewardParticle)
    {
        if (isBomb)
        {
            ResetState();

            rewardParticlePlayer(rewardParticle, true);
            return;
        }

        currentLevel = Mathf.Max(1, currentLevel + 1);
        SetLevelUI(currentLevel);
        UpsertRewardRow(rewardName, rewardIcon, amount, rewardParticle, isBomb);
        ShowReward("+" + amount + " " + rewardName);

        SaveStateNow();
    }

    private void rewardParticlePlayer(ParticleImage particleImage, bool isBomb, Transform particleDest =null)
    {
        ParticleImage particle = Instantiate(particleImage, canvas.transform);

        particle.rectTransform.anchoredPosition = Vector2.zero;

        if(!isBomb)
            particle.attractorTarget = particleDest;

        DOVirtual.DelayedCall(2f, () => {

            particle.Stop();
            Destroy(particle);
        });

    }

    // --- level UI ---
    private void SetLevelUI(int level)
    {
        int windowEnd = windowStart + levelWindowSize - 1;
        if (level > windowEnd) windowStart = level - levelWindowSize + 1;
        else if (level < windowStart) windowStart = level;

        RebuildLevelBar(level);

        if (safeZoneText) safeZoneText.text = $"Next Safe Zone: {((level / 5) + 1) * 5}";
        if (superZoneText) superZoneText.text = $"Next Super Zone: {((level / 30) + 1) * 30}";
    }

    private void RebuildLevelBar(int level)
    {
        if (!levelBarContent || !levelCellPrefab) return;

        // ensure exactly levelWindowSize children
        int childCount = levelBarContent.childCount;

        // create missing
        for (int i = childCount; i < levelWindowSize; i++)
            Instantiate(levelCellPrefab, levelBarContent);

        // remove extras
        for (int i = levelWindowSize; i < childCount; i++)
            Destroy(levelBarContent.GetChild(i).gameObject);

        // update cells
        for (int i = 0; i < levelWindowSize; i++)
        {
            int lv = Mathf.Max(1, windowStart + i);
            Transform cellT = levelBarContent.GetChild(i);
            GameObject cellGO = cellT.gameObject;

            // label
            TextMeshProUGUI label = cellGO.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label) label.text = lv.ToString();

            // highlight
            LevelCellUI cell = cellGO.GetComponent<LevelCellUI>();
            bool isActive = (lv == level);

            cell.SetNumber(lv);
            cell.SetActive(isActive);

            // set text color here
            if (label) label.color = isActive ? Color.green : Color.white;

        }

        allSpinItemsClosed();

        if (currentLevel % 5 == 0 && currentLevel % 30 != 0)
        {
            foreach (GameObject spin in safeZoneSpinBaseIndicators)
            {
                spin.SetActive(true);
            }

        }

        else if (currentLevel % 30 == 0)
        {
            foreach (GameObject spin in superZoneSpinBaseIndicators)
            {
                spin.SetActive(true);
            }

        }

        else
        {
            foreach (GameObject spin in normalSpinBaseIndicators)
            {
                spin.SetActive(true);
            }

        }
    }

    void allSpinItemsClosed()
    {
        foreach (GameObject spin in normalSpinBaseIndicators)
        {
            spin.SetActive(false);
        }

        foreach (GameObject spin in safeZoneSpinBaseIndicators)
        {
            spin.SetActive(false);
        }

        foreach (GameObject spin in superZoneSpinBaseIndicators)
        {
            spin.SetActive(false);
        }
    }


    // --- reward list ---
    private void UpsertRewardRow(string key, Sprite icon, int delta, ParticleImage rewardParticle, bool isBomb)
    {
        if (!rewardListContent || !rewardRowPrefab) return;

        if (string.IsNullOrEmpty(key)) key = "Reward";

        bool exists = rows.TryGetValue(key, out RewardRow row);

        if (!exists)
        {
            GameObject rowGO = Instantiate(rewardRowPrefab, rewardListContent);
            TextMeshProUGUI tmp = rowGO.GetComponentInChildren<TextMeshProUGUI>();
            Image img = rowGO.GetComponentInChildren<Image>();

            row = new RewardRow { go = rowGO, text = tmp, icon = img, total = 0, count = 0 };
            rows.Add(key, row);

            // set icon first time if available
            if (img && icon) img.sprite = icon;
        }

        rewardParticlePlayer(rewardParticle, isBomb, row.icon.transform);

        row.total += Mathf.Max(0, delta);

        // if icon still empty, fill with provided icon
        if (row.icon && row.icon.sprite == null && icon) row.icon.sprite = icon;

        if (row.text) row.text.text = $"TOTAL : {row.total}";
    }

    public void ShowReward(string text)
    {
        if (rewardText == null) return;

        // Text ayarla
        rewardText.text = text;
        rewardText.color = rewardTextActiveColor;
        rewardText.rectTransform.anchoredPosition = initialPos;

        // DOTween animasyonu
        rewardText.rectTransform
            .DOAnchorPosY(initialPos.y - moveDistance, duration)
            .SetEase(Ease.OutCubic);

        rewardText
            .DOColor(rewardTextDeActiveColor, duration)
            .SetEase(Ease.Linear);
    }

    private void ClearRewardList()
    {
        if (rewardListContent)
            foreach (Transform child in rewardListContent) Destroy(child.gameObject);
        rows.Clear();
    }

    // --- persistence ---
    private void SaveStateNow()
    {
        SaveState state = new SaveState { level = currentLevel };
        foreach (KeyValuePair<string, RewardRow> kv in rows)
            state.rewards.Add(new RewardSave { key = kv.Key, total = kv.Value.total, count = kv.Value.count });

        PlayerPrefs.SetString(PREF_SAVE, JsonUtility.ToJson(state));
        PlayerPrefs.Save();
    }

    private void LoadState()
    {
        ClearRewardList();

        SaveState state = null;
        if (PlayerPrefs.HasKey(PREF_SAVE))
        {
            string json = PlayerPrefs.GetString(PREF_SAVE, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { state = JsonUtility.FromJson<SaveState>(json); } catch { state = null; }
            }
        }

        currentLevel = (state != null) ? Mathf.Max(1, state.level) : 1;
        windowStart = Mathf.Max(1, currentLevel - levelWindowSize + 1);
        SetLevelUI(currentLevel);

        if (state == null) return;

        // build a quick name->icon map from WheelConfig via WheelController in scene
        Dictionary<string, Sprite> iconMap = new Dictionary<string, Sprite>();
        WheelController wc = FindObjectOfType<WheelController>(true);
        if (wc != null)
        {
            // WheelController has [SerializeField] WheelConfig config
            var cfgField = typeof(WheelController).GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            WheelConfig cfg = cfgField != null ? (WheelConfig)cfgField.GetValue(wc) : null;
            if (cfg != null && cfg.slices != null)
            {
                foreach (var s in cfg.slices)
                {
                    if (s == null || s.isBomb) continue;
                    string key = string.IsNullOrEmpty(s.rewardName) ? "Reward" : s.rewardName;
                    if (!iconMap.ContainsKey(key) && s.rewardIcon != null)
                        iconMap.Add(key, s.rewardIcon);
                }
            }
        }

        // rebuild rows using saved totals and set icons from config map
        foreach (RewardSave rs in state.rewards)
        {
            if (rs.total <= 0 && rs.count <= 0) continue;

            GameObject rowGO = Instantiate(rewardRowPrefab, rewardListContent);
            TextMeshProUGUI tmp = rowGO.GetComponentInChildren<TextMeshProUGUI>();
            Image img = rowGO.GetComponentInChildren<Image>();

            RewardRow row = new RewardRow { go = rowGO, text = tmp, icon = img, total = rs.total, count = rs.count };
            rows[rs.key] = row;

            if (tmp) tmp.text = $"Total: {rs.total}";

            // FORCE assign icon (same behavior as runtime add)
            if (img && iconMap.TryGetValue(rs.key, out Sprite iconFromCfg))
                img.sprite = iconFromCfg;
        }

    }

    private void ResetState()
    {
        PlayerPrefs.DeleteKey(PREF_SAVE);
        PlayerPrefs.Save();

        currentLevel = 1;
        windowStart = 1;

        ClearRewardList();
        SetLevelUI(currentLevel);
    }
}
