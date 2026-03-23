
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HelperUIManager : MonoBehaviour
{
    [Header("References")]
    public PowerupManager powerupManager;

    [Header("Buttons")]
    public Button hammerButton;
    public Button horizontalKnifeButton;
    public Button blenderButton;
    public Button verticalKnifeButton;

    [Header("Count Texts")]
    public Text hammerCountText;
    public Text horizontalKnifeCountText;
    public Text blenderCountText;
    public Text verticalKnifeCountText;

    [Header("Selected Visuals (Child Images)")]
    public GameObject hammerSelectedIcon;
    public GameObject horizontalKnifeSelectedIcon;
    public GameObject blenderSelectedIcon;
    public GameObject verticalKnifeSelectedIcon;

    private void Start()
    {
        if (hammerButton != null) hammerButton.onClick.AddListener(() => OnHelperClicked(PowerupType.Hammer));
        if (horizontalKnifeButton != null) horizontalKnifeButton.onClick.AddListener(() => OnHelperClicked(PowerupType.HorizontalKnife));
        if (blenderButton != null) blenderButton.onClick.AddListener(() => OnHelperClicked(PowerupType.Blender));
        if (verticalKnifeButton != null) verticalKnifeButton.onClick.AddListener(() => OnHelperClicked(PowerupType.VerticalKnife));

        if (powerupManager != null) powerupManager.OnPowerupStateChanged += RefreshUI;

        RefreshUI();
    }

    private void OnDestroy()
    {
        if (powerupManager != null) powerupManager.OnPowerupStateChanged -= RefreshUI;
    }

    private void OnEnable()
    {
        RefreshUI();
    }

    private void OnHelperClicked(PowerupType type)
    {
        if (powerupManager == null) return;

        if (powerupManager.currentActivePowerup == type)
        {
            powerupManager.DeselectPowerup();
        }
        else
        {
            // Check if we have enough count
            if (GetPowerupCount(type) > 0)
            {
                powerupManager.currentActivePowerup = type;
            }
            else
            {
                Debug.LogWarning($"[HelperUI] Not enough {type} powerups!");
            }
        }

        RefreshUI();
    }

    public void RefreshUI()
    {
        UpdateCountTexts();
        UpdateSelectionVisuals();
    }

    private void UpdateCountTexts()
    {
        if (hammerCountText != null) hammerCountText.text = GetPowerupCount(PowerupType.Hammer).ToString();
        if (horizontalKnifeCountText != null) horizontalKnifeCountText.text = GetPowerupCount(PowerupType.HorizontalKnife).ToString();
        if (blenderCountText != null) blenderCountText.text = GetPowerupCount(PowerupType.Blender).ToString();
        if (verticalKnifeCountText != null) verticalKnifeCountText.text = GetPowerupCount(PowerupType.VerticalKnife).ToString();
    }

    private void UpdateSelectionVisuals()
    {
        if (powerupManager == null) return;

        PowerupType active = powerupManager.currentActivePowerup;

        if (hammerSelectedIcon != null) hammerSelectedIcon.SetActive(active == PowerupType.Hammer);
        if (horizontalKnifeSelectedIcon != null) horizontalKnifeSelectedIcon.SetActive(active == PowerupType.HorizontalKnife);
        if (blenderSelectedIcon != null) blenderSelectedIcon.SetActive(active == PowerupType.Blender);
        if (verticalKnifeSelectedIcon != null) verticalKnifeSelectedIcon.SetActive(active == PowerupType.VerticalKnife);
    }

    private int GetPowerupCount(PowerupType type)
    {
        string key = GetPowerupKey(type);
        if (string.IsNullOrEmpty(key)) return 0;
        return ProgressDataManager.EnsureInstance().GetPowerupCount(key);
    }

    private string GetPowerupKey(PowerupType type)
    {
        switch (type)
        {
            case PowerupType.HorizontalKnife: return "Powerup_HorizontalKnife";
            case PowerupType.VerticalKnife: return "Powerup_VerticalKnife";
            case PowerupType.Blender: return "Powerup_Blender";
            case PowerupType.Hammer: return "Powerup_Hammer";
            default: return null;
        }
    }
}
