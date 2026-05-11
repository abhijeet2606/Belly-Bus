using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;
using System.Text.RegularExpressions;
using System.Globalization;
using System.IO;

[DefaultExecutionOrder(-100)]
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

    [Header("Daily Empty State")]
    public GameObject DailyAllCompletedMessage;
    public Text DailyAllCompletedText;
    public string DailyAllCompletedMessageText = "All tasks have been completed. Come back tomorrow for more tasks.";

    private const string MissionsCacheKey = "MissionsDataCache";
    private const string LastFetchTimeKey = "MissionsLastFetchTime";
    private const string MissionsCacheFileName = "missions_cache.json";
    private const string MissionsCacheEditorRelativePath = "Resources/Missions/missions_cache.json";

    private MissionsApiResponse currentResponse;
    private string lastLoadedMissionsCache;
    private List<ChallengeDTO> dailyQueue = new List<ChallengeDTO>();
    private List<ChallengeDTO> weeklyQueue = new List<ChallengeDTO>();
    private List<ChallengeDTO> monthlyQueue = new List<ChallengeDTO>();
    private DayChallengesDTO[] dailyDays;
    private int selectedDailyDay = 1;
    private int unlockedDailyDays;
    private int maxDailyDays;
    private DateTime dailyStartUtcDate;
    private DateTime dailyEndUtcDate;

    public static event Action OnMissionsCacheUpdated;

    private void OnEnable()
    {
        if (MissionProgressManager.Instance != null)
        {
            MissionProgressManager.Instance.OnAnyProgressChanged += HandleAnyMissionProgressChanged;
        }
        OnMissionsCacheUpdated += HandleMissionsCacheUpdated;
    }

    private void OnDisable()
    {
        if (MissionProgressManager.Instance != null)
        {
            MissionProgressManager.Instance.OnAnyProgressChanged -= HandleAnyMissionProgressChanged;
        }
        OnMissionsCacheUpdated -= HandleMissionsCacheUpdated;
    }

    private void HandleMissionsCacheUpdated()
    {
        LoadMissionsFromCache();

        // after fresh data arrives we *must* select the correct day
        if (dailyDays != null && dailyDays.Length > 0)
            SelectDailyDay(GetDailyActiveDay(), true);
    }

    private void HandleAnyMissionProgressChanged()
    {
        if (MissionPanel == null || !MissionPanel.activeInHierarchy) return;
        RefreshAllPanels();
    }

    private bool isFetchingMissions = false;

    private void Awake()
    {
        // This MUST run in Awake() to ensure it happens before any other script's Start() can load stale data.
        if (Debug_ResetMissionsOnStart)
        {
            ForceResetAllMissionProgress();
            // After a forced reset, immediately trigger a background fetch for new data.
            Debug.Log("[Mission] Forced reset complete. Triggering immediate background fetch.");
            StartCoroutine(FetchAndRefreshMissions());
        }
    }

    private void Start()
    {
        var _ = MissionProgressManager.Instance;
        
        // 1. First, find and configure the roadmap icons so they are ready to be updated.
        ConfigureRoadmapButtons();

        // 2. Load the mission data from the cache.
        LoadMissionsFromCache();
        
        // 3. Select the active day. This will now correctly update the roadmap icons
        //    because ConfigureRoadmapButtons has already populated the ProgressIcons list.
        if (dailyDays != null && dailyDays.Length > 0)
            SelectDailyDay(GetDailyActiveDay(), true);
        else
            RefreshAllPanels();
        
        ShowDailyTab();
    }

    private bool IsRefreshNeeded()
    {
        if (dailyEndUtcDate == default(DateTime)) return true;
        return DateTime.UtcNow >= dailyEndUtcDate;
    }

    private void LoadMissionsFromCache()
    {
        string cachedJson = ReadCachedMissionsJson();
        Debug.Log($"[Mission] LoadMissionsFromCache: hasCache={!string.IsNullOrEmpty(cachedJson)} length={(cachedJson != null ? cachedJson.Length : 0)}");
        if (string.IsNullOrEmpty(cachedJson)) return;
        TryProcessMissionsJsonSafe(SanitizeMissionsJson(cachedJson));
    }

    private void TryProcessMissionsJsonSafe(string json)
    {
        try
        {
            ProcessMissionsJson(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Mission] Missions JSON parse failed. err={ex.Message}");
        }
    }

    [Header("Debug")]
    public bool Debug_ResetMissionsOnStart = true; // This will be auto-disabled after one run.

    private void ProcessMissionsJson(string json)
    {
        lastLoadedMissionsCache = json;
        MissionsApiResponse parsed = null;
        try
        {
            parsed = JsonUtility.FromJson<MissionsApiResponse>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Mission] Invalid missions JSON. len={(json != null ? json.Length : 0)} err={ex.Message}");
            return;
        }

        if (parsed == null || !parsed.success)
        {
            string head = json != null ? json.Substring(0, Mathf.Min(200, json.Length)) : "";
            Debug.LogWarning($"[Mission] Missions JSON not parsed or success=false. len={(json != null ? json.Length : 0)} head='{head}'");
            return;
        }

        currentResponse = parsed;

        // --- NEW LOGIC: Handle Flat List from player/challenges/ ---
        if (currentResponse.data == null && currentResponse.challenges != null && currentResponse.challenges.Length > 0)
        {
            Debug.Log($"[Mission] Processing flat list of {currentResponse.challenges.Length} challenges.");
            
            currentResponse.data = new MissionsData();
            var allChallenges = currentResponse.challenges.ToList();
            
            // Reconstruct daily days (distribute challenges across 7 days)
            var days = new List<DayChallengesDTO>();
            int challengesPerDay = Mathf.Max(1, Mathf.CeilToInt(allChallenges.Count / 7.0f));
            
            for (int d = 1; d <= 7; d++)
            {
                var dayChallenges = allChallenges.Skip((d - 1) * challengesPerDay).Take(challengesPerDay).ToArray();
                if (dayChallenges.Length > 0)
                {
                    days.Add(new DayChallengesDTO { day = d, challenges = dayChallenges });
                }
            }

            currentResponse.data.daily = new MissionPeriodDTO
            {
                days = days.ToArray()
            };

            // Weekly and Monthly from the same pool or separate?
            // For now, keep them populated so UI isn't empty
            currentResponse.data.weekly = new MissionPeriodDTO
            {
                days = new DayChallengesDTO[] {
                    new DayChallengesDTO { day = 1, challenges = allChallenges.Take(6).ToArray() }
                }
            };
            currentResponse.data.monthly = new MissionPeriodDTO
            {
                days = new DayChallengesDTO[] {
                    new DayChallengesDTO { day = 1, challenges = allChallenges.Take(6).ToArray() }
                }
            };
        }

        if (Debug_ResetMissionsOnStart)
        {
            ForceResetAllMissionProgress();
        }


        // --- EXISTING LOGIC: Process the (possibly reconstructed) data ---
        if (currentResponse.data != null && currentResponse.data.daily != null && currentResponse.data.daily.days != null && currentResponse.data.daily.days.Length > 0)
        {
            dailyEndUtcDate = ParseUtcDateOrDefault(currentResponse.data.daily.endDate).AddDays(1);
            dailyDays = EnsureDailyDays(dailyDays: currentResponse.data.daily.days, desiredCount: 7);
            maxDailyDays = 7;
            RecomputeDailyRoadmap();
            SelectDailyDay(GetDailyActiveDay(), true);
        }

        // Process Weekly
        if (currentResponse.data != null && currentResponse.data.weekly != null && currentResponse.data.weekly.days != null && currentResponse.data.weekly.days.Length > 0)
        {
            weeklyQueue = currentResponse.data.weekly.days[0].challenges != null
                ? currentResponse.data.weekly.days[0].challenges.OrderBy(c => c.slot).ThenBy(c => (int)c.difficulty).ToList()
                : new List<ChallengeDTO>();
        }

        // Process Monthly
        if (currentResponse.data != null && currentResponse.data.monthly != null && currentResponse.data.monthly.days != null && currentResponse.data.monthly.days.Length > 0)
        {
            monthlyQueue = currentResponse.data.monthly.days[0].challenges != null
                ? currentResponse.data.monthly.days[0].challenges.OrderBy(c => c.slot).ThenBy(c => (int)c.difficulty).ToList()
                : new List<ChallengeDTO>();
        }

        NormalizeQueue(weeklyQueue);
        NormalizeQueue(monthlyQueue);

#if UNITY_EDITOR
        if (Debug_ResetMissionsOnStart)
        {
            ForceResetAllMissionProgress();
        }
#else
        Debug_ResetMissionsOnStart = false;
#endif

        RefreshAllPanels();
    }

    private DayChallengesDTO[] EnsureDailyDays(DayChallengesDTO[] dailyDays, int desiredCount)
    {
        desiredCount = Mathf.Max(1, desiredCount);
        var result = new DayChallengesDTO[desiredCount];

        if (dailyDays != null)
        {
            for (int i = 0; i < dailyDays.Length; i++)
            {
                var d = dailyDays[i];
                if (d == null) continue;
                int dayIndex = d.day - 1;
                if (dayIndex < 0 || dayIndex >= desiredCount) continue;
                result[dayIndex] = d;
            }
        }

        for (int day = 1; day <= desiredCount; day++)
        {
            int idx = day - 1;
            if (result[idx] == null)
            {
                result[idx] = new DayChallengesDTO
                {
                    day = day,
                    challenges = Array.Empty<ChallengeDTO>()
                };
            }
            else if (result[idx].challenges == null)
            {
                result[idx].challenges = Array.Empty<ChallengeDTO>();
            }
        }

        return result;
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        if (string.IsNullOrEmpty(baseUrl)) return path ?? "";
        if (string.IsNullOrEmpty(path)) return baseUrl;

        string b = baseUrl.TrimEnd('/');
        string p = path.TrimStart('/');
        return b + "/" + p;
    }

    public static string SanitizeMissionsJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;
        return Regex.Replace(json, "\"entity\"\\s*:\\s*null", "\"entity\":0");
    }

    public static bool IsWeeklyRefreshNeededStatic()
    {
        string cachedJson = ReadCachedMissionsJsonStatic();
        if (string.IsNullOrEmpty(cachedJson)) return true;

        var sanitized = SanitizeMissionsJson(cachedJson);
        MissionsApiResponse parsed;
        try
        {
            parsed = JsonUtility.FromJson<MissionsApiResponse>(sanitized);
        }
        catch
        {
            return true;
        }

        if (parsed == null || !parsed.success) return true;

        // If we have grouped data, check expiration
        if (parsed.data != null && parsed.data.daily != null)
        {
            DateTime endUtc = ParseUtcDateOrDefault(parsed.data.daily.endDate);
            if (endUtc == default(DateTime)) return true;
            return DateTime.UtcNow.Date > endUtc.Date;
        }

        // If we have flat list data, check if we have any challenges at all
        if (parsed.challenges != null && parsed.challenges.Length > 0)
        {
            // For flat list, we can check a "LastFetchTime" to see if it's been more than 24 hours
            string lastFetchStr = PlayerPrefs.GetString(LastFetchTimeKey, "");
            if (DateTime.TryParse(lastFetchStr, out DateTime lastFetch))
            {
                return (DateTime.UtcNow - lastFetch).TotalHours > 24;
            }
            return false; // We have data and it's not explicitly expired
        }

        return true;
    }

    public static IEnumerator BackgroundFetchAndCacheMissions()
    {
        string accessToken = PlayerPrefs.GetString("AccessToken", "");
        string baseUrl = null;
        if (ProgressDataManager.Instance != null) baseUrl = ProgressDataManager.Instance.ApiBaseUrl;
        else baseUrl = ProgressDataManager.EnsureInstance().ApiBaseUrl;

        Debug.Log($"[Mission] BackgroundFetchAndCacheMissions: baseUrl={baseUrl} hasToken={!string.IsNullOrEmpty(accessToken)}");

        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(accessToken))
            yield break;

        // Use the new endpoint for challenges
        string url = CombineUrl(baseUrl, "player/challenges/");
        Debug.Log($"[Mission] Fetching missions from: {url}");
        using (var req = UnityWebRequest.Get(url))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + accessToken);
            req.timeout = 15;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                string body = req.downloadHandler != null ? req.downloadHandler.text : "";
                string head = body != null ? body.Substring(0, Mathf.Min(200, body.Length)) : "";
                Debug.LogWarning($"[Mission] Missions fetch failed. Url={url} Code={(int)req.responseCode} Error={req.error} Head='{head}'");
                yield break;
            }

            string jsonResponse = req.downloadHandler != null ? req.downloadHandler.text : null;
            if (string.IsNullOrEmpty(jsonResponse))
            {
                Debug.LogWarning($"[Mission] Missions fetch returned empty body. Url={url} Code={(int)req.responseCode}");
                yield break;
            }

            // --- Log the raw server response for debugging ---
            Debug.Log($"[Mission] RAW SERVER RESPONSE: {jsonResponse}");

            Debug.Log($"[Mission] Fetch success. bodyLength={jsonResponse.Length}");

            // If the response is a raw array, wrap it so JsonUtility can parse it
            if (jsonResponse.TrimStart().StartsWith("["))
            {
                jsonResponse = "{\"success\":true,\"challenges\":" + jsonResponse + "}";
            }

            string sanitized = SanitizeMissionsJson(jsonResponse);

            MissionsApiResponse parsed;
            try
            {
                parsed = JsonUtility.FromJson<MissionsApiResponse>(sanitized);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Mission] Missions fetch returned invalid JSON. err={ex.Message}");
                yield break;
            }
            
            // Validate that we got some data (either grouped or flat list)
            bool hasGroupedData = (parsed != null && parsed.success && parsed.data != null && parsed.data.daily != null);
            bool hasFlatListData = (parsed != null && parsed.success && parsed.challenges != null && parsed.challenges.Length > 0);
            
            Debug.Log($"[Mission] Parsed data: success={parsed?.success} hasGrouped={hasGroupedData} hasFlat={hasFlatListData}");

            if (!hasGroupedData && !hasFlatListData)
                yield break;

            WriteCachedMissionsJsonStatic(sanitized);
            PlayerPrefs.SetString(LastFetchTimeKey, DateTime.UtcNow.ToString("O"));
            PlayerPrefs.Save();
            Debug.Log("[Mission] Cache updated successfully.");
            OnMissionsCacheUpdated?.Invoke();
        }
    }

    private string ReadCachedMissionsJson()
    {
        return ReadCachedMissionsJsonStatic();
    }

    public static string ReadCachedMissionsJsonStatic()
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, MissionsCacheFileName);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }
        catch { }

        return PlayerPrefs.GetString(MissionsCacheKey, "");
    }

    private static void WriteCachedMissionsJsonStatic(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            string path = Path.Combine(Application.persistentDataPath, MissionsCacheFileName);
            File.WriteAllText(path, json);
        }
        catch { }

        PlayerPrefs.SetString(MissionsCacheKey, json);

#if UNITY_EDITOR
        try
        {
            string editorPath = Path.Combine(Application.dataPath, MissionsCacheEditorRelativePath);
            string dir = Path.GetDirectoryName(editorPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(editorPath, json);
        }
        catch { }
#endif
    }

    private void ConfigureRoadmapButtons()
    {
        if (ProgressIcons == null || ProgressIcons.Count == 0)
        {
            ProgressIcons = GetComponentsInChildren<ProgressIconUI>(true).OrderBy(x => x.transform.GetSiblingIndex()).Take(7).ToList();
        }

        for (int i = 0; i < ProgressIcons.Count; i++)
        {
            int day = i + 1;
            var icon = ProgressIcons[i];
            if (icon != null) icon.SetDayNumber(day);
            var btn = icon != null ? icon.GetComponent<Button>() : null;
            if (btn == null) continue;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => SelectDailyDay(day, false));
        }
    }

    private int GetTodayUnlockedDay()
    {
        // Ignore startDate from backend and always use the current Day of the Week.
        // This ensures Monday is always Day 1, Thursday is always Day 4, etc.
        int dow = (int)DateTime.UtcNow.DayOfWeek;
        if (dow == 0) dow = 7; // Sunday is 0, map it to 7.
        return dow;
    }

    private bool IsDailyDayComplete(int dayNumber)
    {
        if (dailyDays == null) return false;
        var dayDto = dailyDays.FirstOrDefault(d => d != null && d.day == dayNumber);
        if (dayDto == null || dayDto.challenges == null || dayDto.challenges.Length == 0) return false;
        for (int i = 0; i < dayDto.challenges.Length; i++)
        {
            var ch = dayDto.challenges[i];
            if (ch == null) return false;
            if (!IsMissionComplete(ch)) return false;
        }
        return true;
    }

    private void RecomputeDailyRoadmap()
    {
        if (dailyDays == null || dailyDays.Length == 0)
        {
            unlockedDailyDays = 1;
            maxDailyDays = 0;
            return;
        }

        maxDailyDays = Mathf.Min(7, dailyDays.Length);
        unlockedDailyDays = GetTodayUnlockedDay();

        int activeDay = GetDailyActiveDay();
        selectedDailyDay = Mathf.Clamp(selectedDailyDay, 1, unlockedDailyDays);

        if (IsDailyDayComplete(selectedDailyDay))
        {
            selectedDailyDay = activeDay;
        }

        if (!HasDailyChallenges(selectedDailyDay))
        {
            selectedDailyDay = FindFallbackDailyDay(Mathf.Min(activeDay, unlockedDailyDays));
        }

        PlayerPrefs.SetInt("DailySelectedDay", selectedDailyDay);
        PlayerPrefs.Save();

        var dayDto = dailyDays.FirstOrDefault(d => d != null && d.day == selectedDailyDay);
        if (dayDto == null || dayDto.challenges == null) dailyQueue = new List<ChallengeDTO>();
        else dailyQueue = dayDto.challenges.OrderBy(c => c.slot).ThenBy(c => (int)c.difficulty).ToList();
        NormalizeQueue(dailyQueue);

        UpdateDailyRoadmapUI();

#if UNITY_EDITOR
        Debug.Log($"[Mission] Roadmap recompute: unlockedDailyDays={unlockedDailyDays} selectedDailyDay={selectedDailyDay} maxDailyDays={maxDailyDays}");
#endif
    }

    private bool HasDailyChallenges(int dayNumber)
    {
        if (dailyDays == null) return false;
        var dayDto = dailyDays.FirstOrDefault(d => d != null && d.day == dayNumber);
        return dayDto != null && dayDto.challenges != null && dayDto.challenges.Length > 0;
    }

    private int FindFallbackDailyDay(int preferredDay)
    {
        int upper = Mathf.Clamp(preferredDay, 1, Mathf.Max(1, Mathf.Min(maxDailyDays, unlockedDailyDays)));
        for (int day = upper; day >= 1; day--)
        {
            if (HasDailyChallenges(day)) return day;
        }
        return 1;
    }

    private int GetDailyActiveDay()
    {
        if (maxDailyDays <= 0) return 1;
        
        for (int day = 1; day <= unlockedDailyDays; day++)
        {
            if (!IsDailyDayComplete(day))
            {
                return day;
            }
        }
        
        return Mathf.Clamp(unlockedDailyDays, 1, maxDailyDays);
    }

    private void UpdateDailyRoadmapUI()
    {
        if (ProgressIcons == null) return;
        int activeDay = GetDailyActiveDay();

        for (int i = 0; i < ProgressIcons.Count; i++)
        {
            int day = i + 1;
            bool isCompleted = IsDailyDayComplete(day);
            bool isUnlocked = day <= unlockedDailyDays;
            
            // A button is only interactable if it corresponds to the CURRENT active day.
            bool isSelectable = isUnlocked && !isCompleted && day == activeDay;
            
            // The icon shows its "active" state if it's the one currently selected by the UI.
            bool isActive = day == selectedDailyDay;

            var icon = ProgressIcons[i];
            if (icon != null)
            {
                // The icon's visual state is determined by completion, selection, and unlock status.
                // An active icon should not also show as completed.
                icon.SetState(isCompleted, isActive && !isCompleted, isUnlocked);
            }

            var btn = icon != null ? icon.GetComponent<Button>() : null;
            if (btn != null)
            {
                btn.interactable = isSelectable;
            }
        }
    }

    private void SelectDailyDay(int day, bool force)
    {
        if (dailyDays == null || dailyDays.Length == 0)
        {
            // If there are no daily missions, ensure the UI is cleared.
            dailyQueue.Clear();
            RefreshAllPanels();
            return;
        }

        if (!force)
        {
            // Strict check: only allow selecting the current active day.
            if (day != GetDailyActiveDay())
            {
                Debug.Log($"[Mission] Rejected click on day {day} because active day is {GetDailyActiveDay()}.");
                return;
            }
        }

        selectedDailyDay = Mathf.Clamp(day, 1, unlockedDailyDays);
        PlayerPrefs.SetInt("DailySelectedDay", selectedDailyDay);
        PlayerPrefs.Save();

        // Recompute roadmap and refresh all UI panels to reflect the new selection.
        RecomputeDailyRoadmap();
        RefreshAllPanels();
    }

    private void NormalizeQueue(List<ChallengeDTO> queue)
    {
        if (queue == null) return;
        foreach (var challenge in queue)
        {
            NormalizeChallenge(challenge);
        }
    }

    private void NormalizeChallenge(ChallengeDTO challenge)
    {
        if (challenge == null) return;
        if (!string.IsNullOrEmpty(challenge.title))
        {
            string titleUpper = challenge.title.Trim().ToUpperInvariant();
            titleUpper = titleUpper.Replace("CHESSE", "CHEESE");

            bool missingTaskType = (int)challenge.taskType == 0;
            bool missingEntityType = (int)challenge.entityType == 0;
            bool missingEntity = challenge.entity == 0;

            if (!missingTaskType && !missingEntityType && !missingEntity) return;

            if (titleUpper.StartsWith("COLLECT "))
            {
                challenge.taskType = TaskType.COLLECT_ITEM;

                if (titleUpper.Contains("ITEM") || titleUpper.Contains("ITEMS"))
                {
                    challenge.entityType = MissionEntityType.NONE;
                    challenge.entity = 0;
                    return;
                }

                foreach (FoodType ft in Enum.GetValues(typeof(FoodType)))
                {
                    if (titleUpper.Contains(ft.ToString()))
                    {
                        challenge.entityType = MissionEntityType.FOOD;
                        challenge.entity = (int)ft;
                        return;
                    }
                }

                challenge.entityType = MissionEntityType.NONE;
                challenge.entity = 0;
                return;
            }

            if (titleUpper.StartsWith("USE "))
            {
                challenge.taskType = TaskType.USE_POWERUP;
                challenge.entityType = MissionEntityType.POWERUP;

                if (titleUpper.Contains("OVEN"))
                {
                    challenge.entity = (int)MissionPowerupType.OVEN;
                    return;
                }
                if (titleUpper.Contains("PAN"))
                {
                    challenge.entity = (int)MissionPowerupType.PAN;
                    return;
                }
                if (titleUpper.Contains("KNIFE"))
                {
                    challenge.entity = (int)MissionPowerupType.KNIFE;
                    return;
                }
                if (titleUpper.Contains("FLIES") || titleUpper.Contains("FLY"))
                {
                    challenge.entity = (int)MissionPowerupType.FLIES;
                    return;
                }
                if (titleUpper.Contains("HAMMER"))
                {
                    challenge.entity = (int)MissionPowerupType.HAMMER;
                    return;
                }
                if (titleUpper.Contains("BLENDER"))
                {
                    challenge.entity = (int)MissionPowerupType.BLENDER;
                    return;
                }

                challenge.entityType = MissionEntityType.NONE;
                challenge.entity = 0;
                return;
            }

            if (titleUpper.StartsWith("SCORE "))
            {
                challenge.taskType = TaskType.SCORE_POINTS;
                challenge.entityType = MissionEntityType.NONE;
                challenge.entity = 0;
                return;
            }

            if (titleUpper.StartsWith("COMPLETE "))
            {
                challenge.taskType = TaskType.COMPLETE_LEVEL;
                challenge.entityType = MissionEntityType.NONE;
                challenge.entity = 0;
                return;
            }

            if (titleUpper.Contains("WIN STREAK"))
            {
                challenge.taskType = TaskType.WIN_STREAK;
                challenge.entityType = MissionEntityType.NONE;
                challenge.entity = 0;
                return;
            }

            if (titleUpper.Contains("TRIGGER BEE"))
            {
                challenge.taskType = TaskType.TRIGGER_BEE;
                challenge.entityType = MissionEntityType.NONE;
                challenge.entity = 0;
                return;
            }
        }
    }

    public void ForceResetAllMissionProgress()
    {
        Debug.LogWarning("--- DEBUG: Forcing a full reset of all mission caches and progress! ---");

        // 1. Clear all known mission-related PlayerPrefs keys
        PlayerPrefs.DeleteKey("MissionsDataCache");
        PlayerPrefs.DeleteKey("MissionsLastFetchTime");
        PlayerPrefs.DeleteKey("DailySelectedDay");

        // 2. Delete the cache file from the persistent data path to be absolutely sure.
        try
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, "missions_cache.json");
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
                Debug.Log($"[Mission] Deleted cache file at: {path}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Mission] Failed to delete cache file: {ex.Message}");
        }

        // 3. Reset the in-memory progress manager to clear any active mission state.
        if (MissionProgressManager.Instance != null)
        {
            MissionProgressManager.Instance.ResetState();
        }

        PlayerPrefs.Save();
        Debug.Log("[Mission] Finished resetting mission PlayerPrefs and cache file.");

        // Auto-disable after running to prevent resetting every time.
        Debug_ResetMissionsOnStart = false;
    }

    private void RefreshAllPanels()
    {
        RecomputeDailyRoadmap();

        var dailyActive = GetVisibleMissions(dailyQueue);
        var weeklyActive = GetVisibleMissions(weeklyQueue);
        var monthlyActive = GetVisibleMissions(monthlyQueue);

        if (MissionProgressManager.Instance != null)
        {
            if (dailyActive.Count > 0 || weeklyActive.Count > 0 || monthlyActive.Count > 0)
            {
                MissionProgressManager.Instance.SetActiveMissions(dailyActive, weeklyActive, monthlyActive);
            }
        }

        // Determine if we should show the "All Tasks Completed" message for the currently active tab
        bool isDailyTab = DailyPanel != null && DailyPanel.activeInHierarchy;
        bool isWeeklyTab = WeeklyPanel != null && WeeklyPanel.activeInHierarchy;
        bool isMonthlyTab = MonthlyPanel != null && MonthlyPanel.activeInHierarchy;

        bool showDailyDone = isDailyTab && ShouldShowDailyAllCompletedMessage();
        bool showWeeklyDone = isWeeklyTab && (weeklyQueue == null || weeklyQueue.Count == 0 || weeklyQueue.All(c => IsMissionComplete(c)));
        bool showMonthlyDone = isMonthlyTab && (monthlyQueue == null || monthlyQueue.Count == 0 || monthlyQueue.All(c => IsMissionComplete(c)));

        bool showMessage = showDailyDone || showWeeklyDone || showMonthlyDone;
        SetDailyAllCompletedMessage(showMessage);

        if (showMessage)
        {
            if (DailyAllCompletedText != null)
            {
                if (showDailyDone) DailyAllCompletedText.text = DailyAllCompletedMessageText;
                else if (showWeeklyDone) DailyAllCompletedText.text = "All weekly tasks have been completed. Come back next week for more tasks.";
                else if (showMonthlyDone) DailyAllCompletedText.text = "All monthly tasks have been completed. Come back next month for more tasks.";
            }

            bool hasMessageUI = DailyAllCompletedMessage != null || DailyAllCompletedText != null;
            if (hasMessageUI)
            {
                if (showDailyDone) HideAllChildren(DailyTaskContainer);
                else if (showWeeklyDone) HideAllChildren(WeeklyTaskContainer);
                else if (showMonthlyDone) HideAllChildren(MonthlyTaskContainer);
            }
        }

        RefreshPanel(DailyTaskContainer, dailyQueue, !showDailyDone);
        RefreshPanel(WeeklyTaskContainer, weeklyQueue, !showWeeklyDone);
        RefreshPanel(MonthlyTaskContainer, monthlyQueue, !showMonthlyDone);
    }

    private List<ChallengeDTO> GetVisibleMissions(List<ChallengeDTO> queue)
    {
        if (queue == null) return new List<ChallengeDTO>();
        return queue.Where(c => !IsMissionComplete(c)).Take(3).ToList();
    }

    private void RefreshPanel(Transform container, List<ChallengeDTO> queue, bool includeClaimedFallback)
    {
        if (container == null) return;

        int totalInQueue = queue != null ? queue.Count : 0;
        int slotCount = Mathf.Min(3, totalInQueue);
        
        Debug.Log($"[Mission] RefreshPanel for {container.name}: queueCount={totalInQueue} slotCount={slotCount} includeFallback={includeClaimedFallback}");

        if (slotCount <= 0)
        {
            for (int i = container.childCount - 1; i >= 0; i--) Destroy(container.GetChild(i).gameObject);
            return;
        }

        var active = queue.Where(c => !IsMissionComplete(c)).ToList();
        var claimed = queue.Where(c => IsMissionComplete(c)).ToList();

        var visibleMissions = new List<ChallengeDTO>();
        visibleMissions.AddRange(active.Take(slotCount));

        if (includeClaimedFallback && visibleMissions.Count < slotCount && claimed.Count > 0)
        {
            int need = slotCount - visibleMissions.Count;
            var tailClaimed = claimed.Skip(Mathf.Max(0, claimed.Count - need)).Take(need);
            visibleMissions.AddRange(tailClaimed);
        }

        // Ensure we have enough children in the container
        int currentChildCount = container.childCount;
        int neededCount = slotCount;

        // Destroy excess children if any
        for (int i = currentChildCount - 1; i >= neededCount; i--)
        {
            Destroy(container.GetChild(i).gameObject);
        }

        // Update or Create children
        for (int i = 0; i < neededCount; i++)
        {
            var challenge = i < visibleMissions.Count ? visibleMissions[i] : null;
            GameObject taskGO;
            
            if (i < currentChildCount)
            {
                taskGO = container.GetChild(i).gameObject;
            }
            else
            {
                taskGO = Instantiate(TaskPrefab, container);
            }

            taskGO.SetActive(challenge != null);
            if (challenge == null) continue;
            MissionItemUI missionItem = taskGO.GetComponent<MissionItemUI>();

            int playerProgress = 0;
            if (MissionProgressManager.Instance != null)
            {
                playerProgress = MissionProgressManager.Instance.GetMissionProgress(challenge);
            }

            Sprite entityIcon = GetEntityIcon(challenge.entityType, challenge.entity);
            bool claimedMission = IsMissionComplete(challenge);
            int displayProgress = claimedMission ? challenge.target : playerProgress;
            missionItem.SetData(challenge.title, displayProgress, challenge.target, challenge.rewardCoins, claimedMission, entityIcon);
            
            // Link the claim requested delegate to our manager logic
            // Use a local variable to capture the specific challenge for this UI item
            var currentChallenge = challenge; 
            missionItem.OnClaimRequested = claimedMission ? null : () => OnMissionClaimed(currentChallenge, container, queue);
        }
    }

    private bool ShouldShowDailyAllCompletedMessage()
    {
        if (dailyQueue == null || dailyQueue.Count == 0) return false;
        if (dailyQueue.Any(c => !IsMissionComplete(c))) return false;
        
        // Show if all unlocked days are complete
        for (int day = 1; day <= unlockedDailyDays; day++)
        {
            if (!IsDailyDayComplete(day)) return false;
        }
        
        return true;
    }

    private void SetDailyAllCompletedMessage(bool show)
    {
        if (DailyAllCompletedMessage != null) DailyAllCompletedMessage.SetActive(show);
    }

    private void HideAllChildren(Transform container)
    {
        if (container == null) return;
        for (int i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            if (child != null) child.gameObject.SetActive(false);
        }
    }

    private static DateTime ParseUtcDateOrDefault(string dateString)
    {
        if (string.IsNullOrEmpty(dateString)) return default(DateTime);

        DateTime dt;
        if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
            return dt.Date;

        return default(DateTime);
    }

    private void OnMissionClaimed(ChallengeDTO challenge, Transform container, List<ChallengeDTO> queue)
    {
        if (MissionProgressManager.Instance == null) return;

        int currentProgress = MissionProgressManager.Instance.GetMissionProgress(challenge);
        Debug.Log($"[Mission] OnMissionClaimed: challenge='{challenge.title}', progress={currentProgress}, target={challenge.target}");

        if (currentProgress < challenge.target)
        {
            Debug.Log($"[Mission] Cannot claim mission '{challenge.title}': Progress {currentProgress}/{challenge.target}");
            return;
        }

        Debug.Log($"[Mission] Marking mission '{challenge.challengeId}' as complete and adding reward {challenge.rewardCoins}");
        MarkMissionAsComplete(challenge.challengeId);
        
        ProgressDataManager.EnsureInstance().AddCoins(challenge.rewardCoins);

        // When a mission is claimed, we should reset its progress in the manager 
        // so it doesn't stay at 'completed' for the next time it appears.
        MissionProgressManager.Instance.ResetProgressForChallenge(challenge);

        // Check if the current day is now complete. If so, auto-advance to the next day.
        if (IsDailyDayComplete(selectedDailyDay))
        {
            Debug.Log($"[Mission] Day {selectedDailyDay} is now complete. Auto-advancing to the next day.");
            SelectDailyDay(GetDailyActiveDay(), true);
        }
        else
        {
            // If the day is not yet complete, just refresh the current view.
            RefreshAllPanels();
        }
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
        if (challenge == null || string.IsNullOrEmpty(challenge.challengeId)) return false;
        
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

    public void UpdateRoadmap(int completedDays) { }

    public void OpenMissionPanel()
    {
        if (MissionPanel != null) MissionPanel.SetActive(true);
        ShowDailyTab();

        // Always load from cache first. This populates currentResponse if cache exists.
        LoadMissionsFromCache();

        // If the cache was empty or is expired, trigger a background fetch.
        // The OnMissionsCacheUpdated event will handle the refresh when data arrives.
        if (currentResponse == null || IsRefreshNeeded())
        {
            StartCoroutine(FetchAndRefreshMissions());
        }

        // Only select if we already have daily data.
        // If not, HandleMissionsCacheUpdated will take care of it when data arrives.
        if (dailyDays != null && dailyDays.Length > 0)
        {
            SelectDailyDay(GetDailyActiveDay(), true);
        }
    }

    private IEnumerator FetchAndRefreshMissions()
    {
        if (isFetchingMissions)
        {
            Debug.LogWarning("[Mission] FetchAndRefreshMissions called while a fetch was already in progress. Ignoring.");
            yield break;
        }

        isFetchingMissions = true;
        try
        {
            yield return BackgroundFetchAndCacheMissions();
        }
        finally
        {
            isFetchingMissions = false;
        }
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

        RefreshAllPanels(); // Ensure the completion message is correctly toggled for this tab
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

        RefreshAllPanels(); // Ensure the completion message is correctly toggled for this tab
    }

    public void ShowMonthlyTab()
    {
        SetPanelActive(DailyPanel, false);
        SetPanelActive(WeeklyPanel, false);
        SetPanelActive(MonthlyPanel, true);
        SetPanelActive(RoadmapHeader, false); 

        UpdateTabVisuals(DailyTabButton, false);
        UpdateTabVisuals(WeeklyTabButton, false);
        UpdateTabVisuals(MonthlyTabButton, true);

        RefreshAllPanels(); // Ensure the completion message is correctly toggled for this tab
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
