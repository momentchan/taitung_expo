using System;
using System.Linq;
using System.Runtime.InteropServices;
using Osc;
using PrefsGUI;
using PrefsGUI.RapidGUI;
using UnityEngine;
using mj.gist;

namespace TaitungExpo
{
    public class TrackerManager : SingletonMonoBehaviour<TrackerManager>, IGUIUser
    {
        #region IGUIUser

        public string GetName() => "Tracker";

        public void ShowGUI()
        {
            depthRangeMin.DoGUISlider(0f, 1f, "Depth Range Min");
            depthRangeMax.DoGUISlider(0f, 1f, "Depth Range Max");
        }

        public void SetupGUI()
        {
            if (depthRangeMin != null)
                return;

            depthRangeMin = new PrefsFloat($"{GetName()}_depthRangeMin", 0f);
            depthRangeMax = new PrefsFloat($"{GetName()}_depthRangeMax", 1f);
        }

        #endregion

        private PrefsFloat depthRangeMin;
        private PrefsFloat depthRangeMax;
        [SerializeField] private int trackerNum = 20;
        [SerializeField] private float distanceThreshold = 0.1f;
        [SerializeField] private bool mouseUpdate = true;

        [Header("Mouse depth (LMB)")]
        [SerializeField] private float mouseDepthPressSpeed = 8f;
        [SerializeField] private float mouseDepthReleaseSpeed = 3f;

        private float mouseSmoothedDepth;

        public GraphicsBuffer TrackerBuffer { get; private set; }
        public TrackerData[] Trackers { get; private set; }
        public TrackerData[] ActiveTrackers => Trackers.Where(t => t.active == 1).ToArray();

        public TrackerData MouseData => Trackers[mouseId];
        public int TotalTrackerNum => trackerNum + 1; // plus : mouse
        public bool MouseUpdate
        {
            get
            {
                return mouseUpdate;
            }
            set
            {
                mouseUpdate = value;
            }
        }

        private int mouseId => TotalTrackerNum - 1;

        private static readonly float RANDOM_SMOOTH_FACTOR = 0.5f;
        private static readonly float ALIVE_DURATION = 0.1f;
        private static readonly float FADE_DURATION = 1f;

        public void OnReceivePoint(OscPort.Capsule c)
        {
            try
            {
                var msg = c.message;
                var pos = new Vector2(Mathf.Clamp01((float)msg.data[0]), Mathf.Clamp01((float)msg.data[1]));
                var depthRaw = (float)msg.data[2];
                UpdateTrackerData(pos, RemapDepth(depthRaw));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        float RemapDepth(float raw)
        {
            float min = depthRangeMin != null ? depthRangeMin : 0f;
            float max = depthRangeMax != null ? depthRangeMax : 1f;
            float span = Mathf.Max(max - min, 1e-4f);
            return Mathf.Clamp01((raw - min) / span);
        }

        protected override void Awake()
        {
            base.Awake();
            SetupGUI();
            Trackers = new TrackerData[TotalTrackerNum];
            TrackerBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, TotalTrackerNum, Marshal.SizeOf(typeof(TrackerData)));
        }

        private void Update()
        {
            if (mouseUpdate)
                UpdateMouseTrackerData();

            UpdateTrackers();
        }
        private void UpdateMouseTrackerData()
        {
            var pos = new Vector2(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height);
            float depthTarget = Input.GetMouseButton(0) ? 1f : 0f;
            float depthSpeed = Input.GetMouseButton(0) ? mouseDepthPressSpeed : mouseDepthReleaseSpeed;
            mouseSmoothedDepth = Mathf.MoveTowards(mouseSmoothedDepth, depthTarget, depthSpeed * Time.deltaTime);

            var data = new TrackerData
            {
                active = 1,
                isMoving = 1,
                pos = pos,
                dis = pos - MouseData.pos,
                dir = (pos - MouseData.pos).normalized,
                lastUpdateTime = Time.time,
                activeRatio = 1,
                depth = mouseSmoothedDepth
            };
            Trackers[mouseId] = data;
        }

        private void UpdateTrackers()
        {
            for (var i = 0; i < Trackers.Length; i++)
            {
                var d = Trackers[i];
                if (d.lastUpdateTime < Time.time - ALIVE_DURATION)
                {
                    d.active = 0;
                    d.activeRatio = Mathf.Clamp01(d.activeRatio - Time.deltaTime / FADE_DURATION);
                    Trackers[i] = d;
                }
            }
            TrackerBuffer.SetData(Trackers);
        }

        private void UpdateTrackerData(Vector2 pos, float depth)
        {
            var actives = 0;
            var minId = -1;
            var minDist = 1e5;
            for (var i = 0; i < trackerNum; i++)
            {
                var tracker = Trackers[i];
                var dist = (pos - tracker.pos).magnitude;
                if (dist < minDist) { minId = i; minDist = dist; }
                if (tracker.active == 1) { actives = i; }
            }

            if (minDist < distanceThreshold)
            {
                var d = GetUpdatedTracker(minId, pos, depth);
                Trackers[minId] = d;
            }
            else
            {
                var newID = (actives + 1) % trackerNum;
                var d = GetUpdatedTracker(newID, pos, depth);
                Trackers[newID] = d;
            }
        }

        private TrackerData GetUpdatedTracker(int id, Vector2 pos, float depth)
        {
            var prevPos = Trackers[id].pos;
            var prevRatio = Trackers[id].activeRatio;

            TrackerData d;
            d.active = 1;
            d.pos = Vector2.Lerp(prevPos, pos, RANDOM_SMOOTH_FACTOR);
            d.dis = d.pos - prevPos;
            d.dir = d.dis.normalized;
            d.isMoving = 0;
            d.lastUpdateTime = Time.time;
            d.activeRatio = Mathf.Clamp(prevRatio + Time.deltaTime / FADE_DURATION, 0, 1);
            d.depth = depth;
            return d;
        }

        private void OnDestroy()
        {
            TrackerBuffer.Dispose();
        }
    }
}