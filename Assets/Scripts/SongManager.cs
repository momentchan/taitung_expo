using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TaitungExpo
{
    public class SongManager : MonoBehaviour
    {
        [SerializeField] private float maxDepth = 2f;
        [SerializeField] private float volumeSmoothing = 0.5f;

        [SerializeField] private List<VolumeTracker> volumeTrackers;

        void Start()
        {

        }

        void Update()
        {

            foreach (var vt in volumeTrackers)
            {
                var trackersInVolume = TrackerManager.Instance.ActiveTrackers
                    .Where(t => vt.ContainsNormalizedPosition(t.pos))
                    .ToArray();
                vt.UpdateVolume(trackersInVolume, maxDepth, volumeSmoothing);
            }
        }
    }

    [System.Serializable]
    public class VolumeTracker
    {
        [SerializeField] private VolumePosition position;
        [SerializeField] private Vector2 xRange, yRange;
        [SerializeField] private float currentVolume;

        public void UpdateVolume(TrackerData[] trackers, float maxDepth, float volumeSmoothing)
        {
            float target;
            if (trackers == null || trackers.Length == 0)
            {
                target = 0f;
            }
            else
            {
                var denom = Mathf.Max(0.0001f, maxDepth);
                var sum = trackers.Sum(t => t.depth);
                target = Mathf.Clamp01(sum / denom);
            }

            float t = Mathf.Clamp01(volumeSmoothing);
            currentVolume = Mathf.Lerp(currentVolume, target, t);
        }

        public bool ContainsNormalizedPosition(Vector2 pos)
        {
            float xMin = Mathf.Min(xRange.x, xRange.y);
            float xMax = Mathf.Max(xRange.x, xRange.y);
            float yMin = Mathf.Min(yRange.x, yRange.y);
            float yMax = Mathf.Max(yRange.x, yRange.y);
            return pos.x >= xMin && pos.x <= xMax && pos.y >= yMin && pos.y <= yMax;
        }

    }

    public enum VolumePosition
    {
        BottomLeft, BottomRight, TopLeft, TopRight
    }
}