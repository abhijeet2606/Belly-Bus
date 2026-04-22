using UnityEngine;
using System;
using System.Collections;
using GoogleMobileAds.Api;

public class GoogleAdsManager : MonoBehaviour
{
    public static GoogleAdsManager Instance { get; private set; }

    [Header("General")]
    public bool useAdMob = true;              // leave true for AdMob mediation
    public bool useUnityFallbackBanner = true;// optional: show Unity banner if AdMob banner repeatedly fails
    public bool useTestAds = true;
    public bool useAdaptiveBanner = true;

    [Header("Android AdMob (keep as-is)")]
    [SerializeField] private string androidAppId = "ca-app-pub-4650825417220512~6192967000";
    [SerializeField] private string androidBannerId = "ca-app-pub-4650825417220512/2995958028";
    [SerializeField] private string androidInterstitialId = "ca-app-pub-4650825417220512/4440901632";
    [SerializeField] private string androidRewardedId = "ca-app-pub-4650825417220512/9860749278";
    [SerializeField] private string androidLongRewardedId = "ca-app-pub-4650825417220512/2173830948";

    [Header("iOS AdMob (fill these later)")]
    [SerializeField] private string iosAppId = "";
    [SerializeField] private string iosBannerId = "";
    [SerializeField] private string iosInterstitialId = "";
    [SerializeField] private string iosRewardedId = "";
    [SerializeField] private string iosLongRewardedId = "";

    [Header("Behaviour")]
    [SerializeField] private float interstitialCooldown = 30f;
    [SerializeField] private int maxBannerRetriesBeforeUnityFallback = 3;

    // internals
    private BannerView bannerView;
    private InterstitialAd interstitialAd;
    private RewardedAd rewardedAd;

    private bool isBannerVisible = false;
    private bool isInterstitialLoaded = false;
    private bool isRewardedLoaded = false;
    private bool interstitialLoading = false;
    private bool rewardedLoading = false;

    private float lastInterstitialTime = -9999f;
    private int bannerRetryCount = 0;

    private Action<bool> onRewardedCompleteCallback;

    // resolved IDs
    private string activeAppId;
    private string BANNER_ID;
    private string INTERSTITIAL_ID;
    private string REWARDED_ID;
    private string LONG_REWARDED_ID;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ResolvePlatformIds();
    }

    private void ResolvePlatformIds()
    {
#if UNITY_ANDROID
        activeAppId = androidAppId;
        BANNER_ID = useTestAds ? "ca-app-pub-3940256099942544/6300978111" : androidBannerId;
        INTERSTITIAL_ID = useTestAds ? "ca-app-pub-3940256099942544/1033173712" : androidInterstitialId;
        REWARDED_ID = useTestAds ? "ca-app-pub-3940256099942544/5224354917" : androidRewardedId;
        LONG_REWARDED_ID = useTestAds ? "ca-app-pub-3940256099942544/5224354917" : androidLongRewardedId;
#elif UNITY_IOS
        activeAppId = string.IsNullOrEmpty(iosAppId) ? androidAppId : iosAppId;
        BANNER_ID = useTestAds ? "ca-app-pub-3940256099942544/2934735716" : (string.IsNullOrEmpty(iosBannerId) ? androidBannerId : iosBannerId);
        INTERSTITIAL_ID = useTestAds ? "ca-app-pub-3940256099942544/4411468910" : (string.IsNullOrEmpty(iosInterstitialId) ? androidInterstitialId : iosInterstitialId);
        REWARDED_ID = useTestAds ? "ca-app-pub-3940256099942544/1712485313" : (string.IsNullOrEmpty(iosRewardedId) ? androidRewardedId : iosRewardedId);
        LONG_REWARDED_ID = useTestAds ? REWARDED_ID : (string.IsNullOrEmpty(iosLongRewardedId) ? REWARDED_ID : iosLongRewardedId);
#else
        activeAppId = androidAppId;
        BANNER_ID = androidBannerId;
        INTERSTITIAL_ID = androidInterstitialId;
        REWARDED_ID = androidRewardedId;
        LONG_REWARDED_ID = androidLongRewardedId;
#endif
    }

    private void Start()
    {
        Debug.Log("[Ads] Starting Ads Manager (Mediation-ready)");
        if (!useAdMob)
        {
            Debug.LogWarning("[Ads] useAdMob is false - no AdMob requests will be made.");
            return;
        }

        // Initialize Google Mobile Ads SDK
        MobileAds.Initialize(initStatus =>
        {
            Debug.Log("[Ads] AdMob initialized.");
            StartCoroutine(DelayedAdLoad());
        });
    }

    private IEnumerator DelayedAdLoad()
    {
        // slight delay to let initialization stabilize
        yield return new WaitForSeconds(0.5f);
        // CreateBannerIfNeeded();
        // LoadBannerAd();

        LoadInterstitialAd();
        LoadRewardedAd(false);

        // StartCoroutine(AutoRefreshBanner());
    }

    #region NO_ADS
    private bool IsNoAds()
    {
        return PlayerPrefs.GetInt("NO_ADS", 0) == 1;
    }
    #endregion

    #region BANNER
    private void CreateBannerIfNeeded()
    {
        if (bannerView != null) return;

        AdSize size = useAdaptiveBanner ? AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth) : AdSize.Banner;
        bannerView = new BannerView(BANNER_ID, size, AdPosition.Bottom);

        bannerView.OnBannerAdLoaded += () =>
        {
            bannerRetryCount = 0;
            // if (isBannerVisible) bannerView.Show();
            Debug.Log("[Ads] AdMob Banner Loaded");
        };

        bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
        {
            bannerRetryCount++;
            Debug.LogWarning($"[Ads] AdMob Banner Failed: {error}. Retry {bannerRetryCount}/{maxBannerRetriesBeforeUnityFallback}");
            if (bannerRetryCount >= maxBannerRetriesBeforeUnityFallback && useUnityFallbackBanner)
            {
                Debug.LogWarning("[Ads] Banner keeps failing. Showing temporary Unity banner (if available).");
                // do NOT flip AdMob flag permanently; Unity fallback is temporary
                ShowUnityBannerFallback();
            }
            else
            {
                // retry after short delay
                StartCoroutine(RetryBannerLoad(2f));
            }
        };
    }

    public void LoadBannerAd()
    {
        if (IsNoAds()) return;
        if (!useAdMob) return;
        if (bannerView == null) CreateBannerIfNeeded();

        // bannerView.LoadAd(new AdRequest());
    }

    public void ShowBanner()
    {
        if (IsNoAds()) { Debug.Log("[Ads] NO_ADS active - skipping banner."); return; }
        if (bannerView == null) CreateBannerIfNeeded();

        // bannerView.Show();
        isBannerVisible = true;
    }

    public void HideBanner()
    {
        if (bannerView != null)
        {
            bannerView.Hide();
            isBannerVisible = false;
        }
    }

    private IEnumerator RetryBannerLoad(float delay)
    {
        yield return new WaitForSeconds(delay);
        LoadBannerAd();
    }

    private IEnumerator AutoRefreshBanner()
    {
        while (true)
        {
            yield return new WaitForSeconds(30f);
            if (isBannerVisible && bannerView != null && !IsNoAds())
            {
                Debug.Log("[Ads] Refreshing AdMob banner...");
                LoadBannerAd();
            }
        }
    }

    // Lightweight Unity banner fallback (optional). No heavy init here (keeps mediation-first approach)
    private void ShowUnityBannerFallback()
    {
#if UNITY_ADS
        if (!useUnityFallbackBanner) return;
        // If you have Unity Ads and placements set, you may show a Unity banner.
        // This call is intentionally minimal: developer can re-enable Unity initialization if they want fallback to work.
        Debug.Log("[Ads] Unity banner fallback requested (ensure Unity Ads SDK and placements configured).");
#endif
    }
    #endregion

    #region INTERSTITIAL
    public void LoadInterstitialAd()
    {
        if (!useAdMob) return;
        if (interstitialLoading) return;

        interstitialLoading = true;
        interstitialAd?.Destroy();
        interstitialAd = null;
        isInterstitialLoaded = false;

        Debug.Log("[Ads] Loading AdMob Interstitial...");
        InterstitialAd.Load(INTERSTITIAL_ID, new AdRequest(), (ad, error) =>
        {
            interstitialLoading = false;
            if (error != null || ad == null)
            {
                Debug.LogWarning($"[Ads] Interstitial load failed: {error}");
                return;
            }

            interstitialAd = ad;
            isInterstitialLoaded = true;
            Debug.Log("[Ads] AdMob Interstitial Loaded");

            ad.OnAdFullScreenContentClosed += () =>
            {
                isInterstitialLoaded = false;
                LoadInterstitialAd();
            };

            ad.OnAdFullScreenContentFailed += (iError) =>
            {
                Debug.LogWarning($"[Ads] Interstitial show failed: {iError}");
                isInterstitialLoaded = false;
            };
        });
    }

    public void ShowInterstitial()
    {
        if (IsNoAds()) { Debug.Log("[Ads] NO_ADS active - skipping interstitial."); return; }
        if (Time.time - lastInterstitialTime < interstitialCooldown)
        {
            Debug.Log("[Ads] Interstitial on cooldown.");
            return;
        }

        if (useAdMob && isInterstitialLoaded && interstitialAd != null && interstitialAd.CanShowAd())
        {
            Debug.Log("[Ads] Showing AdMob Interstitial");
            interstitialAd.Show();
            lastInterstitialTime = Time.time;
            return;
        }

        Debug.Log("[Ads] No interstitial available.");
    }
    #endregion

    #region REWARDED
    public void LoadRewardedAd(bool isLongAd)
    {
        if (!useAdMob) return;
        if (rewardedLoading) return;

        rewardedLoading = true;
        rewardedAd?.Destroy();
        rewardedAd = null;
        isRewardedLoaded = false;

        string unit = isLongAd ? LONG_REWARDED_ID : REWARDED_ID;
        Debug.Log("[Ads] Loading AdMob Rewarded...");
        RewardedAd.Load(unit, new AdRequest(), (ad, error) =>
        {
            rewardedLoading = false;
            if (error != null || ad == null)
            {
                Debug.LogWarning($"[Ads] Rewarded load failed: {error}");
                return;
            }

            rewardedAd = ad;
            isRewardedLoaded = true;
            Debug.Log("[Ads] AdMob Rewarded Loaded");

            ad.OnAdFullScreenContentClosed += () =>
            {
                isRewardedLoaded = false;
                LoadRewardedAd(false);
            };

            ad.OnAdFullScreenContentFailed += (iError) =>
            {
                Debug.LogWarning($"[Ads] Rewarded show failed: {iError}");
                isRewardedLoaded = false;
            };
        });
    }

    public void ShowRewardedAd(Action<bool> onComplete, bool skipReward = false)
    {
        onRewardedCompleteCallback = onComplete;

        if (IsNoAds()) { Debug.Log("[Ads] NO_ADS active - skipping rewarded."); onComplete?.Invoke(false); return; }

        if (useAdMob && isRewardedLoaded && rewardedAd != null && rewardedAd.CanShowAd())
        {
            Debug.Log("[Ads] Showing AdMob Rewarded");
            rewardedAd.Show(reward =>
            {
                // reward callback from SDK
                onRewardedCompleteCallback?.Invoke(!skipReward);
            });
            return;
        }

        Debug.Log("[Ads] No rewarded available.");
        onComplete?.Invoke(false);
    }
    #endregion

    #region PUBLIC HELPERS
    public void ShowInterstitialFromButton() => ShowInterstitial();
    public void ShowRewardedFromButton() => ShowRewardedAd(success => Debug.Log("[Ads] Rewarded from Button: " + success));
    public void ShowBannerFromUI() => ShowBanner();
    public void HideBannerFromUI() => HideBanner();
    #endregion

    private void OnDestroy()
    {
        bannerView?.Destroy();
        interstitialAd?.Destroy();
        rewardedAd?.Destroy();
    }
}
