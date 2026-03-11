using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MissionUIManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject MissionPanel;
    public GameObject DailyPanel;
    public GameObject WeeklyPanel;
    public GameObject MonthlyPanel;
    public GameObject RoadmapHeader; // For the 1, 2, 3... 7 progress view

    [Header("Tab Buttons")]
    public Button DailyTabButton;
    public Button WeeklyTabButton;
    public Button MonthlyTabButton;

    [Header("Tab Visuals")]
    public Color ActiveTabColor = Color.green;
    public Color InactiveTabColor = Color.white;
    public Sprite ActiveTabSprite;
    public Sprite InactiveTabSprite;

    [Header("Daily Progress Roadmap")]
    public List<ProgressIconUI> ProgressIcons;

    [Header("Mission Prefabs & Containers")]
    public GameObject TaskPrefab;
    public Transform DailyTaskContainer;
    public Transform WeeklyTaskContainer;
    public Transform MonthlyTaskContainer;

    private void Start()
    {
        // Initialize by showing Daily tab
        ShowDailyTab();
        UpdateRoadmap(3); // Example: Player has completed 3 days

        // Example usage:
        StartCoroutine(FetchAndDisplayMissions());
    }

    private MissionsApiResponse currentResponse;
    private List<ChallengeDTO> dailyQueue = new List<ChallengeDTO>();
    private List<ChallengeDTO> weeklyQueue = new List<ChallengeDTO>();
    private List<ChallengeDTO> monthlyQueue = new List<ChallengeDTO>();

    private IEnumerator FetchAndDisplayMissions()
    {
        string jsonResponse = GetSampleJsonResponse();
        currentResponse = JsonUtility.FromJson<MissionsApiResponse>(jsonResponse);

        if (currentResponse != null && currentResponse.success)
        {
            // Process Daily
            if (currentResponse.data.daily != null && currentResponse.data.daily.days.Length > 0)
            {
                dailyQueue = currentResponse.data.daily.days[0].challenges
                    .OrderBy(c => (int)c.difficulty)
                    .ToList();
            }

            // Process Weekly
            if (currentResponse.data.weekly != null && currentResponse.data.weekly.days.Length > 0)
            {
                weeklyQueue = currentResponse.data.weekly.days[0].challenges
                    .OrderBy(c => (int)c.difficulty)
                    .ToList();
            }

            // Process Monthly
            if (currentResponse.data.monthly != null && currentResponse.data.monthly.days.Length > 0)
            {
                monthlyQueue = currentResponse.data.monthly.days[0].challenges
                    .OrderBy(c => (int)c.difficulty)
                    .ToList();
            }

            RefreshAllPanels();
        }
        yield return null;
    }

    private void RefreshAllPanels()
    {
        RefreshPanel(DailyTaskContainer, dailyQueue);
        RefreshPanel(WeeklyTaskContainer, weeklyQueue);
        RefreshPanel(MonthlyTaskContainer, monthlyQueue);
    }

    private void RefreshPanel(Transform container, List<ChallengeDTO> queue)
    {
        if (container == null) return;

        // Clear container
        foreach (Transform child in container) Destroy(child.gameObject);

        // Generate ALL missions but only show top 3
        int visibleCount = 0;
        foreach (var challenge in queue)
        {
            if (IsMissionComplete(challenge)) continue;

            GameObject taskGO = Instantiate(TaskPrefab, container);
            MissionItemUI missionItem = taskGO.GetComponent<MissionItemUI>();

            int playerProgress = GetPlayerProgress(challenge.challengeId);
            Sprite entityIcon = GetEntityIcon(challenge.entityType, challenge.entity);
            
            missionItem.SetData(challenge.title, playerProgress, challenge.target, challenge.rewardCoins, entityIcon);
            
            if (visibleCount < 3)
            {
                taskGO.SetActive(true);
                visibleCount++;
            }
            else
            {
                taskGO.SetActive(false);
            }

            Button claimBtn = missionItem.ClaimButton;
            if (claimBtn != null)
            {
                claimBtn.onClick.AddListener(() => OnMissionClaimed(challenge, container, queue));
            }
        }
    }

    private void OnMissionClaimed(ChallengeDTO challenge, Transform container, List<ChallengeDTO> queue)
    {
        MarkMissionAsComplete(challenge.challengeId);
        RefreshPanel(container, queue);
    }

    // --- Placeholder Functions for Progress Tracking ---

    private int GetPlayerProgress(string challengeId)
    {
        // In a real game, you would fetch this from a save file or a progress manager
        // For testing, we can return a mock value.
        if (challengeId == "69a3ede367eea95a80ab96e9") return 5; // Example: "Use OVEN 5 times" is complete
        return 0;
    }

    private bool IsMissionComplete(ChallengeDTO challenge)
    {
        // Check against a list of claimed mission IDs
        return PlayerPrefs.HasKey("MissionClaimed_" + challenge.challengeId);
    }

    private void MarkMissionAsComplete(string challengeId)
    {
        PlayerPrefs.SetInt("MissionClaimed_" + challengeId, 1);
        PlayerPrefs.Save();
    }

    private Sprite GetEntityIcon(MissionEntityType entityType, int entityCode)
    {
        // This is where you would return the actual sprite based on the code.
        // For example:
        // if (entityType == MissionEntityType.FOOD)
        // {
        //     FoodType food = (FoodType)entityCode;
        //     return foodSpriteList.Find(s => s.name == food.ToString());
        // }
        return null;
    }

    private string GetSampleJsonResponse()
    {
        return @"{
            ""success"": true,
            ""data"": {
                ""daily"": {
                    ""_id"": ""69acbc02ed9fafc7169e7dc9"",
                    ""days"": [
                        {
                            ""day"": 1,
                            ""challenges"": [
                                { ""challengeId"": ""d1"", ""title"": ""Collect 30 LETTUCE"", ""target"": 30, ""rewardCoins"": 60, ""difficulty"": 8100 },
                                { ""challengeId"": ""d2"", ""title"": ""Collect 30 BURGER"", ""target"": 30, ""rewardCoins"": 60, ""difficulty"": 8100 },
                                { ""challengeId"": ""d3"", ""title"": ""Use OVEN 5 times"", ""target"": 5, ""rewardCoins"": 25, ""difficulty"": 8100 },
                                { ""challengeId"": ""d4"", ""title"": ""Use KNIFE 8 times"", ""target"": 8, ""rewardCoins"": 40, ""difficulty"": 8101 },
                                { ""challengeId"": ""d5"", ""title"": ""Collect 50 LETTUCE"", ""target"": 50, ""rewardCoins"": 100, ""difficulty"": 8101 },
                                { ""challengeId"": ""d6"", ""title"": ""Collect 80 BURGER"", ""target"": 80, ""rewardCoins"": 160, ""difficulty"": 8102 }
                            ]
                        }
                    ]
                },
                ""weekly"": {
                    ""_id"": ""69acbc03ed9fafc7169e7dcf"",
                    ""days"": [
                        {
                            ""day"": 1,
                            ""challenges"": [
                                { ""challengeId"": ""w1"", ""title"": ""Trigger Bee 10 times"", ""target"": 10, ""rewardCoins"": 30, ""difficulty"": 8100 },
                                { ""challengeId"": ""w2"", ""title"": ""Collect 100 items"", ""target"": 100, ""rewardCoins"": 300, ""difficulty"": 8100 },
                                { ""challengeId"": ""w3"", ""title"": ""Collect 150 ONION"", ""target"": 150, ""rewardCoins"": 300, ""difficulty"": 8100 },
                                { ""challengeId"": ""w4"", ""title"": ""Collect 225 LETTUCE"", ""target"": 225, ""rewardCoins"": 450, ""difficulty"": 8101 },
                                { ""challengeId"": ""w5"", ""title"": ""Use FLIES 30 times"", ""target"": 30, ""rewardCoins"": 150, ""difficulty"": 8101 },
                                { ""challengeId"": ""w6"", ""title"": ""Score 75000 points"", ""target"": 75000, ""rewardCoins"": 225000, ""difficulty"": 8102 }
                            ]
                        }
                    ]
                },
                ""monthly"": {
                    ""_id"": ""69b11be4ed9fafc7169e7ddd"",
                    ""days"": [
                        {
                            ""day"": 1,
                            ""challenges"": [
                                { ""challengeId"": ""m1"", ""title"": ""Use OVEN 50 times"", ""target"": 50, ""rewardCoins"": 250, ""difficulty"": 8100 },
                                { ""challengeId"": ""m2"", ""title"": ""Collect 250 BURGER"", ""target"": 250, ""rewardCoins"": 500, ""difficulty"": 8100 },
                                { ""challengeId"": ""m3"", ""title"": ""Use OVEN 30 times"", ""target"": 30, ""rewardCoins"": 150, ""difficulty"": 8100 },
                                { ""challengeId"": ""m4"", ""title"": ""Use PAN 60 times"", ""target"": 60, ""rewardCoins"": 300, ""difficulty"": 8101 },
                                { ""challengeId"": ""m5"", ""title"": ""Collect 500 ONION"", ""target"": 500, ""rewardCoins"": 1000, ""difficulty"": 8101 },
                                { ""challengeId"": ""m6"", ""title"": ""Complete 60 levels"", ""target"": 60, ""rewardCoins"": 180, ""difficulty"": 8102 }
                            ]
                        }
                    ]
                }
            }
        }";
    }

    public void UpdateRoadmap(int completedDays)
    {
        for (int i = 0; i < ProgressIcons.Count; i++)
        {
            bool isCompleted = i < completedDays;
            bool isActive = i == completedDays;
            ProgressIcons[i].SetState(isCompleted, isActive);
        }
    }

    public void OpenMissionPanel()
    {
        if (MissionPanel != null) MissionPanel.SetActive(true);
        ShowDailyTab();
    }

    public void CloseMissionPanel()
    {
        if (MissionPanel != null) MissionPanel.SetActive(false);
    }

    public void ShowDailyTab()
    {
        SetPanelActive(DailyPanel, true);
        SetPanelActive(WeeklyPanel, false);
        SetPanelActive(MonthlyPanel, false);
        SetPanelActive(RoadmapHeader, true); // Show roadmap for daily missions
        
        UpdateTabVisuals(DailyTabButton, true);
        UpdateTabVisuals(WeeklyTabButton, false);
        UpdateTabVisuals(MonthlyTabButton, false);
    }

    public void ShowWeeklyTab()
    {
        SetPanelActive(DailyPanel, false);
        SetPanelActive(WeeklyPanel, true);
        SetPanelActive(MonthlyPanel, false);
        SetPanelActive(RoadmapHeader, false); // Hide roadmap for weekly missions

        UpdateTabVisuals(DailyTabButton, false);
        UpdateTabVisuals(WeeklyTabButton, true);
        UpdateTabVisuals(MonthlyTabButton, false);
    }

    public void ShowMonthlyTab()
    {
        SetPanelActive(DailyPanel, false);
        SetPanelActive(WeeklyPanel, false);
        SetPanelActive(MonthlyPanel, true);
        SetPanelActive(RoadmapHeader, false); // Hide roadmap for monthly missions

        UpdateTabVisuals(DailyTabButton, false);
        UpdateTabVisuals(WeeklyTabButton, false);
        UpdateTabVisuals(MonthlyTabButton, true);
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }

    private void UpdateTabVisuals(Button tabButton, bool active)
    {
        if (tabButton == null) return;
        
        Image img = tabButton.GetComponent<Image>();
        if (img != null)
        {
            if (active && ActiveTabSprite != null) img.sprite = ActiveTabSprite;
            else if (!active && InactiveTabSprite != null) img.sprite = InactiveTabSprite;
            
            img.color = active ? ActiveTabColor : InactiveTabColor;
        }

        // Optional: Scale effect or different font weight
        tabButton.transform.localScale = active ? Vector3.one * 1.05f : Vector3.one;
    }
}
