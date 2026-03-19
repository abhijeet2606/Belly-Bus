using UnityEngine;
using UnityEngine.UI;

public class ProgressIconUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Image Background;
    public Text DayNumberText;
    public GameObject CompletedCheckmark;
    public GameObject LockedIcon;

    [Header("State Visuals")]
    public Sprite BaseSprite;
    public Color ActiveColor = new Color(0.4f, 0.69803923f, 0.003921569f, 1f);
    public Color InactiveColor = Color.white;
    public Color LockedColor = new Color(0.5f, 0.54f, 0.44f, 1f);

    private void Awake()
    {
        AutoBindIfMissing();
    }

    private void AutoBindIfMissing()
    {
        if (Background == null)
        {
            Background = GetComponent<Image>();
            if (Background == null)
            {
                var images = GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    var img = images[i];
                    if (img == null) continue;
                    string n = img.name != null ? img.name.ToLowerInvariant() : "";
                    if (n.Contains("bg") || n.Contains("background"))
                    {
                        Background = img;
                        break;
                    }
                }
                if (Background == null && images.Length > 0) Background = images[0];
            }
        }
        if (DayNumberText == null)
        {
            var texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var t = texts[i];
                if (t == null) continue;
                string n = t.name != null ? t.name.ToLowerInvariant() : "";
                if (n.Contains("day") || n.Contains("num") || n.Contains("number"))
                {
                    DayNumberText = t;
                    break;
                }
            }
            for (int i = 0; i < texts.Length; i++)
            {
                var t = texts[i];
                if (t == null) continue;
                if (t.transform != null && t.transform.parent == transform)
                {
                    DayNumberText = t;
                    break;
                }
            }
            if (DayNumberText == null && texts.Length > 0) DayNumberText = texts[0];
        }

        if (LockedIcon == null)
        {
            var t = transform;
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c == null) continue;
                string n = c.name != null ? c.name.ToLowerInvariant() : "";
                if (n.Contains("lock") || n.Contains("locked") || n.Contains("?"))
                {
                    LockedIcon = c.gameObject;
                    break;
                }
            }
        }

        if (CompletedCheckmark == null)
        {
            var t = transform;
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c == null) continue;
                string n = c.name != null ? c.name.ToLowerInvariant() : "";
                if (n.Contains("check") || n.Contains("tick") || n.Contains("complete"))
                {
                    CompletedCheckmark = c.gameObject;
                    break;
                }
            }
        }
    }

    public void SetState(bool isCompleted, bool isActive, bool isUnlocked)
    {
        if (CompletedCheckmark != null) CompletedCheckmark.SetActive(isCompleted);
        if (LockedIcon != null) LockedIcon.SetActive(!isUnlocked);
        if (DayNumberText != null) DayNumberText.gameObject.SetActive(!isCompleted);

        var targetColor = !isUnlocked ? LockedColor : (isCompleted ? ActiveColor : (isActive ? ActiveColor : InactiveColor));
        if (Background != null)
        {
            if (BaseSprite != null) Background.sprite = BaseSprite;
            Background.color = targetColor;
        }
    }

    public void SetDayNumber(int dayNumber)
    {
        if (DayNumberText != null) DayNumberText.text = dayNumber.ToString();
    }
}
