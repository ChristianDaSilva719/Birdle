using System;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using UnityEngine;

public class AdManager : MonoBehaviour
{
    //-----Ad unit IDs-----//
    const string TestAndroidRewarded = "ca-app-pub-3667655174213243/9812707496";

    const string RealAndroidRewarded = "YOUR_ANDROID_REWARDED_AD_UNIT_ID";

    const bool UseTestAds = true;

    const bool SkipAdsInEditor = true;

    const float RetryDelay = 10f;

    static AdManager instance;

    private RewardedAd rewarded;
    private bool initialised;
    private bool loading;
    private bool showing;
    private float nextLoadTime;
    private Action<bool> pendingCallback;
    private volatile bool earnedReward; 

    private readonly Queue<Action> mainThreadQueue = new Queue<Action>();

    public static bool RewardedReady
    {
        get
        {
#if UNITY_EDITOR
            if (SkipAdsInEditor) return true;
#endif
            return instance != null && instance.rewarded != null && instance.rewarded.CanShowAd();
        }
    }

    //Creates the AdManager by itself if there isn't one in the scene, so no setup is needed
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (instance == null)
            new GameObject("AdManager").AddComponent<AdManager>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (instance != this) return;

#if UNITY_EDITOR
        if (SkipAdsInEditor) return;
#endif

        MobileAds.Initialize(status =>
        {
            RunOnMainThread(() =>
            {
                Debug.Log("AdManager: Google ads are initialised");
                initialised = true;
                LoadAd();
            });
        });
    }

    void Update()
    {
        //Run anything the ad callbacks queued up
        while (true)
        {
            Action next;
            lock (mainThreadQueue)
            {
                if (mainThreadQueue.Count == 0) break;
                next = mainThreadQueue.Dequeue();
            }
            next();
        }

        //Keep one ad loaded and ready, retrying after a failure
        if (initialised && rewarded == null && !loading && !showing && Time.unscaledTime >= nextLoadTime)
            LoadAd();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (rewarded != null) rewarded.Destroy();
    }

    void RunOnMainThread(Action action)
    {
        lock (mainThreadQueue) { mainThreadQueue.Enqueue(action); }
    }

    //-----Ad unit-----//
    string RewardedId()
    {
#if UNITY_ANDROID
        string id = UseTestAds ? TestAndroidRewarded : RealAndroidRewarded;
#elif UNITY_IOS
        string id = UseTestAds ? TestIosRewarded : RealIosRewarded;
#else
        string id = "unused";
#endif
        if (id.StartsWith("YOUR_"))
            Debug.LogError("AdManager: paste your real rewarded ad unit ID into AdManager.cs, or leave UseTestAds on.");

        return id;
    }

    //-----Loading-----//
    void LoadAd()
    {
        if (loading || rewarded != null) return;
        loading = true;

        RewardedAd.Load(RewardedId(), new AdRequest(), (RewardedAd ad, LoadAdError error) =>
        {
            RunOnMainThread(() =>
            {
                loading = false;

                if (error != null || ad == null)
                {
                    Debug.LogWarning("AdManager: rewarded ad failed to load: " + error);
                    nextLoadTime = Time.unscaledTime + RetryDelay;
                    return;
                }

                rewarded = ad;
            });
        });
    }

    //-----Showing-----//
    //GameManager calls this. onFinished(true) only fires if the player watched the ad all the way through
    public static void ShowRewardedAd(Action<bool> onFinished)
    {
        if (instance == null)
        {
            Debug.LogWarning("AdManager: there is no AdManager in the scene.");
            onFinished?.Invoke(false);
            return;
        }

        instance.Show(onFinished);
    }

    void Show(Action<bool> onFinished)
    {
#if UNITY_EDITOR
        if (SkipAdsInEditor)
        {
            Debug.Log("AdManager: editor mode, pretending the ad was watched");
            onFinished?.Invoke(true);
            return;
        }
#endif

        if (showing) return; //Ignore double taps while an ad is up

        if (rewarded == null || !rewarded.CanShowAd())
        {
            Debug.Log("AdManager: no ad is ready yet, try again in a moment");
            LoadAd();
            onFinished?.Invoke(false);
            return;
        }

        showing = true;
        earnedReward = false;
        pendingCallback = onFinished;

        RewardedAd ad = rewarded;
        rewarded = null;

        ad.OnAdFullScreenContentClosed += () => RunOnMainThread(() => FinishShow(ad));
        ad.OnAdFullScreenContentFailed += (AdError error) => RunOnMainThread(() =>
        {
            Debug.LogWarning("AdManager: ad failed to show: " + error);
            earnedReward = false;
            FinishShow(ad);
        });

        ad.Show(reward => { earnedReward = true; });
    }

    //Runs once the ad has been closed (or failed to open)
    void FinishShow(RewardedAd ad)
    {
        if (!showing) return;
        showing = false;

        ad.Destroy();

        Action<bool> callback = pendingCallback;
        pendingCallback = null;
        bool success = earnedReward;

        LoadAd(); //Get the next one ready

        callback?.Invoke(success);
    }
}