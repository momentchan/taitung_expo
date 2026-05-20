using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Klak.Hap;
using mj.gist;

namespace TaitungExpo
{
    public class SongManager : SingletonMonoBehaviour<SongManager>
    {
        [Header("Data")]
        [SerializeField] private int currentSongIndex = 0;
        [SerializeField] private Songs songsDatabase;

        [Header("Stems")]
        [SerializeField] [FormerlySerializedAs("stemTrackerVolume")]
        private StemZoneManager stemZoneManager;

        [Header("Video")]
        [SerializeField] private string videoFolderPath = "C:/TaitungExpo";
        [SerializeField] private HapPlayer videoPlayer;

        [Header("Debug")]
        [SerializeField] private bool showSongDebugUI = true;

        private Dictionary<StemType, AsyncOperationHandle<AudioClip>> loadedHandles = new Dictionary<StemType, AsyncOperationHandle<AudioClip>>();

        private bool isSwitchingSong = false;
        private int playingSongIndex = -1;

        /// <summary>Set after a full successful load (use for lyrics/UI that enable after startup).</summary>
        public int LastLoadedSongIndex { get; private set; } = -1;

        public Songs SongsDatabase => songsDatabase;

        /// <summary>Play Mode: last loaded index. Edit Mode: inspector <see cref="currentSongIndex"/>.</summary>
        public int ActiveSongIndex =>
            Application.isPlaying && LastLoadedSongIndex >= 0
                ? LastLoadedSongIndex
                : currentSongIndex;

        public Song CurrentSong =>
            songsDatabase != null && songsDatabase.songs != null
            && ActiveSongIndex >= 0
            && ActiveSongIndex < songsDatabase.songs.Count
                ? songsDatabase.songs[ActiveSongIndex]
                : null;

        public SongType CurrentSongType => CurrentSong != null ? CurrentSong.type : default;

        public event Action<int> OnSongLoaded;
        public event Action<int, Song> OnSongChanged;

        IReadOnlyList<StemAudioZone> AudioZones =>
            stemZoneManager != null ? stemZoneManager.Zones : Array.Empty<StemAudioZone>();

        async void Start()
        {
            await ChangeSong(currentSongIndex);
        }

        void Update()
        {
            TrySelectSongByNumberKey();

            if (currentSongIndex != playingSongIndex && !isSwitchingSong)
                _ = ChangeSong(currentSongIndex);
        }

        void OnGUI()
        {
            if (!showSongDebugUI) return;

            var richLabel = new GUIStyle(GUI.skin.label) { richText = true };

            GUI.backgroundColor = new Color(0, 0, 0, 0.7f);
            GUILayout.BeginArea(new Rect(10, 10, 300, 52), GUI.skin.box);
            GUILayout.Label("<b>SONG MANAGER</b>", richLabel);
            GUILayout.Label($"Current Song: {currentSongIndex} ({(isSwitchingSong ? "Loading..." : "Ready")})", richLabel);
            GUILayout.EndArea();
        }

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

            foreach (var zone in AudioZones)
            {
                if (zone == null) continue;
                zone.Source.Stop();
                zone.Source.clip = null;
            }

            foreach (var handle in loadedHandles.Values)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
            loadedHandles.Clear();

            await LoadAndPlaySong(currentSongIndex);

            isSwitchingSong = false;
            LastLoadedSongIndex = currentSongIndex;
            Song loaded = CurrentSong;
            OnSongLoaded?.Invoke(currentSongIndex);
            OnSongChanged?.Invoke(currentSongIndex, loaded);
        }

        async Task LoadAndPlaySong(int index)
        {
            Song targetSong = songsDatabase.songs[index];
            TryOpenHapVideoForSong(targetSong);
            List<Task> loadTasks = new List<Task>();

            foreach (var zone in AudioZones)
            {
                if (zone == null) continue;

                AssetReferenceT<AudioClip> assetRef = GetStemReference(targetSong, zone.targetStem);
                if (assetRef == null || !assetRef.RuntimeKeyIsValid()) continue;

                var handle = assetRef.LoadAssetAsync<AudioClip>();
                loadedHandles[zone.targetStem] = handle;

                loadTasks.Add(handle.Task.ContinueWith(t =>
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                        zone.Source.clip = handle.Result;
                }, TaskScheduler.FromCurrentSynchronizationContext()));
            }

            await Task.WhenAll(loadTasks);

            foreach (var zone in AudioZones)
            {
                if (zone != null && zone.Source.clip != null)
                    zone.Source.Play();
            }
        }

        void TryOpenHapVideoForSong(Song song)
        {
            if (videoPlayer == null) return;

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

        static AssetReferenceT<AudioClip> GetStemReference(Song song, StemType type)
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
