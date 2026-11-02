using System;
using UnityEngine;

public static class AdManager
{
    //Swap the body for your ad SDK's rewarded ad (AdMob, Unity LevelPlay, etc.)
    //Call onFinished(true) only once the player has watched the whole ad
    public static void ShowRewardedAd(Action<bool> onFinished)
    {
#if UNITY_EDITOR
        Debug.Log("AdManager: pretending a rewarded ad was watched");
        onFinished?.Invoke(true);
#else
        onFinished?.Invoke(false); //No free revives until a real SDK is plugged in
#endif
    }
}