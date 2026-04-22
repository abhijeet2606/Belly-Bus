using UnityEngine;
using Unity.Services.Core;
using Unity.Services.PushNotifications;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class UGSManager : MonoBehaviour
{
    private bool isSubscribed = false;

    async void Start()
    {
        try
        {
            Debug.Log("[UGS] Initializing Unity Gaming Services...");
            await UnityServices.InitializeAsync();

            SetupAndroidNotificationChannel();

            // ✅ Subscribe once only, before registration
            if (!isSubscribed)
            {
                PushNotificationsService.Instance.OnRemoteNotificationReceived += OnNotificationReceived;
                isSubscribed = true;
            }

            await RegisterForPushNotificationsAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[UGS] Initialization failed: {e.Message}");
        }
    }

    private void SetupAndroidNotificationChannel()
    {
#if UNITY_ANDROID
        var channel = new AndroidNotificationChannel
        {
            Id = "default_channel",
            Name = "Default Channel",
            Description = "Generic notifications",
            Importance = Importance.Default
        };
        AndroidNotificationCenter.RegisterNotificationChannel(channel);
#endif
    }

    private async Task RegisterForPushNotificationsAsync()
    {
        try
        {
            Debug.Log("[PUSH] Registering device for push notifications...");
            string token = await PushNotificationsService.Instance.RegisterForPushNotificationsAsync();
            Debug.Log($"[PUSH] Registration complete ✅ Token: {token}");
            if (!string.IsNullOrEmpty(token))
            {
                PlayerPrefs.SetString("PUSH_DEVICE_TOKEN", token);
                PlayerPrefs.Save();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PUSH] Registration failed ❌ {e.Message}");
        }
    }

    private void OnNotificationReceived(Dictionary<string, object> data)
    {
        Debug.Log("[PUSH] Notification received!");

        if (data.TryGetValue("title", out var title))
            Debug.Log($"[PUSH] Title: {title}");

        if (data.TryGetValue("body", out var body))
            Debug.Log($"[PUSH] Body: {body}");

        if (data.TryGetValue("data", out var payload) && payload is Dictionary<string, object> extra)
        {
            if (extra.TryGetValue("deeplink", out var deeplink))
                Debug.Log($"[PUSH] Deep Link: {deeplink}");
        }
    }

    public static string GetLastPushDeviceToken()
    {
        return PlayerPrefs.GetString("PUSH_DEVICE_TOKEN", string.Empty);
    }
}
