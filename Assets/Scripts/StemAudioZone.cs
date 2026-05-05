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

        // Exposed for debug info
        public float CurrentVolume { get; private set; }

        void Awake()
        {
            // Initialize AudioSource parameters
            Source = GetComponent<AudioSource>();
            Source.playOnAwake = false;
            Source.loop = true;
            Source.volume = 0f;
        }

        public void UpdateVolume(TrackerData[] trackers, float maxDepth, float volumeSmoothing)
        {
            float target = 0f;

            // If the label is All, target volume is always 1
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

            // Smooth volume transition
            float t = Mathf.Clamp01(volumeSmoothing * Time.deltaTime * 10f);
            CurrentVolume = Mathf.Lerp(CurrentVolume, target, t);
            Source.volume = CurrentVolume;
        }

        public bool ContainsNormalizedPosition(Vector2 pos)
        {
            // Hardcode quadrants based on normalized coordinates [0.0, 1.0]
            switch (positionLabel)
            {
                case VolumePosition.BottomLeft:  return pos.x >= 0f && pos.x <= 0.5f && pos.y >= 0f && pos.y <= 0.5f;
                case VolumePosition.BottomRight: return pos.x > 0.5f && pos.x <= 1.0f && pos.y >= 0f && pos.y <= 0.5f;
                case VolumePosition.TopLeft:     return pos.x >= 0f && pos.x <= 0.5f && pos.y > 0.5f && pos.y <= 1.0f;
                case VolumePosition.TopRight:    return pos.x > 0.5f && pos.x <= 1.0f && pos.y > 0.5f && pos.y <= 1.0f;
                case VolumePosition.All:         return true; // Always active for any tracker position
                default: return false;
            }
        }
    }
}