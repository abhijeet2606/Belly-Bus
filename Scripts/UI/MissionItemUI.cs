using UnityEngine;
using UnityEngine.UI;

public class MissionItemUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Text MissionDescription;
    public Slider ProgressSlider;
    public Text ProgressText;
    public Image RewardIcon;
    public Text RewardAmount;
    public GameObject Checkmark;
    public Button ClaimButton;

    public void SetData(string description, int current, int target, int reward, Sprite rewardIcon = null)
    {
        if (MissionDescription != null) MissionDescription.text = description;
        
        // Calculate and set progress bar value
        if (ProgressSlider != null)
        {
            float percentage = (target > 0) ? (float)current / target : 0f;
            ProgressSlider.value = Mathf.Clamp01(percentage);
        }

        // Set the text to "current/target" (e.g., 15/30)
        if (ProgressText != null)
        {
            ProgressText.text = $"{current}/{target}";
        }

        if (RewardAmount != null) RewardAmount.text = reward.ToString();
        if (RewardIcon != null && rewardIcon != null) RewardIcon.sprite = rewardIcon;

        bool isComplete = target > 0 && current >= target;
        if (Checkmark != null) Checkmark.SetActive(isComplete);
        if (ClaimButton != null) ClaimButton.gameObject.SetActive(isComplete);

        // Visual tweaks for completion
        if (isComplete)
        {
            // Optional: Darken background or change color to show it's done
            Image bg = GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        }
    }

    public void OnClaimClicked()
    {
        // Placeholder for claim reward logic
        Debug.Log("Claimed Reward!");
        // Disable claim button after click
        if (ClaimButton != null) ClaimButton.interactable = false;
    }
}
