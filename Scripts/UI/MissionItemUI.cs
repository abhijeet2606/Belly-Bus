using UnityEngine;
using UnityEngine.UI;
using System;

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

    private string currentDescription;
    private int currentProgress;
    private int currentTarget;
    private int currentReward;
    private Sprite currentRewardIcon;
    private bool isMissionComplete;
    private bool isMissionClaimed;

    public Action OnClaimRequested;

    public void SetData(string description, int current, int target, int reward, bool claimed, Sprite rewardIcon = null)
    {
        currentDescription = description;
        currentProgress = current;
        currentTarget = target;
        currentReward = reward;
        currentRewardIcon = rewardIcon;
        isMissionClaimed = claimed;

        if (MissionDescription != null) MissionDescription.text = description;
        
        // Cap displayed progress at target
        int displayProgress = Mathf.Min(current, target);

        // Calculate and set progress bar value
        if (ProgressSlider != null)
        {
            ProgressSlider.minValue = 0;
            ProgressSlider.maxValue = target;
            ProgressSlider.value = displayProgress;
            ProgressSlider.interactable = false; // Make progress bar non-interactable

            // Ensure slider children don't block raycasts
            Image[] images = ProgressSlider.GetComponentsInChildren<Image>();
            foreach(var img in images) img.raycastTarget = false;
        }

        // Set the text to "current/target" (e.g., 100/100) - capped at target
        if (ProgressText != null)
        {
            ProgressText.text = $"{displayProgress}/{target}";
            ProgressText.raycastTarget = false; // Don't block background button
        }

        if (RewardAmount != null) 
        {
            RewardAmount.text = reward.ToString();
            RewardAmount.raycastTarget = false;
        }
        
        if (RewardIcon != null && rewardIcon != null) 
        {
            RewardIcon.sprite = rewardIcon;
            RewardIcon.raycastTarget = false;
        }

        if (MissionDescription != null) MissionDescription.raycastTarget = false;

        isMissionComplete = target > 0 && current >= target;

        // Visual checkmark only appears if the mission has been successfully claimed.
        if (Checkmark != null) Checkmark.SetActive(isMissionClaimed);

        // Claim Button logic:
        // The button is always interactable unless the mission has already been claimed.
        // The OnClaimRequested action will handle the logic of whether the claim is valid.
        if (ClaimButton != null) 
        {
            ClaimButton.gameObject.SetActive(true);
            ClaimButton.interactable = !isMissionClaimed;
            
            ClaimButton.onClick.RemoveAllListeners();
            if (!isMissionClaimed)
            {
                ClaimButton.onClick.AddListener(() => InvokeClaim());
            }
        }

        // If the whole task item has a button component, use it as a claim button too.
        Button parentButton = GetComponent<Button>();
        if(parentButton != null) 
        {
            parentButton.interactable = !isMissionClaimed;
            parentButton.onClick.RemoveAllListeners();
            if (!isMissionClaimed)
            {
                parentButton.onClick.AddListener(() => InvokeClaim());
            }
        }
    }

    private void InvokeClaim()
    {
        Debug.Log($"[MissionUI] Clicked task item: {currentDescription}");
        OnClaimRequested?.Invoke();
    }

    public void OnClaimClicked()
    {
        // Reward logic is handled by MissionUIManager.OnMissionClaimed listener.
        // We just keep this method as a placeholder if it's used in the inspector.
    }
}
