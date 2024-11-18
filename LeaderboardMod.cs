using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Il2Cpp.ReplayLoader;

namespace ClassLibrary1
{
    public class Leaderboard_Mod : MelonMod
    {
        private static DateTime lastRequestTime = DateTime.MinValue;
        //private static readonly Queue<Func<Task>> _executionQueue = new();
        private static readonly HttpClient httpClient = new();
        private static CancellationTokenSource cts = new();


        public override void OnInitializeMelon()
        {
            Melon<Leaderboard_Mod>.Logger.Msg("Leaderboard_Mod initialized.");
        }

        //Enqueue method and task execution
        public override void OnUpdate()
        {
            lock (_executionQueue)
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
                        Melon<Leaderboard_Mod>.Logger.Error($"Exception during task execution: {ex.Message}");
                    }
                }
            }
        }

        private static readonly Queue<Action> _executionQueue = new();

        public static void Enqueue(Action action)
        {
            if (action == null)
                return;

            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        [HarmonyPatch(typeof(LeaderboardManager), "retrieveLeaderboardDelayed")]
        private static class PatchRetrieveLeaderboard
        {
       
            private static bool Prefix(int course, bool layout, bool night, int car)
            {
                var instance = LeaderboardManager.instance;

                if (instance == null)
                {
                    // Let the original method handle this case
                    return true;
                }

                // Initialize leaderboard UI
                instance.status.text = "<#fcbe03>Retrieving Custom Leaderboard...";
                instance.contentPane.sizeDelta = new Vector2(instance.contentPane.sizeDelta.x, 0f);

                // Clean up existing leaderboard entries
                try
                {
                    int childCount = instance.contentPane.childCount;
                    for (int i = childCount - 1; i >= 0; i--)
                    {
                        Transform child = instance.contentPane.GetChild(i);
                        UnityEngine.Object.Destroy(child.gameObject);
                    }
                }
                catch (Exception ex)
                {
                    Melon<Leaderboard_Mod>.Logger.Error($"Exception during child destruction: {ex.Message}");
                }

                instance.entries = Array.Empty<Button>();
                // Cancel previous requests
                if (cts != null && !cts.IsCancellationRequested)
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                cts = new CancellationTokenSource();

                var localCts = cts;

                // Enqueue the request to the main thread
                Leaderboard_Mod.Enqueue(async () =>
                {
                    var cancellationToken = localCts.Token;

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    lastRequestTime = DateTime.UtcNow;

                    Melon<Leaderboard_Mod>.Logger.Msg("Sending request to retrieve leaderboard...");
                    var records = await RetrieveLeaderboard(course, layout, night, car, cancellationToken);

                    if (records != null && !cancellationToken.IsCancellationRequested)
                    {
                        // Enqueue the UI update on the main thread
                        Leaderboard_Mod.Enqueue(() => OnLeaderboardRetrieved(records, instance, cancellationToken));
                    }
                    else
                    {
                        Melon<Leaderboard_Mod>.Logger.Msg("No records retrieved or request was canceled");
                    }
                });

                return false; // Skip original method
            }


            private static async Task<List<LeaderboardRecord>> RetrieveLeaderboard(int course, bool layout, bool night, int car, CancellationToken cancellationToken)
            {
                string url = $"{Utils.getServerAddress()}/getRecords/?track={course}&layout={(layout ? 1 : 0)}&condition={(night ? 1 : 0)}&car={car}";

                try
                {
                    // Send the HTTP request
                    var responseText = await SendHttpRequestAsync(url, cancellationToken);

                    // Check for cancellation before proceeding
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }

                    if (string.IsNullOrEmpty(responseText))
                    {
                        Melon<Leaderboard_Mod>.Logger.Error("Failed to retrieve leaderboard data: responseText is null or empty.");
                        return null;
                    }

                    Melon<Leaderboard_Mod>.Logger.Msg("Successfully retrieved leaderboard data.");

                    // Parse the JSON response
                    try
                    {
                        var parsedData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                        if (parsedData == null || !parsedData.ContainsKey("records"))
                        {
                            Melon<Leaderboard_Mod>.Logger.Error("Parsed data is null or does not contain 'records'.");
                            Melon<Leaderboard_Mod>.Logger.Error($"Response Text: {responseText}");
                            return null;
                        }

                        var recordsJson = parsedData["records"].ToString();
                        if (string.IsNullOrEmpty(recordsJson))
                        {
                            Melon<Leaderboard_Mod>.Logger.Error("Records JSON is null or empty.");
                            Melon<Leaderboard_Mod>.Logger.Error($"Parsed Data: {parsedData}");
                            return null;
                        }

                        List<LeaderboardRecord> records = JsonConvert.DeserializeObject<List<LeaderboardRecord>>(recordsJson);

                        Melon<Leaderboard_Mod>.Logger.Msg($"Parsed {records.Count} records.");
                        return records;
                    }
                    catch (JsonSerializationException jse)
                    {
                        Melon<Leaderboard_Mod>.Logger.Error($"JSON Serialization Exception: {jse.Message}");
                        Melon<Leaderboard_Mod>.Logger.Error($"Response Text: {responseText}");
                        return null;
                    }
                    catch (Exception ex)
                    {
                        Melon<Leaderboard_Mod>.Logger.Error($"Exception during deserialization: {ex.Message}");
                        Melon<Leaderboard_Mod>.Logger.Error($"Response Text: {responseText}");
                        return null;
                    }
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
                catch (Exception ex)
                {
                    Melon<Leaderboard_Mod>.Logger.Error($"Exception during RetrieveLeaderboard: {ex.Message}");
                    return null;
                }
            }


            public static async Task<string> SendHttpRequestAsync(string url, CancellationToken cancellationToken)
            {
                try
                {
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        HttpResponseMessage response = null;
                        // Wait a bit and see if it got cancelled, puts a cap of one request per half second
                        await Task.Delay(500);
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            Melon<Leaderboard_Mod>.Logger.Msg("Sending request!");
                            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken); if (response.IsSuccessStatusCode)
                            {
                                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                                return responseBody;
                            }
                            else
                            {
                                Melon<Leaderboard_Mod>.Logger.Error($"HTTP Error: {response.StatusCode}");
                                return null;
                            }
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
                catch (HttpRequestException ex)
                {
                    Melon<Leaderboard_Mod>.Logger.Error($"HTTP Request Exception: {ex.Message}");
                    return null;
                }
                catch (Exception ex)
                {
                    Melon<Leaderboard_Mod>.Logger.Error($"Unexpected exception in SendHttpRequestAsync: {ex.Message}");
                    return null;
                }
            }


            private static async Task OnLeaderboardRetrieved(List<LeaderboardRecord> records, LeaderboardManager instance, CancellationToken cancellationToken)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (records == null || records.Count == 0)
                {
                    instance.status.text = "NO RECORDS";
                    Melon<Leaderboard_Mod>.Logger.Msg("No records to display.");
                    return;
                }
                else
                {
                    instance.status.text = "";
                }

                instance.entries = new Button[records.Count];
                float height = instance.entryPrefab.GetComponent<RectTransform>().rect.height;
                float currentBestTime = TimeAttackTimings.singleton.getCurrentBestTime();

                for (int i = 0; i < records.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    try
                    {
                        LeaderboardRecord record = records[i];

                        GameObject gameObject = UnityEngine.Object.Instantiate(instance.entryPrefab, instance.contentPane);
                        gameObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, i * -height);

                        LeaderboardEntry entryComponent = gameObject.GetComponent<LeaderboardEntry>();
                        entryComponent.setPos(i + 1);
                        entryComponent.setDrivername(record.driver_name);
                        entryComponent.setTime(record.timing);

                        // Set focus on the first entry
                        if (i == 0)
                        {
                            instance.toFocus = gameObject.GetComponent<RectTransform>();
                        }

                        // Highlight the current player's best time
                        if (Mathf.Approximately(record.timing, currentBestTime) &&
                            record.driver_name.Equals(PlayerPrefs.GetString("Driver Name")))
                        {
                            entryComponent.highLight();
                            instance.toFocus = gameObject.GetComponent<RectTransform>();
                        }

                        instance.entries[i] = gameObject.GetComponent<Button>();

                        // Setup the button listener
                        string recordID = record.id;
                        int replayTrack = record.track;
                        bool layoutBool = record.layout != 0;
                        bool replayCondition = record.condition == 1;
                        float timing = record.timing;

                        gameObject.GetComponent<Button>().onClick.AddListener((UnityEngine.Events.UnityAction)(() =>
                        {
                            instance.viewOnlineReplay(recordID, replayTrack, layoutBool, replayCondition, timing);
                        }));
                    }
                    catch (Exception ex)
                    {
                        Melon<Leaderboard_Mod>.Logger.Error($"ERROR processing leaderboard entry {i}: {ex.Message}");
                    }
                }

                // Update navigation for the entries
                UpdateEntryNavigation(instance.entries);

                // Adjust contentPane size
                Vector2 sizeDelta = instance.contentPane.sizeDelta;
                sizeDelta.y = records.Count * height;
                instance.contentPane.sizeDelta = sizeDelta;

                // Focus on the first entry if available
                if (instance.toFocus != null)
                {
                    FocusOnEntry(instance);
                }
            }


            private static void UpdateEntryNavigation(Button[] entries)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    Navigation nav = entries[i].navigation;
                    nav.mode = Navigation.Mode.Explicit;
                    nav.selectOnUp = (i > 0) ? entries[i - 1] : null;
                    nav.selectOnDown = (i < entries.Length - 1) ? entries[i + 1] : null;
                    entries[i].navigation = nav;
                }
            }

            private static void FocusOnEntry(LeaderboardManager instance)
            {
                if (instance.toFocus != null)
                {
                    RectTransform contentPane = instance.contentPane;
                    RectTransform sRect = instance.sRect; // Scroll Rect
                    RectTransform target = instance.toFocus;

                    Vector2 anchoredPosition = contentPane.anchoredPosition;
                    anchoredPosition.y = -target.anchoredPosition.y - sRect.rect.height * 0.5f + target.rect.height * 0.5f;
                    contentPane.anchoredPosition = anchoredPosition;
                }
            }

        }


        //[HarmonyPatch(typeof(ReplayLoader), "retrieveReplay")]
        //private static class PatchRetrieveReplay
        //{
        //    private static readonly FieldInfo readHeaderField = AccessTools.Field(typeof(ReplayLoader), "readHeader");
        //    private static readonly FieldInfo readDataField = AccessTools.Field(typeof(ReplayLoader), "readData");
        //    private static readonly FieldInfo readingField = AccessTools.Field(typeof(ReplayLoader), "reading");
        //    private static readonly FieldInfo readingCompleteField = AccessTools.Field(typeof(ReplayLoader), "readingComplete");

        //    private static bool Prefix(ReplayLoader __instance, string recordID)
        //    {
        //        var instance = __instance;

        //        // Access the readingComplete UnityEvent
        //        var readingComplete = readingCompleteField.GetValue(instance) as UnityEvent;

        //        // Set 'reading' to true
        //        readingField.SetValue(instance, true);

        //        // Start the async task to fetch and process the replay
        //        FetchAndProcessReplay(instance, recordID, readingComplete);

        //        // Skip the original method
        //        return false;
        //    }

        //    private static void FetchAndProcessReplay(ReplayLoader instance, string recordID, UnityEvent readingComplete)
        //    {
        //        // Enqueue the task to the main thread
        //        Leaderboard_Mod.Enqueue(async () =>
        //        {
        //            // Build the URL
        //            string apiKey = "YOUR_ACTUAL_API_KEY"; // Replace with your API key
        //            string url = $"https://yourapi.com/getReplay?id={recordID}&apiKey={apiKey}"; // Replace with your API URL

        //            byte[] replayData = null;
        //            try
        //            {
        //                Melon<Leaderboard_Mod>.Logger.Msg("Sending request to retrieve replay...");
        //                replayData = await SendHttpRequestForReplayAsync(url);

        //                if (replayData == null)
        //                {
        //                    Melon<Leaderboard_Mod>.Logger.Error("Failed to retrieve replay data: responseData is null.");
        //                    // Handle error: go back to main menu
        //                    Enqueue(() =>
        //                    {
        //                        EventManager.singleton.goToMainMenu();
        //                    });
        //                    return;
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                Melon<Leaderboard_Mod>.Logger.Error($"Exception during Replay retrieval: {ex.Message}");
        //                // Handle error: go back to main menu
        //                Enqueue(() =>
        //                {
        //                    EventManager.singleton.goToMainMenu();
        //                });
        //                return;
        //            }

        //            // Now, process the replay data on a separate thread
        //            Task.Run(() =>
        //            {
        //                ProcessReplayData(instance, replayData);

        //                // Once processing is complete, invoke readingComplete on the main thread
        //                Enqueue(() =>
        //                {
        //                    readingComplete?.Invoke();
        //                });
        //            });
        //        });
        //    }

        //    private static async Task<byte[]> SendHttpRequestForReplayAsync(string url)
        //    {
        //        try
        //        {
        //            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
        //            {
        //                // Send the HTTP request
        //                HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        //                if (response.IsSuccessStatusCode)
        //                {
        //                    byte[] data = await response.Content.ReadAsByteArrayAsync();
        //                    return data;
        //                }
        //                else
        //                {
        //                    Melon<Leaderboard_Mod>.Logger.Error($"HTTP Error: {response.StatusCode}");
        //                    return null;
        //                }
        //            }
        //        }
        //        catch (HttpRequestException ex)
        //        {
        //            Melon<Leaderboard_Mod>.Logger.Error($"HTTP Request Exception: {ex.Message}");
        //            return null;
        //        }
        //        catch (Exception ex)
        //        {
        //            Melon<Leaderboard_Mod>.Logger.Error($"Unexpected exception in SendHttpRequestForReplayAsync: {ex.Message}");
        //            return null;
        //        }
        //    }

        //    private static void ProcessReplayData(ReplayLoader instance, byte[] replayData)
        //    {
        //        try
        //        {
        //            // Deserialize the replay data
        //            using (MemoryStream memoryStream = new MemoryStream(replayData))
        //            {
        //                BinaryFormatter binaryFormatter = new BinaryFormatter();

        //                // Deserialize the NetworkReplay object
        //                ReplayLoader.NetworkReplay networkReplay = (ReplayLoader.NetworkReplay)binaryFormatter.Deserialize(memoryStream);

        //                // Decompress the data
        //                var readHeader = networkReplay.header;
        //                var readData = (ReplayLoader.ReplayData)networkReplay.compressedData.getDecompressedObject();

        //                // Set the fields via reflection
        //                readHeaderField.SetValue(instance, readHeader);
        //                readDataField.SetValue(instance, readData);
        //                readingField.SetValue(instance, false);
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Melon<Leaderboard_Mod>.Logger.Error($"Exception during replay data processing: {ex.Message}");
        //            // Handle error: go back to main menu
        //            Enqueue(() =>
        //            {
        //                EventManager.singleton.goToMainMenu();
        //            });
        //        }
        //    }
        //}
    }

    // Define the LeaderboardRecord class
    public class LeaderboardRecord
    {
        public string driver_name { get; set; }
        public float timing { get; set; }
        public string id { get; set; }
        public int track { get; set; }
        public int layout { get; set; }
        public int condition { get; set; }
    }
}