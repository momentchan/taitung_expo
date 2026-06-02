using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using TMPro;
using TaitungExpo;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor preview and offline bake for circular lyric rings. Does not build rings during Play Mode —
/// use <see cref="LyricRingsPrefabApplier"/> with baked prefabs at runtime.
/// </summary>
[ExecuteAlways]
public class SongLyricRingView : MonoBehaviour
{
    const string RingsRootObjectName = "LyricRings";
    const int MaxNbspPaddingCount = 8000;
    static int s_uiLayer = -2;
    /// <summary>Returned by <see cref="GetPlaybackListIndexIfAny"/> when rings are driven by <see cref="previewSongType"/> instead of the manager.</summary>
    const int NoManagedPlaybackIndex = int.MinValue;

    [Header("Song source")]
    [SerializeField] Songs songDatabase;
    [SerializeField] [FormerlySerializedAs("songType")]
    SongType previewSongType = SongType.Song1;
    [SerializeField] SongManager songManager;

    [Header("Ring geometry")]
    [SerializeField] [Min(0.001f)] float outerRadius = 480f;
    [SerializeField] [Min(0.001f)] float innerRadius = 40f;
    [SerializeField] [Min(1f)] [FormerlySerializedAs("outerFontSize")]
    float ringFontSize = 36f;
    [SerializeField] Color textColor = Color.white;

    [Header("Rings root transform")]
    [SerializeField] [FormerlySerializedAs("groupLocalPosition")]
    Vector3 ringsRootLocalPosition;
    [SerializeField] [FormerlySerializedAs("groupLocalScale")]
    Vector3 ringsRootLocalScale = Vector3.one;

    [Header("Typography")]
    [SerializeField] TMP_FontAsset fontOverride;

    Transform _ringsRoot;
    Object _lastSongDatabase;
    SongType _lastPreviewSongType;
    int _cachedPlaybackListIndex = NoManagedPlaybackIndex;
    List<string> _lyricsTextSnapshot = new List<string>();
    int _ringObjectCount;

    bool _editorSyncScheduled;

    bool ShouldRunEditorRingPreview =>
        !Application.isPlaying && isActiveAndEnabled && gameObject.activeInHierarchy;

    void OnEnable()
    {
        if (Application.isPlaying)
            return;
        if (!ShouldRunEditorRingPreview)
            return;

        BindSongManager();
        SyncPreviewSongTypeFromManager();
        RefreshRingsFromSong();
    }

    void OnDisable()
    {
        UnbindSongManager();
#if UNITY_EDITOR
        EditorApplication.delayCall -= RunEditorDeferredRefresh;
        _editorSyncScheduled = false;
#endif
    }

    void BindSongManager()
    {
        if (songManager == null) return;
        songManager.OnSongChanged -= OnSongManagerChanged;
        songManager.OnSongChanged += OnSongManagerChanged;
    }

    void UnbindSongManager()
    {
        if (songManager == null) return;
        songManager.OnSongChanged -= OnSongManagerChanged;
    }

    void OnSongManagerChanged(int _, Song song)
    {
        if (!ShouldRunEditorRingPreview)
            return;

        ApplyPreviewSongTypeFromSong(song);
        InvalidateLyricCache();
        RefreshRingsFromSong();
    }

    /// <summary>Called by <see cref="SongManager"/> after each successful song load.</summary>
    public void SyncToSongManager(SongManager manager)
    {
        if (manager == null) return;
        if (songManager != manager)
        {
            UnbindSongManager();
            songManager = manager;
            BindSongManager();
        }
        if (manager.SongsDatabase != null)
            songDatabase = manager.SongsDatabase;
        if (!ShouldRunEditorRingPreview)
            return;

        ApplyPreviewSongTypeFromSong(manager.CurrentSong);
        InvalidateLyricCache();
        RefreshRingsFromSong();
    }

    void SyncPreviewSongTypeFromManager()
    {
        if (!Application.isPlaying || songManager == null) return;
        ApplyPreviewSongTypeFromSong(songManager.CurrentSong);
    }

    void ApplyPreviewSongTypeFromSong(Song song)
    {
        if (song == null) return;
        previewSongType = song.type;
    }

    void InvalidateLyricCache()
    {
        _lastSongDatabase = null;
        _cachedPlaybackListIndex = NoManagedPlaybackIndex;
        _lyricsTextSnapshot.Clear();
        _ringObjectCount = 0;
    }

    Songs ActiveSongDatabase =>
        songManager != null && songManager.SongsDatabase != null
            ? songManager.SongsDatabase
            : songDatabase;

    void OnValidate()
    {
        innerRadius = Mathf.Min(innerRadius, outerRadius);
        if (Application.isPlaying)
            return;
        if (!ShouldRunEditorRingPreview)
            return;

#if UNITY_EDITOR
        if (_editorSyncScheduled) return;
        _editorSyncScheduled = true;
        EditorApplication.delayCall += RunEditorDeferredRefresh;
#else
        RefreshRingsFromSong();
#endif
    }

#if UNITY_EDITOR
    void RunEditorDeferredRefresh()
    {
        _editorSyncScheduled = false;
        if (this == null) return;
        if (!ShouldRunEditorRingPreview)
            return;

        RefreshRingsFromSong();
    }
#endif

    [ContextMenu("Force rebuild lyric rings")]
    public void ForceFullRebuild()
    {
        if (Application.isPlaying)
            return;
        if (!ShouldRunEditorRingPreview)
            return;

        _lastSongDatabase = null;
        _cachedPlaybackListIndex = NoManagedPlaybackIndex;
        _lyricsTextSnapshot.Clear();
        _ringObjectCount = 0;
        RefreshRingsFromSong();
    }

    /// <summary>
    /// Builds curved lyric rings under <c>LyricRings</c>, bakes mesh geometry, and tags with <see cref="LyricRingsPrefabMarker"/>.
    /// Uses <see cref="previewSongType"/> and <see cref="ActiveSongDatabase"/> in the Editor.
    /// </summary>
    [ContextMenu("Generate offline lyric rings (prefab-ready)")]
    public void GenerateOfflineLyricRings()
    {
        if (!GenerateOfflineLyricRings(previewSongType))
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            SaveRingsRootAsPrefab(previewSongType);
#endif
    }

    public bool GenerateOfflineLyricRings(SongType songType)
    {
        previewSongType = songType;

        if (!TryResolveSongForLyrics(out var song) || song.lyrics == null || song.lyrics.Count == 0)
        {
            Debug.LogError($"{nameof(SongLyricRingView)}: Cannot bake offline rings — no lyrics for {songType}.", this);
            return false;
        }

        InvalidateLyricCache();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            RebuildAllRingsImmediate(song.lyrics);
        else
#endif
            RefreshRingsFromSong();

        EnsureRingsRootExists();
        ApplyRingsRootTransform();

        if (_ringsRoot.childCount == 0)
        {
            Debug.LogError($"{nameof(SongLyricRingView)}: Bake produced no ring objects for {songType}.", this);
            return false;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
            ForceEditorTextMeshUpdate();
#endif

        for (int i = 0; i < _ringsRoot.childCount; i++)
        {
            var circular = _ringsRoot.GetChild(i).GetComponent<CircularTextMeshPro>();
            if (circular != null)
                circular.RefreshCurve();
        }

        var marker = _ringsRoot.GetComponent<LyricRingsPrefabMarker>();
        if (marker == null)
            marker = _ringsRoot.gameObject.AddComponent<LyricRingsPrefabMarker>();
        marker.SetSongType(song.type);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        EditorUtility.SetDirty(_ringsRoot.gameObject);
#endif

        Debug.Log($"{nameof(SongLyricRingView)}: Baked {song.lyrics.Count} offline rings for {song.type} on '{_ringsRoot.name}'.", this);
        return true;
    }

    /// <summary>Returns the <c>LyricRings</c> transform after <see cref="EnsureRingsRootExists"/>.</summary>
    public Transform GetRingsRootTransform()
    {
        EnsureRingsRootExists();
        return _ringsRoot;
    }

    void RebuildAllRingsImmediate(IReadOnlyList<string> lyrics)
    {
        EnsureRingsRootExists();
        ApplyRingsRootTransform();

        while (_ringsRoot.childCount > lyrics.Count)
        {
            var last = _ringsRoot.GetChild(_ringsRoot.childCount - 1).gameObject;
            if (Application.isPlaying)
                Destroy(last);
            else
                DestroyImmediate(last);
        }

        for (int i = _ringsRoot.childCount; i < lyrics.Count; i++)
            CreateRingObjectForLine(i);

        for (int i = 0; i < lyrics.Count; i++)
            ApplyLyricLineToRing(_ringsRoot.GetChild(i), i, lyrics);

        StoreLyricsSnapshot(lyrics);
    }

#if UNITY_EDITOR
    void ForceEditorTextMeshUpdate()
    {
        Canvas.ForceUpdateCanvases();
        for (int i = 0; i < _ringsRoot.childCount; i++)
        {
            var tmp = _ringsRoot.GetChild(i).GetComponent<TextMeshPro>();
            if (tmp != null)
                tmp.ForceMeshUpdate(true);
        }
    }
#endif

    /// <summary>Removes all ring children so the next bake starts clean.</summary>
    public void ClearLyricRingsChildren()
    {
        EnsureRingsRootExists();
        InvalidateLyricCache();

        for (int i = _ringsRoot.childCount - 1; i >= 0; i--)
        {
            var child = _ringsRoot.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    /// <summary>Rebuilds ring GameObjects if lyrics changed; always updates TMP text and layout. Editor only.</summary>
    void RefreshRingsFromSong()
    {
        if (Application.isPlaying)
            return;
        if (!ShouldRunEditorRingPreview)
            return;

        var db = ActiveSongDatabase;
        if (db == null || db.songs == null) return;

        if (!TryResolveSongForLyrics(out var song) || song.lyrics == null)
            return;

        IReadOnlyList<string> lyrics = song.lyrics;
        EnsureRingsRootExists();
        ApplyRingsRootTransform();

        if (ShouldRebuildRingObjects(lyrics))
        {
            while (_ringsRoot.childCount > lyrics.Count)
            {
                var last = _ringsRoot.GetChild(_ringsRoot.childCount - 1).gameObject;
                if (Application.isPlaying) Destroy(last);
                else DestroyImmediate(last);
            }
            for (int i = 0; i < lyrics.Count; i++)
            {
                if (i >= _ringsRoot.childCount)
                    CreateRingObjectForLine(i);
            }
            StoreLyricsSnapshot(lyrics);
        }

        for (int i = 0; i < lyrics.Count; i++)
        {
            var ringTransform = _ringsRoot.GetChild(i);
            ApplyLyricLineToRing(ringTransform, i, lyrics);
        }
    }

    bool TryResolveSongForLyrics(out Song song)
    {
        song = null;
        int listIdx = GetPlaybackListIndexIfAny();
        var db = ActiveSongDatabase;
        if (listIdx != NoManagedPlaybackIndex)
        {
            song = db.songs[listIdx];
            return song != null;
        }

        return TryFindSongByPreviewType(out song);
    }

    int GetPlaybackListIndexIfAny()
    {
        var db = ActiveSongDatabase;
        if (!Application.isPlaying || songManager == null || db == null || db.songs == null)
            return NoManagedPlaybackIndex;

        int idx = songManager.LastLoadedSongIndex;
        if (idx >= 0 && idx < db.songs.Count)
            return idx;

        return NoManagedPlaybackIndex;
    }

    bool TryFindSongByPreviewType(out Song song)
    {
        song = null;
        var db = ActiveSongDatabase;
        if (db == null || db.songs == null) return false;
        for (int i = 0; i < db.songs.Count; i++)
        {
            if (db.songs[i].type == previewSongType)
            {
                song = db.songs[i];
                return true;
            }
        }
        Debug.LogWarning($"{nameof(SongLyricRingView)}: No song with {nameof(previewSongType)} \"{previewSongType}\" in the database.");
        return false;
    }

    bool ShouldRebuildRingObjects(IReadOnlyList<string> lyrics)
    {
        int playbackListIdx = GetPlaybackListIndexIfAny();
        var db = ActiveSongDatabase;
        if (_lastSongDatabase != db || _lastPreviewSongType != previewSongType || playbackListIdx != _cachedPlaybackListIndex)
            return true;
        if (lyrics.Count != _ringObjectCount)
            return true;
        for (int i = 0; i < lyrics.Count; i++)
        {
            if (i >= _lyricsTextSnapshot.Count) return true;
            string a = lyrics[i] ?? string.Empty;
            string b = _lyricsTextSnapshot[i] ?? string.Empty;
            if (a != b) return true;
        }
        return false;
    }

    void StoreLyricsSnapshot(IReadOnlyList<string> lyrics)
    {
        _lastSongDatabase = ActiveSongDatabase;
        _lastPreviewSongType = previewSongType;
        _cachedPlaybackListIndex = GetPlaybackListIndexIfAny();
        _ringObjectCount = lyrics.Count;
        _lyricsTextSnapshot = new List<string>(lyrics.Count);
        for (int i = 0; i < lyrics.Count; i++)
            _lyricsTextSnapshot.Add(lyrics[i] == null ? string.Empty : string.Copy(lyrics[i]));
    }

    void ApplyRingsRootTransform()
    {
        if (_ringsRoot == null) return;
        _ringsRoot.localPosition = ringsRootLocalPosition;
        _ringsRoot.localScale = ringsRootLocalScale;
    }

    void EnsureRingsRootExists()
    {
        if (_ringsRoot == null)
        {
            var existing = transform.Find(RingsRootObjectName);
            if (existing != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    if (ShouldReplaceBrokenRingsRoot(existing.gameObject))
                    {
                        DestroyImmediate(existing.gameObject);
                        existing = null;
                    }
                    else if (PrefabUtility.IsPartOfPrefabInstance(existing.gameObject))
                    {
                        var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(existing.gameObject);
                        if (instanceRoot != null)
                        {
                            PrefabUtility.UnpackPrefabInstance(
                                instanceRoot,
                                PrefabUnpackMode.Completely,
                                InteractionMode.AutomatedAction);
                        }
                    }
                }
#endif
                if (existing != null)
                    _ringsRoot = existing;
            }
        }

        if (_ringsRoot == null)
        {
            var go = new GameObject(RingsRootObjectName);
            go.transform.SetParent(transform, false);
            SetLayerToUi(go);
            _ringsRoot = go.transform;
        }
    }

#if UNITY_EDITOR
    static bool ShouldReplaceBrokenRingsRoot(GameObject ringsRootGo)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(ringsRootGo))
            return false;

        if (PrefabUtility.IsPrefabAssetMissing(ringsRootGo))
            return true;

        return PrefabUtility.GetPrefabInstanceStatus(ringsRootGo) == PrefabInstanceStatus.MissingAsset;
    }
#endif

    void CreateRingObjectForLine(int lineIndex)
    {
        var go = new GameObject($"LyricRing_{lineIndex:00}", typeof(RectTransform));
        go.transform.SetParent(_ringsRoot, false);
        SetLayerToUi(go);
        go.AddComponent<TextMeshPro>();
        go.AddComponent<CircularTextMeshPro>();
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(10000f, 400f);
    }

    static void SetLayerToUi(GameObject go)
    {
        int layer = UiLayer;
        if (layer < 0) return;
        go.layer = layer;
    }

    static int UiLayer
    {
        get
        {
            if (s_uiLayer == -2)
                s_uiLayer = LayerMask.NameToLayer("UI");
            return s_uiLayer;
        }
    }

    /// <summary>Even spacing: line 0 at inner radius, last line at outer radius.</summary>
    static float RadiusForLyricLine(int lineIndex, int lineCount, float outer, float inner)
    {
        if (lineCount <= 1) return 0.5f * (outer + inner);
        float step = (outer - inner) / (lineCount - 1);
        return inner + lineIndex * step;
    }

    void ApplyLyricLineToRing(Transform ringTransform, int lineIndex, IReadOnlyList<string> lyrics)
    {
        int n = lyrics.Count;
        float ringRadius = RadiusForLyricLine(lineIndex, n, outerRadius, innerRadius);

        var go = ringTransform.gameObject;
        var tmp = go.GetComponent<TextMeshPro>();
        var circular = go.GetComponent<CircularTextMeshPro>();
        if (tmp == null || circular == null) return;

        if (fontOverride != null) tmp.font = fontOverride;
        tmp.enableAutoSizing = false;
        tmp.fontSize = ringFontSize;
        tmp.color = textColor;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.alignment = TextAlignmentOptions.Left;

        circular.radius = ringRadius;
        circular.angleOffset = 0f;

        string line = lyrics[lineIndex];
        string phrase = NormalizeLyricLineForDisplay(line);
        float circumference = 2f * Mathf.PI * Mathf.Abs(ringRadius) * 0.995f;
        string paddedForRing = PadPhraseToMinWidth(tmp, phrase, circumference);
        tmp.text = paddedForRing;
        tmp.havePropertiesChanged = true;
    }

    static string NormalizeLyricLineForDisplay(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return "·";
        return line.Trim();
    }

    /// <summary>NBSP padding so the curved line fills the ring circumference.</summary>
    static string PadPhraseToMinWidth(TMP_Text tmp, string phrase, float minWidth)
    {
        if (string.IsNullOrEmpty(phrase)) phrase = " ";
        phrase = phrase.Trim();
        if (string.IsNullOrEmpty(phrase)) phrase = " ";

        const char pad = '\u00A0';

        float phraseWidth = tmp.GetPreferredValues(phrase).x;
        if (phraseWidth >= minWidth) return phrase;

        float padWidth = tmp.GetPreferredValues(pad.ToString()).x;
        if (padWidth <= 0.001f) return phrase;

        int padsNeeded = Mathf.CeilToInt((minWidth - phraseWidth) / padWidth);
        padsNeeded = Mathf.Min(padsNeeded, MaxNbspPaddingCount);
        return phrase + new string(pad, padsNeeded);
    }

#if UNITY_EDITOR
    public static string GetLyricRingsPrefabPath(SongType songType) =>
        LyricRingsPrefabPaths.GetPrefabAssetPath(songType);

    public void SaveRingsRootAsPrefab(SongType songType)
    {
        EnsureRingsRootExists();
        if (_ringsRoot == null || _ringsRoot.childCount == 0)
        {
            Debug.LogError($"{nameof(SongLyricRingView)}: Nothing to save for {songType}.", this);
            return;
        }

        EnsureLyricRingsPrefabFolderExists();
        string assetPath = GetLyricRingsPrefabPath(songType);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);

        GameObject copy = Instantiate(_ringsRoot.gameObject);
        copy.name = _ringsRoot.name;
        copy.transform.SetParent(null, false);
        UnpackPrefabHierarchyForSave(copy);

        try
        {
            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(copy, assetPath, out bool success);
            if (!success || prefabAsset == null)
            {
                Debug.LogError($"{nameof(SongLyricRingView)}: Failed to save prefab at {assetPath}.", this);
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(SongLyricRingView)}: Saved prefab {assetPath}", prefabAsset);
        }
        finally
        {
            DestroyImmediate(copy);
        }
    }

    static void EnsureLyricRingsPrefabFolderExists()
    {
        if (AssetDatabase.IsValidFolder(LyricRingsPrefabPaths.PrefabFolder))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/TaitungExpo"))
            AssetDatabase.CreateFolder("Assets", "TaitungExpo");

        AssetDatabase.CreateFolder("Assets/TaitungExpo", "Prefabs");
    }

    static void UnpackPrefabHierarchyForSave(GameObject root)
    {
        if (PrefabUtility.IsPartOfAnyPrefab(root))
        {
            PrefabUtility.UnpackPrefabInstance(
                PrefabUtility.GetOutermostPrefabInstanceRoot(root),
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
        }
    }
#endif
}
