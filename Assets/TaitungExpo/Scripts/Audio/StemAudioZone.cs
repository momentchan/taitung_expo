using System.Linq;
using UnityEngine;

namespace TaitungExpo
{
    public enum StemType
    {
        Origin, Vocal, Chord, Bass, HiDrums, LoDrums
    }

    public enum VolumePosition
    {
        BottomLeft, BottomRight, TopLeft, TopRight, All
    }

    [RequireComponent(typeof(AudioSource))]
    public class StemAudioZone : MonoBehaviour
    {
        [Header("Zone Configuration")]
        public StemType targetStem;
        public VolumePosition positionLabel;

        public AudioSource Source { get; private set; }

        // Exposed for debug info (after gain is applied).
        public float CurrentVolume { get; private set; }

        /// <summary>Stable PrefsGUI key for per-zone volume gain (owned by <see cref="StemZoneManager"/>).</summary>
        public string VolumeGainPrefsKey => $"StemZone_{targetStem}_{positionLabel}_volumeGain";

        void Awake()
        {
            Source = GetComponent<AudioSource>();
            Source.playOnAwake = false;
            Source.loop = true;
            Source.volume = 0f;
        }

        public void UpdateVolume(TrackerData[] trackers, float maxDepth, float volumeSmoothing, float volumeGain)
        {
            float target = 0f;

            if (positionLabel == VolumePosition.All)
            {
                target = 1f;
            }
            else if (trackers != null && trackers.Length > 0)
            {
                float denom = Mathf.Max(0.0001f, maxDepth);
                float sum = trackers.Sum(t => t.depth);
                target = Mathf.Clamp01(sum / denom);
            }

            target *= Mathf.Max(0f, volumeGain);

            float t = Mathf.Clamp01(volumeSmoothing * Time.deltaTime * 10f);
            CurrentVolume = Mathf.Lerp(CurrentVolume, target, t);
            Source.volume = CurrentVolume;
        }

        public void SetVolumeImmediate(float volume)
        {
            CurrentVolume = Mathf.Max(0f, volume);
            if (Source != null)
                Source.volume = CurrentVolume;
        }

        public bool ContainsNormalizedPosition(Vector2 pos)
        {
            switch (positionLabel)
            {
                case VolumePosition.BottomLeft:  return pos.x >= 0f && pos.x <= 0.5f && pos.y >= 0f && pos.y <= 0.5f;
                case VolumePosition.BottomRight: return pos.x > 0.5f && pos.x <= 1.0f && pos.y >= 0f && pos.y <= 0.5f;
                case VolumePosition.TopLeft:     return pos.x >= 0f && pos.x <= 0.5f && pos.y > 0.5f && pos.y <= 1.0f;
                case VolumePosition.TopRight:    return pos.x > 0.5f && pos.x <= 1.0f && pos.y > 0.5f && pos.y <= 1.0f;
                case VolumePosition.All:         return true;
                default: return false;
            }
        }
    }
}
