using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class MissionProgressManager : MonoBehaviour
{
    private static MissionProgressManager _instance;
    private const string ANY = "ANY";
    private const string ChallengeProgressPrefix = "MissionProgressC_";

    public static MissionProgressManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<MissionProgressManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("MissionProgressManager");
                    _instance = go.AddComponent<MissionProgressManager>();
                    DontDestroyOnLoad(go);
                    Debug.Log("[Mission] Auto-created MissionProgressManager instance.");
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public event Action OnAnyProgressChanged;

    private List<ChallengeDTO> activeDailyMissions = new List<ChallengeDTO>();
    private List<ChallengeDTO> activeWeeklyMissions = new List<ChallengeDTO>();
    private List<ChallengeDTO> activeMonthlyMissions = new List<ChallengeDTO>();

    public void ResetState()
    {
        Debug.LogWarning("[Mission] Resetting in-memory mission progress lists.");
        activeDailyMissions.Clear();
        activeWeeklyMissions.Clear();
        activeMonthlyMissions.Clear();
    }

    public void SetActiveMissions(List<ChallengeDTO> daily, List<ChallengeDTO> weekly, List<ChallengeDTO> monthly)
    {
        activeDailyMissions = daily ?? new List<ChallengeDTO>();
        activeWeeklyMissions = weekly ?? new List<ChallengeDTO>();
        activeMonthlyMissions = monthly ?? new List<ChallengeDTO>();
        
        // Initialize current progress from PlayerPrefs for all active missions
        InitializeProgressForMissions(activeDailyMissions);
        InitializeProgressForMissions(activeWeeklyMissions);
        InitializeProgressForMissions(activeMonthlyMissions);

        Debug.Log($"[Mission] Active missions set and progress initialized. Daily: {activeDailyMissions.Count}, Weekly: {activeWeeklyMissions.Count}, Monthly: {activeMonthlyMissions.Count}");
    }

    private void InitializeProgressForMissions(List<ChallengeDTO> missions)
    {
        foreach (var mission in missions)
        {
            mission.currentProgress = GetChallengeProgress(mission.challengeId);
        }
    }

    private int FetchProgressFromPrefs(ChallengeDTO challenge)
    {
        if (challenge == null) return 0;
        return GetChallengeProgress(challenge.challengeId);
    }

    private void UpdateActiveMissionsProgress(TaskType taskType, object entity, int delta)
    {
        UpdateMissionList(activeDailyMissions, taskType, entity, delta);
        UpdateMissionList(activeWeeklyMissions, taskType, entity, delta);
        UpdateMissionList(activeMonthlyMissions, taskType, entity, delta);
    }

    private void UpdateMissionList(List<ChallengeDTO> missions, TaskType taskType, object entity, int delta)
    {
        foreach (var mission in missions)
        {
            bool match = false;
            if (mission.taskType == taskType)
            {
                string missionEntityName = ResolveEntityName(mission);
                if (entity is string entityStr)
                {
                    match = missionEntityName == ANY || missionEntityName.Equals(entityStr, StringComparison.OrdinalIgnoreCase);
                }
                else if (entity is int entityInt)
                {
                    match = missionEntityName == ANY || mission.entity == entityInt;
                }
            }

            if (match)
            {
                mission.currentProgress = Mathf.Max(0, mission.currentProgress + delta);
                SetChallengeProgress(mission.challengeId, mission.currentProgress);
            }
        }
    }

    public List<ChallengeDTO> GetActiveMissions(PeriodType period)
    {
        switch (period)
        {
            case PeriodType.DAILY: return activeDailyMissions;
            case PeriodType.WEEKLY: return activeWeeklyMissions;
            case PeriodType.MONTHLY: return activeMonthlyMissions;
            default: return new List<ChallengeDTO>();
        }
    }

    public void OnItemCollected(FoodType foodType)
    {
        OnItemCollected(foodType.ToString());
    }

    public void OnItemCollected(string foodName)
    {
        if (string.IsNullOrEmpty(foodName)) foodName = ANY;

        string normalized = foodName.Trim().ToUpperInvariant().Replace("CHESSE", "CHEESE");
        
        // We only call this once. UpdateMissionList will handle the logic for specific or ANY food types.
        UpdateActiveMissionsProgress(TaskType.COLLECT_ITEM, normalized, 1);

        FoodType foodType;
        if (TryParseFoodType(normalized, out foodType))
        {
            // We also update with the numeric code for missions that might use it
            UpdateActiveMissionsProgress(TaskType.COLLECT_ITEM, (int)foodType, 1);
        }

        PlayerPrefs.Save();
        OnAnyProgressChanged?.Invoke();
    }

    public void OnItemsCollectedBatch(Dictionary<string, int> foodCounts)
    {
        if (foodCounts == null || foodCounts.Count == 0) return;

        foreach (var kv in foodCounts)
        {
            int delta = kv.Value;
            if (delta <= 0) continue;

            string name = string.IsNullOrEmpty(kv.Key) ? ANY : kv.Key.Trim().ToUpperInvariant().Replace("CHESSE", "CHEESE");
            
            UpdateActiveMissionsProgress(TaskType.COLLECT_ITEM, name, delta);

            FoodType ft;
            if (TryParseFoodType(name, out ft))
            {
                UpdateActiveMissionsProgress(TaskType.COLLECT_ITEM, (int)ft, delta);
            }
        }

        PlayerPrefs.Save();
        OnAnyProgressChanged?.Invoke();
    }

    public void OnPowerupUsed(MissionPowerupType powerupType)
    {
        UpdateActiveMissionsProgress(TaskType.USE_POWERUP, (int)powerupType, 1);
        UpdateActiveMissionsProgress(TaskType.USE_POWERUP, powerupType.ToString(), 1);

        PlayerPrefs.Save();
        OnAnyProgressChanged?.Invoke();
    }

    public void OnLevelCompleted(int level)
    {
        UpdateActiveMissionsProgress(TaskType.COMPLETE_LEVEL, 0, 1);

        PlayerPrefs.Save();
        OnAnyProgressChanged?.Invoke();
    }

    public void OnScorePoints(int delta)
    {
        UpdateActiveMissionsProgress(TaskType.SCORE_POINTS, 0, delta);

        PlayerPrefs.Save();
        OnAnyProgressChanged?.Invoke();
    }

    public void OnWinStreak(int streak)
    {
        if (streak < 0) streak = 0;
        UpdateWinStreakMissions(streak);
        PlayerPrefs.Save();
        OnAnyProgressChanged?.Invoke();
    }

    private void UpdateWinStreakMissions(int streak)
    {
        UpdateWinStreakList(activeDailyMissions, streak);
        UpdateWinStreakList(activeWeeklyMissions, streak);
        UpdateWinStreakList(activeMonthlyMissions, streak);
    }

    private void UpdateWinStreakList(List<ChallengeDTO> missions, int streak)
    {
        foreach (var mission in missions)
        {
            if (mission.taskType == TaskType.WIN_STREAK)
            {
                mission.currentProgress = streak;
                SetChallengeProgress(mission.challengeId, mission.currentProgress);
            }
        }
    }

    public void OnTriggerBee()
    {
        UpdateActiveMissionsProgress(TaskType.TRIGGER_BEE, 0, 1);

        PlayerPrefs.Save();
        OnAnyProgressChanged?.Invoke();
    }

    public void OnClearRow()
    {
        UpdateActiveMissionsProgress(TaskType.CLEAR_ROW, 0, 1);

        PlayerPrefs.Save();
        OnAnyProgressChanged?.Invoke();
    }

    public void OnClearColumn()
    {
        UpdateActiveMissionsProgress(TaskType.CLEAR_COLUMN, 0, 1);

        PlayerPrefs.Save();
        OnAnyProgressChanged?.Invoke();
    }

    public void OnFinishWithMovesLeft(int moves)
    {
        UpdateActiveMissionsProgress(TaskType.FINISH_WITH_MOVES_LEFT, 0, moves);

        PlayerPrefs.Save();
        OnAnyProgressChanged?.Invoke();
    }

    public int GetMissionProgress(ChallengeDTO challenge)
    {
        if (challenge == null) return 0;

        // If we have active missions, they might already have the up-to-date progress
        var allActive = activeDailyMissions.Concat(activeWeeklyMissions).Concat(activeMonthlyMissions);
        var active = allActive.FirstOrDefault(m => m.challengeId == challenge.challengeId);
        if (active != null)
        {
            return active.currentProgress;
        }

        return FetchProgressFromPrefs(challenge);
    }

    public void ResetProgressForChallenge(ChallengeDTO challenge)
    {
        if (challenge == null) return;
        DeleteChallengeProgress(challenge.challengeId);

        // Also reset the in-memory progress for the active mission instance
        var allActive = activeDailyMissions.Concat(activeWeeklyMissions).Concat(activeMonthlyMissions);
        var active = allActive.FirstOrDefault(m => m.challengeId == challenge.challengeId);
        if (active != null)
        {
            active.currentProgress = 0;
        }

        PlayerPrefs.Save();
        OnAnyProgressChanged?.Invoke();
    }

    private int GetChallengeProgress(string challengeId)
    {
        if (string.IsNullOrEmpty(challengeId)) return 0;
        return PlayerPrefs.GetInt(ChallengeProgressPrefix + challengeId, 0);
    }

    private void SetChallengeProgress(string challengeId, int progress)
    {
        if (string.IsNullOrEmpty(challengeId)) return;
        PlayerPrefs.SetInt(ChallengeProgressPrefix + challengeId, Mathf.Max(0, progress));
    }

    private void DeleteChallengeProgress(string challengeId)
    {
        if (string.IsNullOrEmpty(challengeId)) return;
        PlayerPrefs.DeleteKey(ChallengeProgressPrefix + challengeId);
    }

    private string ResolveEntityName(ChallengeDTO challenge)
    {
        if (challenge == null) return ANY;

        if (challenge.taskType == TaskType.COLLECT_ITEM)
        {
            if (!string.IsNullOrEmpty(challenge.title) && challenge.title.IndexOf("ITEM", StringComparison.OrdinalIgnoreCase) >= 0)
                return ANY;

            if (!string.IsNullOrEmpty(challenge.title))
            {
                string titleUpper = challenge.title.Trim().ToUpperInvariant().Replace("CHESSE", "CHEESE");

                foreach (FoodType ft in Enum.GetValues(typeof(FoodType)))
                {
                    if (titleUpper.Contains(ft.ToString()))
                        return ft.ToString();
                }
            }

            if (challenge.entityType == MissionEntityType.FOOD && Enum.IsDefined(typeof(FoodType), challenge.entity))
                return ((FoodType)challenge.entity).ToString();

            return ANY;
        }

        if (challenge.taskType == TaskType.USE_POWERUP)
        {
            if (!string.IsNullOrEmpty(challenge.title))
            {
                string titleUpper = challenge.title.Trim().ToUpperInvariant();

                foreach (MissionPowerupType pt in Enum.GetValues(typeof(MissionPowerupType)))
                {
                    if (titleUpper.Contains(pt.ToString()))
                        return pt.ToString();
                }
            }

            if (challenge.entityType == MissionEntityType.POWERUP && Enum.IsDefined(typeof(MissionPowerupType), challenge.entity))
                return ((MissionPowerupType)challenge.entity).ToString();

            return ANY;
        }

        return ANY;
    }

    private void InferFromTitle(string title, out TaskType taskType, out string entityName, out int entityCode)
    {
        taskType = 0;
        entityName = ANY;
        entityCode = 0;

        if (string.IsNullOrEmpty(title)) return;

        string titleUpper = title.Trim().ToUpperInvariant().Replace("CHESSE", "CHEESE");

        if (titleUpper.StartsWith("COLLECT "))
        {
            taskType = TaskType.COLLECT_ITEM;

            if (titleUpper.Contains("ITEM"))
            {
                entityName = ANY;
                entityCode = 0;
                return;
            }

            foreach (FoodType ft in Enum.GetValues(typeof(FoodType)))
            {
                if (titleUpper.Contains(ft.ToString()))
                {
                    entityName = ft.ToString();
                    entityCode = (int)ft;
                    return;
                }
            }

            return;
        }

        if (titleUpper.StartsWith("USE "))
        {
            taskType = TaskType.USE_POWERUP;

            foreach (MissionPowerupType pt in Enum.GetValues(typeof(MissionPowerupType)))
            {
                if (titleUpper.Contains(pt.ToString()))
                {
                    entityName = pt.ToString();
                    entityCode = (int)pt;
                    return;
                }
            }

            return;
        }

        if (titleUpper.StartsWith("SCORE "))
        {
            taskType = TaskType.SCORE_POINTS;
            return;
        }

        if (titleUpper.StartsWith("COMPLETE "))
        {
            taskType = TaskType.COMPLETE_LEVEL;
            return;
        }

        if (titleUpper.Contains("WIN STREAK"))
        {
            taskType = TaskType.WIN_STREAK;
            return;
        }

        if (titleUpper.Contains("TRIGGER BEE"))
        {
            taskType = TaskType.TRIGGER_BEE;
            return;
        }
    }

    private string GetProgressKey(TaskType taskType, int entityCode)
    {
        return $"MissionProgress_{(int)taskType}_{entityCode}";
    }

    private string GetProgressKey(TaskType taskType, string entityName)
    {
        if (string.IsNullOrEmpty(entityName)) entityName = ANY;
        return $"MissionProgressN_{(int)taskType}_{entityName.ToUpperInvariant()}";
    }

    private bool TryParseFoodType(string value, out FoodType foodType)
    {
        try
        {
            foodType = (FoodType)Enum.Parse(typeof(FoodType), value, true);
            return true;
        }
        catch
        {
            foodType = default(FoodType);
            return false;
        }
    }
}
