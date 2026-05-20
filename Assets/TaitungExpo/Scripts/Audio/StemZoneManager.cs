using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TaitungExpo
{
    /// <summary>
    /// Manages <see cref="StemAudioZone"/> volumes from <see cref="TrackerManager"/> each frame.
    /// Owns the zone list used by <see cref="SongManager"/> for stem playback.
    /// </summary>
    public class StemZoneManager : MonoBehaviour
    {
        [SerializeField] List<StemAudioZone> zones = new List<StemAudioZone>();

        [Header("Tracking")]
        [SerializeField] float maxDepth = 2f;
        [SerializeField] float volumeSmoothing = 0.5f;

        [Header("Debug")]
        [SerializeField] bool showDebugUI = true;

        Vector2 _debugUiScroll;

        public IReadOnlyList<StemAudioZone> Zones => zones;

        void Update() => Tick();

        public void Tick()
        {
            if (zones == null || zones.Count == 0) return;

            var manager = TrackerManager.Instance;
            if (manager == null || manager.ActiveTrackers == null) return;

            var activeTrackers = manager.ActiveTrackers;
            foreach (var zone in zones)
            {
                if (zone == null) continue;

                var trackersInVolume = activeTrackers
                    .Where(t => zone.ContainsNormalizedPosition(t.pos))
                    .ToArray();

                zone.UpdateVolume(trackersInVolume, maxDepth, volumeSmoothing);
            }
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
