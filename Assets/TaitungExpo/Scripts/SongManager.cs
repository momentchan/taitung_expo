using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Klak.Hap;
using mj.gist;
using PrefsGUI;
using PrefsGUI.RapidGUI;

namespace TaitungExpo
{
    public enum SongPlaybackMode
    {
        Transition,
        Interaction
    }

    public class SongManager : SingletonMonoBehaviour<SongManager>, IGUIUser
    {
        #region IGUIUser

        public string GetName() => "SongManager";

        public void ShowGUI()
        {
            EnsureVisualFadePrefs();
            visualFadeDurationPrefs.DoGUISlider(0.001f, 10f, "Visual Fade Duration");
        }

        public void SetupGUI()
        {
            EnsureVisualFadePrefs();
        }

        void EnsureVisualFadePrefs()
        {
            visualFadeDurationPrefs ??= new PrefsFloat($"{GetName()}_visualFadeDuration", visualFadeDuration);
        }

        #endregion

        [Header("Data")]
        [SerializeField] private int currentSongIndex = 0;
        [SerializeField] private Songs songsDatabase;

        [Header("Stems")]
        [SerializeField] [FormerlySerializedAs("stemTrackerVolume")]
        private StemZoneManager stemZoneManager;

        [Header("Video")]
        [SerializeField] private string videoFolderPath = "C:/TaitungExpo";
        [SerializeField] private HapPlayer videoPlayer;
        [SerializeField] private VideoEffect videoEffect;

        [Header("Text")]
        [SerializeField] private TextQuad textQuad;

        [Header("Playback Cycle")]
        [SerializeField] private bool autoStartPlaybackCycle = true;
        [SerializeField] [Min(0.001f)] private float visualFadeDuration = 2f;

        [Header("Debug")]
        [SerializeField] private bool showSongDebugUI = true;

        private Dictionary<StemType, AsyncOperationHandle<AudioClip>> loadedHandles = new Dictionary<StemType, AsyncOperationHandle<AudioClip>>();

        private bool isSwitchingSong = false;
        private int playingSongIndex = -1;
        private Coroutine playbackCycleCoroutine;
        private bool hasStartedPlaybackCycle;
        PrefsFloat visualFadeDurationPrefs;

        /// <summary>Set after a full successful load (use for lyrics/UI that enable after startup).</summary>
        public int LastLoadedSongIndex { get; private set; } = -1;
        public SongPlaybackMode CurrentPlaybackMode { get; private set; } = SongPlaybackMode.Transition;

        public float VisualFadeDuration
        {
            get
            {
                EnsureVisualFadePrefs();
                float value = visualFadeDurationPrefs != null
                    ? visualFadeDurationPrefs.Get()
                    : visualFadeDuration;
                return Mathf.Max(0.001f, value);
            }
        }

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
        public event Action<SongPlaybackMode> OnPlaybackModeChanged;

        IReadOnlyList<StemAudioZone> AudioZones =>
            stemZoneManager != null ? stemZoneManager.Zones : Array.Empty<StemAudioZone>();

        protected override void Awake()
        {
            base.Awake();
            SetupGUI();
        }

        async void Start()
        {
            ResolveSceneReferences();
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
            GUILayout.BeginArea(new Rect(10, 10, 300, 72), GUI.skin.box);
            GUILayout.Label("<b>SONG MANAGER</b>", richLabel);
            GUILayout.Label($"Current Song: {currentSongIndex} ({(isSwitchingSong ? "Loading..." : "Ready")})", richLabel);
            GUILayout.Label($"Mode: {CurrentPlaybackMode}", richLabel);
            GUILayout.EndArea();
        }

        void TrySelectSongByNumberKey()
        {
            if (songsDatabase == null || songsDatabase.songs == null) return;
            if (!Application.isFocused) return;
            if (!Input.GetKey(KeyCode.LeftShift)) return;

            int? songIndex = null;
            for (int key = 1; key <= 8; key++)
            {
                KeyCode alpha = (KeyCode)((int)KeyCode.Alpha1 + key - 1);
                KeyCode keypad = (KeyCode)((int)KeyCode.Keypad1 + key - 1);
                if (Input.GetKeyDown(alpha) || Input.GetKeyDown(keypad))
                {
                    songIndex = key - 1;
                    break;
                }
            }

            if (songIndex == null) return;
            int i = songIndex.Value;
            if (i < 0 || i >= songsDatabase.songs.Count) return;
            if (isSwitchingSong || i == playingSongIndex) return;
            _ = ChangeSong(i);
        }

        public async Task ChangeSong(int newIndex)
        {
            if (isSwitchingSong) return;
            if (songsDatabase == null || songsDatabase.songs == null
                || newIndex < 0 || newIndex >= songsDatabase.songs.Count)
            {
                Debug.LogWarning("Invalid Song Index!");
                return;
            }

            ResolveSceneReferences();
            StopPlaybackCycle();
            isSwitchingSong = true;
            currentSongIndex = newIndex;
            playingSongIndex = newIndex;

            if (stemZoneManager != null)
            {
                stemZoneManager.SetPlaybackMode(StemZonePlaybackMode.Muted);
                stemZoneManager.StopAllSources();
            }

            ClearLoadedAudioClips();
            foreach (var handle in loadedHandles.Values)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
            loadedHandles.Clear();

            await LoadSong(currentSongIndex);

            isSwitchingSong = false;
            LastLoadedSongIndex = currentSongIndex;
            Song loaded = CurrentSong;
            OnSongLoaded?.Invoke(currentSongIndex);
            OnSongChanged?.Invoke(currentSongIndex, loaded);

            if (autoStartPlaybackCycle)
                StartPlaybackCycle();
        }

        async Task LoadSong(int index)
        {
            Song targetSong = songsDatabase.songs[index];
            TryOpenHapVideoForSong(targetSong);
            List<Task> loadTasks = new List<Task>();

            foreach (StemType stemType in AudioZones
                         .Where(zone => zone != null)
                         .Select(zone => zone.targetStem)
                         .Distinct())
            {
                AssetReferenceT<AudioClip> assetRef = GetStemReference(targetSong, stemType);
                if (assetRef == null || !assetRef.RuntimeKeyIsValid()) continue;

                var handle = assetRef.LoadAssetAsync<AudioClip>();
                loadedHandles[stemType] = handle;

                loadTasks.Add(handle.Task.ContinueWith(t =>
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                        AssignClipToZones(stemType, handle.Result);
                }, TaskScheduler.FromCurrentSynchronizationContext()));
            }

            await Task.WhenAll(loadTasks);
        }

        void AssignClipToZones(StemType stemType, AudioClip clip)
        {
            foreach (var zone in AudioZones)
            {
                if (zone == null || zone.Source == null || zone.targetStem != stemType) continue;
                zone.Source.clip = clip;
            }
        }

        void ClearLoadedAudioClips()
        {
            foreach (var zone in AudioZones)
            {
                if (zone == null || zone.Source == null) continue;
                zone.Source.clip = null;
            }
        }

        void StartPlaybackCycle()
        {
            StopPlaybackCycle();
            if (CurrentSong == null) return;

            bool isFirstCycle = !hasStartedPlaybackCycle;
            hasStartedPlaybackCycle = true;
            if (isFirstCycle && videoEffect != null)
                videoEffect.SetRatio(0f);
            if (isFirstCycle && textQuad != null)
                textQuad.SetDepthInteractionRatio(0f);

            playbackCycleCoroutine = StartCoroutine(PlaybackCycleRoutine(currentSongIndex, isFirstCycle));
        }

        void StopPlaybackCycle()
        {
            if (playbackCycleCoroutine == null) return;

            StopCoroutine(playbackCycleCoroutine);
            playbackCycleCoroutine = null;
        }

        IEnumerator PlaybackCycleRoutine(int songIndex, bool skipTransitionVideoFadeOut)
        {
            yield return RunPlaybackSegment(
                SongPlaybackMode.Transition,
                GetStemClipLengthOrFallback(StemType.Origin),
                skipTransitionVideoFadeOut);
            yield return RunPlaybackSegment(SongPlaybackMode.Interaction, GetStemClipLengthOrFallback(StemType.Vocal));

            playbackCycleCoroutine = null;
            _ = ChangeSong(GetNextSongIndex(songIndex));
        }

        IEnumerator RunPlaybackSegment(SongPlaybackMode mode, float duration, bool skipVideoFadeOut = false)
        {
            EnterPlaybackMode(mode);

            float videoTarget = mode == SongPlaybackMode.Transition ? 0f : 1f;
            float textTarget = mode == SongPlaybackMode.Transition ? 1f : 0f;
            float depthInteractionTarget = mode == SongPlaybackMode.Transition ? 0f : 1f;
            float videoStart = mode == SongPlaybackMode.Transition && !skipVideoFadeOut ? 1f : 0f;
            float textStart = mode == SongPlaybackMode.Transition ? 0f : 1f;
            float depthInteractionStart = mode == SongPlaybackMode.Transition && !skipVideoFadeOut ? 1f : 0f;
            float fadeDuration = VisualFadeDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float fadeT = Mathf.Clamp01(elapsed / fadeDuration);
                ApplyVisualPlaybackState(
                    Mathf.Lerp(videoStart, videoTarget, fadeT),
                    Mathf.Lerp(textStart, textTarget, fadeT),
                    Mathf.Lerp(depthInteractionStart, depthInteractionTarget, fadeT));

                elapsed += Time.deltaTime;
                yield return null;
            }

            ApplyVisualPlaybackState(videoTarget, textTarget, depthInteractionTarget);
        }

        void EnterPlaybackMode(SongPlaybackMode mode)
        {
            CurrentPlaybackMode = mode;
            OnPlaybackModeChanged?.Invoke(mode);

            if (stemZoneManager == null) return;

            if (mode == SongPlaybackMode.Transition)
            {
                stemZoneManager.SetPlaybackMode(StemZonePlaybackMode.OriginOnly);
                stemZoneManager.PlayOnlyStem(StemType.Origin);
            }
            else
            {
                stemZoneManager.SetPlaybackMode(StemZonePlaybackMode.NonOriginOnly);
                stemZoneManager.PlayAllExceptStem(StemType.Origin);
            }
        }

        void ApplyVisualPlaybackState(float videoRatio, float textTintBlend, float depthInteractionRatio)
        {
            if (videoEffect != null)
                videoEffect.SetRatio(videoRatio);
            if (textQuad != null)
            {
                textQuad.SetHdrTintBlend(textTintBlend);
                textQuad.SetDepthInteractionRatio(depthInteractionRatio);
            }
        }

        void ResolveSceneReferences()
        {
            if (videoEffect == null)
                videoEffect = FindFirstObjectByType<VideoEffect>();
            if (textQuad == null)
                textQuad = FindFirstObjectByType<TextQuad>();
        }

        float GetStemClipLengthOrFallback(StemType stemType)
        {
            AudioClip clip = GetClipForStem(stemType);
            if (clip != null && clip.length > 0f)
                return clip.length;

            Debug.LogWarning($"{nameof(SongManager)}: Missing {stemType} clip length for song {currentSongIndex}; using visual fade duration.", this);
            return VisualFadeDuration;
        }

        AudioClip GetClipForStem(StemType stemType)
        {
            foreach (var zone in AudioZones)
            {
                if (zone == null || zone.Source == null || zone.targetStem != stemType) continue;
                if (zone.Source.clip != null)
                    return zone.Source.clip;
            }

            return null;
        }

        int GetNextSongIndex(int songIndex)
        {
            if (songsDatabase == null || songsDatabase.songs == null || songsDatabase.songs.Count == 0)
                return 0;

            return (songIndex + 1) % songsDatabase.songs.Count;
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
            StopPlaybackCycle();
            if (stemZoneManager != null)
                stemZoneManager.SetPlaybackMode(StemZonePlaybackMode.Muted);

            foreach (var handle in loadedHandles.Values)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
            loadedHandles.Clear();
        }
    }
}
