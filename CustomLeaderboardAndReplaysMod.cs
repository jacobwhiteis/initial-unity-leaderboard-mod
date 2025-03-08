
#pragma warning disable IDE0051

using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Runtime.Remoting.Messaging;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using System.Collections;
using static ModNamespace.CustomLeaderboardAndReplayMod;
using Il2CppTMPro;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ModNamespace
{
    public partial class CustomLeaderboardAndReplayMod : MelonMod
    {

        //private GameObject ambienceObject;
        //private AudioSource audioSource;

        //public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        //{

        //MelonLogger.Msg("A scene was loaded");
        //if (sceneName == "Tsuchisaka")
        //{
        //    MelonLogger.Msg($"Scene Loaded: {sceneName} (Index: {buildIndex})");

        //    // If an ambience object already exists, don't create a new one
        //    if (ambienceObject != null)
        //    {
        //        MelonLogger.Msg("Ambience already exists. Skipping creation.");
        //        return;
        //    }

        //    // Create a new GameObject
        //    ambienceObject = new GameObject("AmbienceSound");
        //    UnityEngine.Object.DontDestroyOnLoad(ambienceObject); // Persist across scene loads

        //    MelonLogger.Msg("Created ambient object");

        //    // Add an AudioSource component
        //    audioSource = ambienceObject.AddComponent<AudioSource>();
        //    audioSource.loop = true;
        //    audioSource.playOnAwake = false; // Don't play until the clip is loaded
        //    audioSource.volume = 0.5f; // Adjust as needed
        //    audioSource.spatialBlend = 0f; // Fully 2D sound

        //    MelonLogger.Msg("playing audio clip");

        //    // Load the audio asynchronously
        //    MelonCoroutines.Start(LoadAudioClip($"file://{Application.streamingAssetsPath}/sound.wav"));

        //    MelonLogger.Msg("Created ambient object");
        //    }
        //}

        //private IEnumerator LoadAudioClip(string audioPath)
        //{
        //    WWW www = new WWW(audioPath);
        //    yield return www;

        //    if (!string.IsNullOrEmpty(www.error))
        //    {
        //        MelonLogger.Warning($"Failed to load ambience sound: {www.error}");
        //        yield break;
        //    }

        //    AudioClip clip = www.GetAudioClip(false, true, AudioType.WAV);
        //    if (clip != null)
        //    {
        //        audioSource.clip = clip;
        //        audioSource.Play();
        //        MelonLogger.Msg("Ambience sound started.");
        //        MelonCoroutines.Start(UpdateAudioPosition());
        //    }
        //    else
        //    {
        //        MelonLogger.Warning("Failed to load AudioClip from path.");
        //    }
        //}

        //private IEnumerator UpdateAudioPosition()
        //{
        //    while (true)
        //    {
        //        if (Camera.main != null)
        //        {
        //            ambienceObject.transform.position = Camera.main.transform.position; // Keep at camera position
        //            ambienceObject.transform.rotation = Camera.main.transform.rotation; // Rotate with the camera
        //        }
        //        yield return null;
        //    }
        //}


        private static readonly HttpClient httpClient = new();
        private static readonly Queue<Action> _executionQueue = new();
        private static readonly object _queueLock = new();

        // Static field that gets set to assist with uploading replays to leaderboard
        private static string uploadReplayJson;
  

        public static string BuildAPIKey()
        {

            var base64EncodedBytes = System.Convert.FromBase64String(Constants.ApiKey1 + Constants.ApiKey2 + Constants.ApiKey3);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        public override void OnInitializeMelon()
        {
            Melon<CustomLeaderboardAndReplayMod>.Logger.Msg("CustomLeaderboardAndReplayMod initialized.");

            // Create the mod replay and ghost folders if not present
            string rootFolder = Utils.getRootFolder();
            string ghostPath = Path.Combine(rootFolder, "ModGhosts");
            string replayPath = Path.Combine(rootFolder, "ModReplays");

            if (!Directory.Exists(ghostPath))
            {
                Directory.CreateDirectory(ghostPath);
                Melon<CustomLeaderboardAndReplayMod>.Logger.Msg($"Directory created: {ghostPath}");
            }
            if (!Directory.Exists(replayPath))
            {
                Directory.CreateDirectory(replayPath);
                Melon<CustomLeaderboardAndReplayMod>.Logger.Msg($"Directory created: {replayPath}");
            }

        }


        // Method to enqueue actions to be executed on the main thread
        public static void Enqueue(Action action)
        {
            if (action == null)
                return;

            lock (_queueLock)
            {
                _executionQueue.Enqueue(() =>
                {
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"Exception in Enqueued Action: {ex.Message}");
                    }
                });
            }
        }

        public override void OnUpdate()
        {

            // Execute any queued actions on the main thread
            lock (_queueLock)
            {
                while (_executionQueue.Count > 0)
                {
                    var action = _executionQueue.Dequeue();
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"Exception during task execution: {ex.Message}");
                    }
                }
            }
        }

    }

}