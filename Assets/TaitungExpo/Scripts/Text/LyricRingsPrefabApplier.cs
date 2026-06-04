using System.Collections.Generic;
using mj.gist;
using PrefsGUI;
using PrefsGUI.RapidGUI;
using TMPro;
using UnityEngine;
using TaitungExpo;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// At runtime, instantiates the baked lyric-rings prefab for the active song from <see cref="SongManager"/>.
/// Prefab references are auto-collected from <c>Assets/TaitungExpo/Prefabs</c> in the Editor.
/// </summary>
public class LyricRingsPrefabApplier : MonoBehaviour, IGUIUser
{
    #region IGUIUser

    public string GetName() => "LyricRings";

    public void ShowGUI()
    {
        EnsurePlaybackPrefs();
        rotationSpeed.DoGUISlider(0f, 180f, "Ring Rotation Speed");
        rotationAccelerationPrefs.DoGUISlider(0f, 180f, "Ring Rotation Acceleration");
    }

    public void SetupGUI()
    {
        EnsurePlaybackPrefs();
    }

    #endregion

    const string PrefabFolder = "Assets/TaitungExpo/Prefabs";
    const string PrefabNamePrefix = "LyricRings_";

    [Header("Source")]
    [SerializeField] GameObject[] prefabs;

    [Header("Playback Animation")]
    [SerializeField] bool animatePlaybackMode = true;
    [SerializeField] bool firstRingClockwise = true;

    static SongManager Manager => SongManager.Instance;

    readonly Dictionary<SongType, GameObject> _lookup = new Dictionary<SongType, GameObject>();
    GameObject _activeInstance;
    LyricRingsPlaybackAnimator _playbackAnimator;
    PrefsFloat rotationSpeed;
    PrefsFloat rotationAccelerationPrefs;

    void Awake()
    {
        SetupGUI();
        RebuildLookup();
    }

    void OnEnable()
    {   
        SetupGUI();
        BindSongManager();
        ApplyForCurrentSong();
    }

    void OnDisable()
    {
        UnbindSongManager();
        ClearActiveInstance();
    }

    void Update()
    {
        _playbackAnimator?.Tick(Time.deltaTime, CurrentMaxRotationSpeed, CurrentRotationAcceleration);
    }

    void BindSongManager()
    {
        if (Manager == null) return;
        Manager.OnSongChanged -= OnSongChanged;
        Manager.OnSongChanged += OnSongChanged;
    }

    void UnbindSongManager()
    {
        if (Manager == null) return;
        Manager.OnSongChanged -= OnSongChanged;
    }

    void OnSongChanged(int songIndex, Song song)
    {
        if (song != null)
            ApplyForSongType(song.type);
        else
            ApplyForSongIndex(songIndex);
    }

    /// <summary>Swaps lyric rings to match <see cref="SongManager"/> current song.</summary>
    [ContextMenu("Apply for current song")]
    public void ApplyForCurrentSong()
    {
        if (Manager == null)
        {
            ClearActiveInstance();
            return;
        }

        if (Manager.CurrentSong != null)
            ApplyForSongType(Manager.CurrentSongType);
        else
            ApplyForSongIndex(Manager.ActiveSongIndex);
    }

    public void ApplyForSongIndex(int songIndex)
    {
        if (Manager == null || Manager.SongsDatabase == null
            || Manager.SongsDatabase.songs == null
            || songIndex < 0
            || songIndex >= Manager.SongsDatabase.songs.Count)
        {
            ClearActiveInstance();
            return;
        }

        ApplyForSongType(Manager.SongsDatabase.songs[songIndex].type);
    }

    public void ApplyForSongType(SongType songType)
    {
        if (!_lookup.TryGetValue(songType, out GameObject prefab) || prefab == null)
        {
            RebuildLookup();
            if (!_lookup.TryGetValue(songType, out prefab) || prefab == null)
            {
                Debug.LogWarning($"{nameof(LyricRingsPrefabApplier)}: No baked prefab for {songType}.", this);
                ClearActiveInstance();
                return;
            }
        }

        ClearActiveInstance();

        _activeInstance = Instantiate(prefab, transform);
        _activeInstance.name = prefab.name;
        var instanceTransform = _activeInstance.transform;
        instanceTransform.localPosition = Vector3.zero;
        instanceTransform.localRotation = Quaternion.identity;
        instanceTransform.localScale = Vector3.one;
        _activeInstance.SetActive(true);

        ConfigurePlaybackAnimator(songType);
    }

    void ConfigurePlaybackAnimator(SongType songType)
    {
        if (!animatePlaybackMode || _activeInstance == null)
            return;

        var marker = _activeInstance.GetComponent<LyricRingsPrefabMarker>();
        if (marker == null)
            return;

        float fadeDuration = Manager != null ? Manager.VisualFadeDuration : 2f;
        _playbackAnimator?.Dispose();
        _playbackAnimator = new LyricRingsPlaybackAnimator(
            marker,
            Manager,
            fadeDuration,
            firstRingClockwise,
            GetInteractionVisibleLyricRings(songType));
    }

    void EnsurePlaybackPrefs()
    {
        rotationSpeed ??= new PrefsFloat($"{GetName()}_rotationSpeed", 2f);
        rotationAccelerationPrefs ??= new PrefsFloat($"{GetName()}_rotationAcceleration", 2.5f);
    }

    float CurrentMaxRotationSpeed
    {
        get
        {
            EnsurePlaybackPrefs();
            return Mathf.Max(0f, rotationSpeed != null ? rotationSpeed.Get() : 18f);
        }
    }

    float CurrentRotationAcceleration
    {
        get
        {
            EnsurePlaybackPrefs();
            return Mathf.Max(0f, rotationAccelerationPrefs != null ? rotationAccelerationPrefs.Get() : 6f);
        }
    }

    int[] GetInteractionVisibleLyricRings(SongType songType)
    {
        Song song = GetSongForType(songType);
        return song != null ? song.interactionVisibleLyricRings : null;
    }

    static Song GetSongForType(SongType songType)
    {
        if (Manager == null || Manager.SongsDatabase == null || Manager.SongsDatabase.songs == null)
            return null;

        foreach (Song song in Manager.SongsDatabase.songs)
        {
            if (song != null && song.type == songType)
                return song;
        }

        return null;
    }

    void RebuildLookup()
    {
        _lookup.Clear();
        if (prefabs == null) return;

        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null) continue;

            if (!TryGetSongType(prefab, out SongType songType))
                continue;

            _lookup[songType] = prefab;
        }
    }

    static bool TryGetSongType(GameObject prefab, out SongType songType)
    {
        songType = default;
        if (prefab == null) return false;

        var marker = prefab.GetComponent<LyricRingsPrefabMarker>();
        if (marker != null)
        {
            songType = marker.SongType;
            return true;
        }

        if (!prefab.name.StartsWith(PrefabNamePrefix))
            return false;

        string suffix = prefab.name.Substring(PrefabNamePrefix.Length);
        return System.Enum.TryParse(suffix, out songType);
    }

    void ClearActiveInstance()
    {
        _playbackAnimator?.Dispose();
        _playbackAnimator = null;

        if (_activeInstance == null) return;

        if (Application.isPlaying)
            Destroy(_activeInstance);
        else
            DestroyImmediate(_activeInstance);

        _activeInstance = null;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        CollectPrefabsFromFolder();
        RebuildLookup();
    }

    [ContextMenu("Collect prefabs from Prefabs folder")]
    public void CollectPrefabsFromFolder()
    {
        string[] guids = AssetDatabase.FindAssets("t:GameObject LyricRings_", new[] { PrefabFolder });
        var list = new List<GameObject>(guids.Length);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                list.Add(prefab);
        }

        prefabs = list.ToArray();
        EditorUtility.SetDirty(this);
    }
#endif
}

class LyricRingsPlaybackAnimator
{
    static readonly int[] DefaultInteractionVisibleLyricRings = { 1, 3 };

    readonly SongManager _songManager;
    readonly float _textFadeDuration;
    readonly bool _firstRingClockwise;
    readonly HashSet<int> _interactionVisibleRingIndices = new HashSet<int>();
    TextFadeState[] _lyricStates = System.Array.Empty<TextFadeState>();
    TextFadeState[] _songNameStates = System.Array.Empty<TextFadeState>();
    Transform[] _rotatingRings = System.Array.Empty<Transform>();
    SongPlaybackMode _mode = SongPlaybackMode.Transition;
    float _currentRotationSpeed;

    public LyricRingsPlaybackAnimator(
        LyricRingsPrefabMarker marker,
        SongManager manager,
        float fadeDuration,
        bool firstRingClockwise,
        int[] interactionVisibleLyricRings)
    {
        _songManager = manager;
        _textFadeDuration = Mathf.Max(0.001f, fadeDuration);
        _firstRingClockwise = firstRingClockwise;

        RefreshReferences(marker, interactionVisibleLyricRings);
        if (_songManager != null)
            _songManager.OnPlaybackModeChanged += OnPlaybackModeChanged;

        ApplyMode(manager != null ? manager.CurrentPlaybackMode : SongPlaybackMode.Transition, true);
    }

    public void Dispose()
    {
        if (_songManager != null)
            _songManager.OnPlaybackModeChanged -= OnPlaybackModeChanged;
    }

    public void Tick(float deltaTime, float maxRotationSpeed, float rotationAcceleration)
    {
        UpdateTextFades(deltaTime);
        UpdateRingRotation(deltaTime, maxRotationSpeed, rotationAcceleration);
    }

    void OnPlaybackModeChanged(SongPlaybackMode mode)
    {
        ApplyMode(mode, false);
    }

    void RefreshReferences(LyricRingsPrefabMarker marker, int[] interactionVisibleLyricRings)
    {
        if (marker == null)
            return;

        _lyricStates = BuildStates(marker.LyricTexts);
        _songNameStates = BuildStates(marker.SongNameTexts);
        BuildInteractionVisibleRingSet(marker.LyricTexts, interactionVisibleLyricRings);
    }

    void ApplyMode(SongPlaybackMode mode, bool immediate)
    {
        _mode = mode;

        bool interaction = mode == SongPlaybackMode.Interaction;
        SetSongNameTargets(interaction ? 0f : 1f);
        SetLyricTargets(interaction);

        if (mode == SongPlaybackMode.Transition)
            _currentRotationSpeed = 0f;

        if (immediate)
            ApplyTargetsImmediate();
    }

    void SetSongNameTargets(float targetAlpha)
    {
        for (int i = 0; i < _songNameStates.Length; i++)
            _songNameStates[i].TargetAlpha = targetAlpha;
    }

    void SetLyricTargets(bool interaction)
    {
        for (int i = 0; i < _lyricStates.Length; i++)
        {
            bool keepVisible = _interactionVisibleRingIndices.Contains(i);
            _lyricStates[i].TargetAlpha = interaction && !keepVisible ? 0f : 1f;
        }
    }

    void ApplyTargetsImmediate()
    {
        for (int i = 0; i < _lyricStates.Length; i++)
            _lyricStates[i].ApplyNormalizedAlpha(_lyricStates[i].TargetAlpha);
        for (int i = 0; i < _songNameStates.Length; i++)
            _songNameStates[i].ApplyNormalizedAlpha(_songNameStates[i].TargetAlpha);
    }

    void UpdateTextFades(float deltaTime)
    {
        float step = deltaTime / _textFadeDuration;

        for (int i = 0; i < _lyricStates.Length; i++)
            _lyricStates[i].MoveTowardTarget(step);
        for (int i = 0; i < _songNameStates.Length; i++)
            _songNameStates[i].MoveTowardTarget(step);
    }

    void UpdateRingRotation(float deltaTime, float maxRotationSpeed, float rotationAcceleration)
    {
        if (_mode != SongPlaybackMode.Interaction)
            return;

        maxRotationSpeed = Mathf.Max(0f, maxRotationSpeed);
        rotationAcceleration = Mathf.Max(0f, rotationAcceleration);
        _currentRotationSpeed = Mathf.MoveTowards(
            _currentRotationSpeed,
            maxRotationSpeed,
            rotationAcceleration * deltaTime);

        if (_currentRotationSpeed <= 0f)
            return;

        float baseDirection = _firstRingClockwise ? -1f : 1f;
        float deltaAngle = _currentRotationSpeed * deltaTime;
        for (int i = 0; i < _rotatingRings.Length; i++)
        {
            Transform ring = _rotatingRings[i];
            if (ring == null)
                continue;

            float direction = i % 2 == 0 ? baseDirection : -baseDirection;
            ring.Rotate(0f, 0f, direction * deltaAngle, Space.Self);
        }
    }

    void BuildInteractionVisibleRingSet(TMP_Text[] lyricTexts, int[] oneBasedRingNumbers)
    {
        _interactionVisibleRingIndices.Clear();

        int[] ringNumbers = oneBasedRingNumbers != null && oneBasedRingNumbers.Length > 0
            ? oneBasedRingNumbers
            : DefaultInteractionVisibleLyricRings;

        var rotatingRings = new List<Transform>();
        for (int i = 0; i < ringNumbers.Length; i++)
        {
            int zeroBasedIndex = ringNumbers[i] - 1;
            if (lyricTexts == null || zeroBasedIndex < 0 || zeroBasedIndex >= lyricTexts.Length)
                continue;
            if (!_interactionVisibleRingIndices.Add(zeroBasedIndex))
                continue;

            Transform ring = GetRingTransform(lyricTexts, zeroBasedIndex);
            if (ring != null)
                rotatingRings.Add(ring);
        }

        _rotatingRings = rotatingRings.ToArray();
    }

    static TextFadeState[] BuildStates(TMP_Text[] texts)
    {
        if (texts == null || texts.Length == 0)
            return System.Array.Empty<TextFadeState>();

        var states = new TextFadeState[texts.Length];
        for (int i = 0; i < texts.Length; i++)
            states[i] = new TextFadeState(texts[i]);
        return states;
    }

    static Transform GetRingTransform(TMP_Text[] texts, int index)
    {
        if (texts == null || index < 0 || index >= texts.Length || texts[index] == null)
            return null;
        return texts[index].transform;
    }

    struct TextFadeState
    {
        readonly TMP_Text _text;
        readonly Color _baseColor;
        float _normalizedAlpha;

        public float TargetAlpha;

        public TextFadeState(TMP_Text text)
        {
            _text = text;
            _baseColor = text != null ? text.color : Color.white;
            _normalizedAlpha = 1f;
            TargetAlpha = 1f;
        }

        public void MoveTowardTarget(float step)
        {
            ApplyNormalizedAlpha(Mathf.MoveTowards(_normalizedAlpha, TargetAlpha, step));
        }

        public void ApplyNormalizedAlpha(float alpha)
        {
            _normalizedAlpha = Mathf.Clamp01(alpha);
            if (_text == null)
                return;

            Color color = _baseColor;
            color.a *= _normalizedAlpha;
            _text.color = color;
        }
    }
}
