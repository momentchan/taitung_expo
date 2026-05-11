using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using TMPro;
using TaitungExpo;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Shows a song's lyrics as stacked circular TextMeshPro lines: first lyric on the inner ring, last on the outer ring.
/// In Play Mode you can assign <see cref="songManager"/> so rings follow the loaded track; otherwise pick a song with <see cref="previewSongType"/> (useful in the Editor).
/// </summary>
[ExecuteAlways]
public class SongLyricRingView : MonoBehaviour
{
    const string RingsRootObjectName = "LyricRings";
    const int MaxNbspPaddingCount = 8000;
    /// <summary>Returned by <see cref="GetPlaybackListIndexIfAny"/> when rings are driven by <see cref="previewSongType"/> instead of the manager.</summary>
    const int NoManagedPlaybackIndex = int.MinValue;

    [Header("Song source")]
    [SerializeField] Songs songDatabase;
    [Tooltip("Editor / fallback: which song to show when Song Manager is not driving playback.")]
    [SerializeField] [FormerlySerializedAs("songType")]
    SongType previewSongType = SongType.Song1;
    [Tooltip("When set, Play Mode uses the same song list index as this manager (lyrics match what is playing).")]
    [SerializeField] SongManager songManager;

    [Header("Ring geometry")]
    [Tooltip("Radius of the outer ring (last lyric line).")]
    [SerializeField] [Min(0.001f)] float outerRadius = 480f;
    [Tooltip("Radius of the inner ring (first lyric line). Spacing is even between inner and outer.")]
    [SerializeField] [Min(0.001f)] float innerRadius = 40f;
    [Tooltip("Font size for every ring.")]
    [SerializeField] [Min(1f)] [FormerlySerializedAs("outerFontSize")]
    float ringFontSize = 36f;
    [SerializeField] Color textColor = Color.white;

    [Header("Rings root transform")]
    [Tooltip("Local position of the child object that holds all ring objects.")]
    [SerializeField] [FormerlySerializedAs("groupLocalPosition")]
    Vector3 ringsRootLocalPosition;
    [Tooltip("Local scale of the child object that holds all ring objects.")]
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

    void OnEnable()
    {
        if (songManager != null)
            songManager.OnSongLoaded += OnSongManagerLoadedSong;
        RefreshRingsFromSong();
    }

    void OnDisable()
    {
        if (songManager != null)
            songManager.OnSongLoaded -= OnSongManagerLoadedSong;
    }

    void OnSongManagerLoadedSong(int _)
    {
        RefreshRingsFromSong();
    }

    void OnValidate()
    {
        innerRadius = Mathf.Min(innerRadius, outerRadius);
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            RefreshRingsFromSong();
            return;
        }
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
        RefreshRingsFromSong();
    }
#endif

    [ContextMenu("Force rebuild lyric rings")]
    public void ForceFullRebuild()
    {
        _lastSongDatabase = null;
        _cachedPlaybackListIndex = NoManagedPlaybackIndex;
        _lyricsTextSnapshot.Clear();
        _ringObjectCount = 0;
        RefreshRingsFromSong();
    }

    /// <summary>Rebuilds ring GameObjects if lyrics changed; always updates TMP text and layout.</summary>
    void RefreshRingsFromSong()
    {
        if (songDatabase == null || songDatabase.songs == null) return;

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
        if (listIdx != NoManagedPlaybackIndex)
        {
            song = songDatabase.songs[listIdx];
            return song != null;
        }

        return TryFindSongByPreviewType(out song);
    }

    int GetPlaybackListIndexIfAny()
    {
        if (!Application.isPlaying || songManager == null || songDatabase == null || songDatabase.songs == null)
            return NoManagedPlaybackIndex;

        int idx = songManager.LastLoadedSongIndex;
        if (idx >= 0 && idx < songDatabase.songs.Count)
            return idx;

        return NoManagedPlaybackIndex;
    }

    bool TryFindSongByPreviewType(out Song song)
    {
        song = null;
        for (int i = 0; i < songDatabase.songs.Count; i++)
        {
            if (songDatabase.songs[i].type == previewSongType)
            {
                song = songDatabase.songs[i];
                return true;
            }
        }
        Debug.LogWarning($"{nameof(SongLyricRingView)}: No song with {nameof(previewSongType)} \"{previewSongType}\" in the database.");
        return false;
    }

    bool ShouldRebuildRingObjects(IReadOnlyList<string> lyrics)
    {
        int playbackListIdx = GetPlaybackListIndexIfAny();
        if (_lastSongDatabase != songDatabase || _lastPreviewSongType != previewSongType || playbackListIdx != _cachedPlaybackListIndex)
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
        _lastSongDatabase = songDatabase;
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
            if (existing != null) _ringsRoot = existing;
        }
        if (_ringsRoot == null)
        {
            var go = new GameObject(RingsRootObjectName);
            go.transform.SetParent(transform, false);
            _ringsRoot = go.transform;
        }
    }

    void CreateRingObjectForLine(int lineIndex)
    {
        var go = new GameObject($"LyricRing_{lineIndex:00}", typeof(RectTransform));
        go.transform.SetParent(_ringsRoot, false);
        go.AddComponent<TextMeshPro>();
        go.AddComponent<CircularTextMeshPro>();
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(10000f, 400f);
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
}
