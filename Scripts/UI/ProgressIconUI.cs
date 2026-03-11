using UnityEngine;
using UnityEngine.UI;

public class ProgressIconUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Image Background;
    public Text DayNumberText;
    public GameObject CompletedCheckmark;

    [Header("State Visuals")]
    public Sprite ActiveSprite;
    public Sprite InactiveSprite;
    public Sprite CompletedSprite;

    public void SetState(bool isCompleted, bool isActive)
    {
        CompletedCheckmark.SetActive(isCompleted);
        DayNumberText.gameObject.SetActive(!isCompleted);

        if (isCompleted && CompletedSprite != null)
        {
            Background.sprite = CompletedSprite;
        }
        else if (isActive && ActiveSprite != null)
        {
            Background.sprite = ActiveSprite;
        }
        else if (InactiveSprite != null)
        {
            Background.sprite = InactiveSprite;
        }
    }
}
