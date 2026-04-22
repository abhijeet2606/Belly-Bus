using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Analytics;
using System.Threading.Tasks;

public static class AnalyticsEvents
{
    public const string FirstOpen = "first_open";
    public const string SessionStart = "session_start";
    public const string SessionEnd = "session_end";

    public const string ButtonClick = "button_click";
    public const string IapPurchase = "iap_purchase";

    // NEW: Puzzle solved event
    public const string PuzzleResult = "puzzle_result";
}

public class GameAnalyticsManager : MonoBehaviour
{
    public static GameAnalyticsManager Instance;  

    private static bool isInitialized = false;

    private async void Awake()
    {
        // Singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        await InitializeAnalyticsAsync();
    }

    // ---------------------------
    // INITIALIZATION
    // ---------------------------
    private async Task InitializeAnalyticsAsync()
    {
        if (isInitialized) return;

        try
        {
            await UnityServices.InitializeAsync();
            AnalyticsService.Instance.StartDataCollection();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log("[Analytics] Initialized");
#endif

            if (!PlayerPrefs.HasKey("first_open_tracked"))
            {
                TrackFirstOpen();
                PlayerPrefs.SetInt("first_open_tracked", 1);
            }

            TrackSessionStart();

            isInitialized = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Analytics] Init failed: " + e.Message);
        }
    }

    // ---------------------------
    // BASIC EVENTS
    // ---------------------------
    private void TrackFirstOpen()
    {
        var evt = new CustomEvent(AnalyticsEvents.FirstOpen);
        evt.Add("game_name", Application.productName);
        AnalyticsService.Instance.RecordEvent(evt);
    }

    private void TrackSessionStart()
    {
        var evt = new CustomEvent(AnalyticsEvents.SessionStart);
        evt.Add("session_id", System.Guid.NewGuid().ToString());
        AnalyticsService.Instance.RecordEvent(evt);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            var evt = new CustomEvent(AnalyticsEvents.SessionEnd);
            AnalyticsService.Instance.RecordEvent(evt);
        }
    }

    private void OnApplicationQuit()
    {
        try
        {
            AnalyticsService.Instance.Flush();
        }
        catch { }
    }

    // ---------------------------
    // BUTTON CLICKS
    // ---------------------------
    public void TrackButtonClick(string buttonName)
    {
        var evt = new CustomEvent(AnalyticsEvents.ButtonClick);
        evt.Add("button_name", buttonName);
        AnalyticsService.Instance.RecordEvent(evt);
    }

    // ---------------------------
    // IAP PURCHASE TRACKING
    // ---------------------------
    public void TrackIapResult(string productId, bool success, string reason = null)
    {
        var evt = new CustomEvent(AnalyticsEvents.IapPurchase);
        evt.Add("product_id", productId);
        evt.Add("result", success ? "success" : "failed");

        if (!string.IsNullOrEmpty(reason))
            evt.Add("reason", reason);

        AnalyticsService.Instance.RecordEvent(evt);
    }

    // ----------------------------------------------------
    // 🚀 NEW — VERY SIMPLE PUZZLE RESULT TRACKING
    // ----------------------------------------------------
    public void TrackPuzzleSolved(bool success)
    {
        var evt = new CustomEvent(AnalyticsEvents.PuzzleResult);
        evt.Add("success", success ? "true" : "false");
        AnalyticsService.Instance.RecordEvent(evt);

#if UNITY_EDITOR
        Debug.Log("[Analytics] Puzzle Result Sent: " + (success ? "Correct" : "Incorrect"));
#endif
    }
}
