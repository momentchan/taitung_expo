using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Klak.Hap;

namespace TaitungExpo
{
    public class SongManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private int currentSongIndex = 0;
        [SerializeField] private Songs songsDatabase;
        
        [Header("Tracking Parameters")]
        [SerializeField] private float maxDepth = 2f;
        [SerializeField] private float volumeSmoothing = 0.5f;

        [Header("Video")]
        [SerializeField] private string videoFolderPath = "C:/TaitungExpo";
        [SerializeField] private HapPlayer videoPlayer;


        [Header("Scene References")]
        [SerializeField] private List<StemAudioZone> audioZones;
        [SerializeField] private SongLyricRingView[] lyricRingViews;

        [Header("Debug")]
        [SerializeField] private bool showDebugUI = true;

        private Dictionary<StemType, AsyncOperationHandle<AudioClip>> loadedHandles = new Dictionary<StemType, AsyncOperationHandle<AudioClip>>();
        
        private bool isSwitchingSong = false;
        private int playingSongIndex = -1;
        private Vector2 debugUiScroll;

        /// <summary>Set after a full successful load (use for lyrics/UI that enable after startup).</summary>
        public int LastLoadedSongIndex { get; private set; } = -1;

        public Songs SongsDatabase => songsDatabase;

        public Song CurrentSong =>
            songsDatabase != null && songsDatabase.songs != null
            && LastLoadedSongIndex >= 0
            && LastLoadedSongIndex < songsDatabase.songs.Count
                ? songsDatabase.songs[LastLoadedSongIndex]
                : null;

        public event Action<int> OnSongLoaded;
        public event Action<int, Song> OnSongChanged;

        async void Start()
        {
            await ChangeSong(currentSongIndex);
        }

        void Update()
        {
            TrySelectSongByNumberKey();

            // Detection for Inspector changes during Play Mode
            if (currentSongIndex != playingSongIndex && !isSwitchingSong)
            {
                _ = ChangeSong(currentSongIndex); 
            }

            if (TrackerManager.Instance == null || TrackerManager.Instance.ActiveTrackers == null) return;

            foreach (var zone in audioZones)
            {
                var trackersInVolume = TrackerManager.Instance.ActiveTrackers
                    .Where(t => zone.ContainsNormalizedPosition(t.pos))
                    .ToArray();
                    
                zone.UpdateVolume(trackersInVolume, maxDepth, volumeSmoothing);
            }
        }

        // OnGUI runs on the main thread and is handy for quick debug overlays
        void OnGUI()
        {
            if (!showDebugUI || audioZones == null) return;

            var richLabel = new GUIStyle(GUI.skin.label) { richText = true };

            // Fixed-area height clips rows past the bottom; use a scroll view so every zone stays reachable.
            const float panelWidth = 300f;
            float panelHeight = Mathf.Clamp(Screen.height - 40f, 160f, 480f);

            GUI.backgroundColor = new Color(0, 0, 0, 0.7f);
            GUILayout.BeginArea(new Rect(10, 10, panelWidth, panelHeight), GUI.skin.box);
            debugUiScroll = GUILayout.BeginScrollView(debugUiScroll);
            GUILayout.Label("<b>SONG MANAGER DEBUG</b>", richLabel);
            GUILayout.Label($"Current Song: {currentSongIndex} ({(isSwitchingSong ? "Loading..." : "Ready")})", richLabel);
            GUILayout.Space(5);

            foreach (var zone in audioZones)
            {
                if (zone == null) continue;

                string stemName = zone.targetStem.ToString();
                string posName = zone.positionLabel.ToString();
                float vol = zone.CurrentVolume;
                string bar = new string('|', (int)(vol * 20));

                GUILayout.Label($"{posName} [{stemName}]: {vol:F2}", richLabel);
                GUILayout.Label($"<color=green>{bar}</color>", richLabel);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // Digit keys match list index: 0–9 on main row or keypad (ignored if out of range or already playing).
        void TrySelectSongByNumberKey()
        {
            if (songsDatabase == null || songsDatabase.songs == null) return;
            if (!Application.isFocused) return;

            int? index = null;
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
                index = 0;
            else
            {
                for (int n = 1; n <= 9; n++)
                {
                    KeyCode alpha = (KeyCode)((int)KeyCode.Alpha1 + n - 1);
                    KeyCode keypad = (KeyCode)((int)KeyCode.Keypad1 + n - 1);
                    if (Input.GetKeyDown(alpha) || Input.GetKeyDown(keypad))
                    {
                        index = n;
                        break;
                    }
                }
            }

            if (index == null) return;
            int i = index.Value;
            if (i < 0 || i >= songsDatabase.songs.Count) return;
            if (isSwitchingSong || i == playingSongIndex) return;
            _ = ChangeSong(i);
        }

        public async Task ChangeSong(int newIndex)
        {
            if (isSwitchingSong) return;
            if (newIndex < 0 || newIndex >= songsDatabase.songs.Count)
            {
                Debug.LogWarning("Invalid Song Index!");
                return;
            }

            isSwitchingSong = true;
            currentSongIndex = newIndex;
            playingSongIndex = newIndex;

            // Stop playback and clear clips
            foreach (var zone in audioZones)
            {
                zone.Source.Stop();
                zone.Source.clip = null;
            }

            // Release previous Addressable assets from memory
            foreach (var handle in loadedHandles.Values)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
            loadedHandles.Clear();

            // Load new assets
            await LoadAndPlaySong(currentSongIndex);
            
            isSwitchingSong = false;
            LastLoadedSongIndex = currentSongIndex;
            Song loaded = CurrentSong;
            OnSongLoaded?.Invoke(currentSongIndex);
            OnSongChanged?.Invoke(currentSongIndex, loaded);
            NotifyLyricViews();
        }

        void NotifyLyricViews()
        {
            if (lyricRingViews == null) return;
            foreach (var view in lyricRingViews)
            {
                if (view != null)
                    view.SyncToSongManager(this);
            }
        }

        private async Task LoadAndPlaySong(int index)
        {
            Song targetSong = songsDatabase.songs[index];
            TryOpenHapVideoForSong(targetSong);
            List<Task> loadTasks = new List<Task>();

            foreach (var zone in audioZones)
            {
                AssetReferenceT<AudioClip> assetRef = GetStemReference(targetSong, zone.targetStem);
                if (assetRef == null || !assetRef.RuntimeKeyIsValid()) continue;

                var handle = assetRef.LoadAssetAsync<AudioClip>();
                loadedHandles[zone.targetStem] = handle;
                
                // Add to parallel loading tasks
                loadTasks.Add(handle.Task.ContinueWith(t => 
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        zone.Source.clip = handle.Result;
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext())); 
            }

            // Wait for all stems to finish loading
            await Task.WhenAll(loadTasks);

            // Synchronized start
            foreach (var zone in audioZones)
            {
                if (zone.Source.clip != null)
                {
                    zone.Source.Play();
                }
            }
        }

        void TryOpenHapVideoForSong(Song song)
        {
            if (videoPlayer == null) return;

            // Same HapPlayer instance: Klak blocks Open() while a stream exists, so tear down like OnDestroy first.
            videoPlayer.CloseStreamIfOpen();

            string relativeOrName = song != null ? song.videoFileName : null;
            string fullPath = null;
            if (!string.IsNullOrWhiteSpace(relativeOrName))
            {
                var trimmed = relativeOrName.Trim();
                fullPath = Path.IsPathRooted(trimmed)
                    ? trimmed
                    : Path.Combine(videoFolderPath, trimmed);
                fullPath = Path.GetFullPath(fullPath);
            }

            videoPlayer.time = 0f;
            if (!string.IsNullOrEmpty(fullPath))
                videoPlayer.Open(fullPath, HapPlayer.PathMode.LocalFileSystem);
        }

        private AssetReferenceT<AudioClip> GetStemReference(Song song, StemType type)
        {
            switch (type)
            {
                case StemType.Origin: return song.origin;
                case StemType.Vocal: return song.vocal;
                case StemType.Chord: return song.chord;
                case StemType.Bass: return song.bass;
                case StemType.HiDrums: return song.hidrums;
                case StemType.LoDrums: return song.lowdrums;
                default: return null;
            }
        }
        
        void OnDestroy()
        {
            foreach (var handle in loadedHandles.Values)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
            loadedHandles.Clear();
        }
    }
}