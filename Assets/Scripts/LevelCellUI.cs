using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelCellUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI numberText;
    [SerializeField] private Image highlight;

    public void SetNumber(int n)
    {
        if (numberText) numberText.text = n.ToString();
    }

    public void SetActive(bool active)
    {
        if (highlight) highlight.enabled = active;
        transform.localScale = active ? Vector3.one * 1.08f : Vector3.one;
    }
}
