using System.Collections.Generic;
using System.Linq;
using mj.gist;
using PrefsGUI;
using PrefsGUI.RapidGUI;
using UnityEngine;

namespace TaitungExpo
{
    public enum StemZonePlaybackMode
    {
        Tracking,
        OriginOnly,
        NonOriginOnly,
        Muted
    }

    /// <summary>
    /// Manages <see cref="StemAudioZone"/> volumes from <see cref="TrackerManager"/> each frame.
    /// Owns per-zone volume gain prefs and the zone list used by <see cref="SongManager"/>.
    /// </summary>
    public class StemZoneManager : MonoBehaviour, IGUIUser
    {
        #region IGUIUser

        public string GetName() => "StemZones";

        public void ShowGUI()
        {
            EnsureVolumeGainPrefs();
            if (volumeGains == null) return;

            foreach (var zone in zones)
            {
                if (zone == null) continue;
                if (!volumeGains.TryGetValue(zone, out var gain) || gain == null)
                    continue;

                string label = $"{zone.targetStem} / {zone.positionLabel}";
                gain.DoGUISlider(0f, 2f, label);
            }
        }

        public void SetupGUI()
        {
            if (volumeGains != null)
                return;

            EnsureVolumeGainPrefs();
        }

        #endregion

        [SerializeField] List<StemAudioZone> zones = new List<StemAudioZone>();

        [Header("Tracking")]
        [SerializeField] float maxDepth = 2f;
        [SerializeField] float volumeSmoothing = 0.5f;

        [Header("Debug")]
        [SerializeField] bool showDebugUI = true;

        StemZonePlaybackMode playbackMode = StemZonePlaybackMode.Tracking;
        Dictionary<StemAudioZone, PrefsFloat> volumeGains;
        Vector2 _debugUiScroll;

        public IReadOnlyList<StemAudioZone> Zones => zones;
        public StemZonePlaybackMode PlaybackMode => playbackMode;

        void Awake()
        {
            SetupGUI();
        }

        void Update() => Tick();

        public void Tick()
        {
            if (zones == null || zones.Count == 0) return;

            var manager = TrackerManager.Instance;
            var activeTrackers = manager != null ? manager.ActiveTrackers : null;

            EnsureVolumeGainPrefs();
            foreach (var zone in zones)
            {
                if (zone == null) continue;

                if (TryGetForcedVolume(zone, out float forcedVolume))
                {
                    SetZoneVolumeImmediate(zone, forcedVolume);
                    continue;
                }

                var trackersInVolume = activeTrackers == null
                    ? null
                    : activeTrackers
                        .Where(t => zone.ContainsNormalizedPosition(t.pos))
                        .ToArray();

                zone.UpdateVolume(trackersInVolume, maxDepth, volumeSmoothing, GetVolumeGain(zone));
            }
        }

        public void SetPlaybackMode(StemZonePlaybackMode mode)
        {
            playbackMode = mode;
            ApplyImmediatePlaybackState();
        }

        public void StopAllSources()
        {
            if (zones == null) return;

            foreach (var zone in zones)
            {
                if (zone == null || zone.Source == null) continue;
                zone.Source.Stop();
                SetZoneVolumeImmediate(zone, 0f);
            }
        }

        public void PlayOnlyStem(StemType stem)
        {
            if (zones == null) return;

            foreach (var zone in zones)
            {
                if (zone == null || zone.Source == null) continue;

                bool shouldPlay = zone.targetStem == stem && zone.Source.clip != null;
                RestartSource(zone.Source, shouldPlay);
                SetZoneVolumeImmediate(zone, shouldPlay ? 1f : 0f);
            }
        }

        public void PlayAllExceptStem(StemType stem)
        {
            if (zones == null) return;

            foreach (var zone in zones)
            {
                if (zone == null || zone.Source == null) continue;

                bool shouldPlay = zone.targetStem != stem && zone.Source.clip != null;
                RestartSource(zone.Source, shouldPlay);
                SetZoneVolumeImmediate(zone, shouldPlay ? zone.CurrentVolume : 0f);
            }
        }

        static void RestartSource(AudioSource source, bool shouldPlay)
        {
            if (source == null) return;

            if (!shouldPlay)
            {
                source.Stop();
                return;
            }

            source.time = 0f;
            source.Play();
        }

        bool TryGetForcedVolume(StemAudioZone zone, out float volume)
        {
            volume = 0f;
            if (zone == null)
                return true;

            switch (playbackMode)
            {
                case StemZonePlaybackMode.OriginOnly:
                    volume = zone.targetStem == StemType.Origin ? 1f : 0f;
                    return true;
                case StemZonePlaybackMode.NonOriginOnly:
                    if (zone.targetStem == StemType.Origin)
                    {
                        volume = 0f;
                        return true;
                    }
                    return false;
                case StemZonePlaybackMode.Muted:
                    volume = 0f;
                    return true;
                default:
                    return false;
            }
        }

        void ApplyImmediatePlaybackState()
        {
            if (zones == null) return;

            foreach (var zone in zones)
            {
                if (zone == null) continue;
                if (TryGetForcedVolume(zone, out float forcedVolume))
                    SetZoneVolumeImmediate(zone, forcedVolume);
            }
        }

        static void SetZoneVolumeImmediate(StemAudioZone zone, float volume)
        {
            if (zone == null || zone.Source == null) return;
            zone.SetVolumeImmediate(volume);
        }

        void EnsureVolumeGainPrefs()
        {
            if (zones == null) return;

            volumeGains ??= new Dictionary<StemAudioZone, PrefsFloat>();

            foreach (var zone in zones)
            {
                if (zone == null || volumeGains.ContainsKey(zone))
                    continue;

                volumeGains[zone] = new PrefsFloat(zone.VolumeGainPrefsKey, 1f);
            }
        }

        float GetVolumeGain(StemAudioZone zone)
        {
            if (volumeGains != null && volumeGains.TryGetValue(zone, out var gain) && gain != null)
                return gain;
            return 1f;
        }

        void OnGUI()
        {
            if (!showDebugUI || zones == null) return;

            var richLabel = new GUIStyle(GUI.skin.label) { richText = true };

            const float panelWidth = 300f;
            float panelHeight = Mathf.Clamp(Screen.height - 80f, 160f, 480f);

            GUI.backgroundColor = new Color(0, 0, 0, 0.7f);
            GUILayout.BeginArea(new Rect(10, 70f, panelWidth, panelHeight), GUI.skin.box);
            _debugUiScroll = GUILayout.BeginScrollView(_debugUiScroll);
            GUILayout.Label("<b>STEM ZONE VOLUMES</b>", richLabel);
            GUILayout.Space(5);

            foreach (var zone in zones)
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
    }
}
