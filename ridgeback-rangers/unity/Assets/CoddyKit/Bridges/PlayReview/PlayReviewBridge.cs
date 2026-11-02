// CoddyKit (Unity) — Google Play In-App Review bridge (package com.google.play.review >= 1.8.0 from Google's Play Unity
// plugins tarballs, asmdef versionDefines -> CODDY_PLAY_REVIEW). Only the OS sheet: request flow -> launch flow; the
// result never tells whether the player rated (by design). The gate is CoddyReview / ReviewGate.
// UNCOMPILED until the Unity editor + the package are installed.
#if CODDY_PLAY_REVIEW
using System;
using Google.Play.Review;
using UnityEngine;

namespace CoddyKit.Bridges
{
    public static class PlayReviewBridgeRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            CoddyServices.Review = new PlayReviewProvider();
            CoddyServices.Registered.Add("PlayReview");
#endif
        }
    }

    public sealed class PlayReviewProvider : IReviewProvider
    {
        ReviewManager manager;

        public void Request(Action<bool, string> done)
        {
            manager ??= new ReviewManager();
            var request = manager.RequestReviewFlow();
            request.Completed += op =>
            {
                if (op.Error != ReviewErrorCode.NoError)
                {
                    done?.Invoke(false, op.Error.ToString());
                    return;
                }
                var launch = manager.LaunchReviewFlow(op.GetResult());
                launch.Completed += l => done?.Invoke(l.Error == ReviewErrorCode.NoError, l.Error.ToString());
            };
        }
    }
}
#endif
