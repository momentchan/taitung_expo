using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// One 3D TextMeshPro ring per lyric line from the song matching <see cref="SongType"/>.
/// Loads ring objects when the song or lyric data changes; radius, size, and group position/scale update in the inspector without recreating children.
/// </summary>
[ExecuteAlways]
public class ConcentricLyricRings : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] Songs songDatabase;
    [SerializeField] SongType songType = SongType.Song1;

    [Header("Rings")]
    [Tooltip("Radius of the outermost ring (last lyric in the list; larger index).")]
    [SerializeField] [Min(0.001f)] float outerRadius = 480f;
    [Tooltip("Radius of the innermost ring (first lyric in the list; index 0). Equal radial steps between inner and outer.")]
    [SerializeField] [Min(0.001f)] float innerRadius = 40f;
    [Tooltip("Same point size on every ring.")]
    [SerializeField] [Min(1f)] [FormerlySerializedAs("outerFontSize")]
    float ringFontSize = 36f;
    [SerializeField] Color textColor = Color.white;

    [Header("Group layout")]
    [Tooltip("Local position of the LyricRings child (all rings are parented under it).")]
    [SerializeField] Vector3 groupLocalPosition;
    [Tooltip("Local scale of the LyricRings child.")]
    [SerializeField] Vector3 groupLocalScale = Vector3.one;

    [Header("Optional")]
    [SerializeField] TMP_FontAsset fontOverride;

    const string ContainerName = "LyricRings";
    const int MaxPads = 8000; // Used as a safety cap now

    Transform _ringContainer;
    Object _lastDatabase;
    SongType _lastSongType;
    List<string> _lyricsSnapshot = new List<string>();
    int _ringCount;

    bool _editorSyncScheduled;

    void OnEnable()
    {
        SyncRings();
    }

    void OnValidate()
    {
        innerRadius = Mathf.Min(innerRadius, outerRadius);
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            SyncRings();
            return;
        }
        if (_editorSyncScheduled) return;
        _editorSyncScheduled = true;
        EditorApplication.delayCall += RunEditorDeferredSyncRings;
#else
        SyncRings();
#endif
    }

#if UNITY_EDITOR
    void RunEditorDeferredSyncRings()
    {
        _editorSyncScheduled = false;
        if (this == null) return;
        SyncRings();
    }
#endif

    [ContextMenu("Force rebuild rings")]
    public void ForceFullRebuild()
    {
        _lastDatabase = null;
        _lyricsSnapshot.Clear();
        _ringCount = 0;
        SyncRings();
    }

    void SyncRings()
    {
        if (songDatabase == null || songDatabase.songs == null) return;

        if (!TryGetSongByType(out var song) || song.lyrics == null)
        {
            return;
        }

        var lyrics = song.lyrics;
        EnsureContainer();
        ApplyGroupTransform();

        bool structural = NeedsStructuralRebuild(lyrics);
        if (structural)
        {
            while (_ringContainer.childCount > lyrics.Count)
            {
                var last = _ringContainer.GetChild(_ringContainer.childCount - 1).gameObject;
                if (Application.isPlaying) Destroy(last);
                else DestroyImmediate(last);
            }
            for (int i = 0; i < lyrics.Count; i++)
            {
                if (i >= _ringContainer.childCount)
                {
                    CreateRingObject(i);
                }
            }
            CacheSongState(lyrics);
        }

        for (int i = 0; i < lyrics.Count; i++)
        {
            var child = _ringContainer.GetChild(i);
            UpdateRingContent(child, i, lyrics);
        }
    }

    bool TryGetSongByType(out Song song)
    {
        song = null;
        for (int i = 0; i < songDatabase.songs.Count; i++)
        {
            if (songDatabase.songs[i].type == songType)
            {
                song = songDatabase.songs[i];
                return true;
            }
        }
        Debug.LogWarning("ConcentricLyricRings: No song with the selected SongType was found in the database.");
        return false;
    }

    bool NeedsStructuralRebuild(IReadOnlyList<string> lyrics)
    {
        if (_lastDatabase != songDatabase || _lastSongType != songType)
        {
            return true;
        }
        if (lyrics.Count != _ringCount)
        {
            return true;
        }
        for (int i = 0; i < lyrics.Count; i++)
        {
            if (i >= _lyricsSnapshot.Count) return true;
            string a = lyrics[i] ?? string.Empty;
            string b = _lyricsSnapshot[i] ?? string.Empty;
            if (a != b) return true;
        }
        return false;
    }

    void CacheSongState(IReadOnlyList<string> lyrics)
    {
        _lastDatabase = songDatabase;
        _lastSongType = songType;
        _ringCount = lyrics.Count;
        _lyricsSnapshot = new List<string>(lyrics.Count);
        for (int i = 0; i < lyrics.Count; i++)
        {
            _lyricsSnapshot.Add(lyrics[i] == null ? string.Empty : string.Copy(lyrics[i]));
        }
    }

    void ApplyGroupTransform()
    {
        if (_ringContainer == null) return;
        _ringContainer.localPosition = groupLocalPosition;
        _ringContainer.localScale = groupLocalScale;
    }

    void EnsureContainer()
    {
        if (_ringContainer == null)
        {
            var existing = transform.Find(ContainerName);
            if (existing != null) _ringContainer = existing;
        }
        if (_ringContainer == null)
        {
            var go = new GameObject(ContainerName);
            go.transform.SetParent(transform, false);
            _ringContainer = go.transform;
        }
    }

    void CreateRingObject(int index)
    {
        var go = new GameObject($"Ring_{index:00}", typeof(RectTransform));
        go.transform.SetParent(_ringContainer, false);
        go.AddComponent<TextMeshPro>();
        go.AddComponent<CircularTextMeshPro>();
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(10000f, 400f);
    }

    /// <summary>Equal radial step: lower lyric index (smaller id) is inner; last line is outer.</summary>
    static float RingRadiusAtIndex(int index, int lineCount, float outer, float inner)
    {
        if (lineCount <= 1) return 0.5f * (outer + inner);
        float step = (outer - inner) / (lineCount - 1);
        return inner + index * step;
    }

    void UpdateRingContent(Transform ringTransform, int index, IReadOnlyList<string> lyrics)
    {
        int n = lyrics.Count;
        float ringRadius = RingRadiusAtIndex(index, n, outerRadius, innerRadius);

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

        string line = lyrics[index];
        string phrase = ProcessLine(line);
        float minLayoutWidth = 2f * Mathf.PI * Mathf.Abs(ringRadius) * 0.995f;
        
        // Optimized padding method
        string padded = BuildBlankPaddedToWidth(tmp, phrase, minLayoutWidth);
        tmp.text = padded;
        
        // Let the TMP and CircularTextMeshPro handle the mesh update naturally
        tmp.havePropertiesChanged = true;
    }

    string ProcessLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return "·";
        string t = line.Trim();
            return t;
    }

    // Optimized: Calculate needed padding mathematically instead of brute-force loop.
    static string BuildBlankPaddedToWidth(TMP_Text tmp, string phrase, float minWidth)
    {
        if (string.IsNullOrEmpty(phrase)) phrase = " ";
        phrase = phrase.Trim();
        if (string.IsNullOrEmpty(phrase)) phrase = " ";

        const char pad = '\u00A0'; // NBSP

        // Get the width of the base phrase
        float phraseWidth = tmp.GetPreferredValues(phrase).x;
        if (phraseWidth >= minWidth) return phrase;

        // Get the width of a single padding character
        float padWidth = tmp.GetPreferredValues(pad.ToString()).x;
        
        // Failsafe to prevent division by zero or infinite allocations
        if (padWidth <= 0.001f) return phrase; 

        // Calculate how many pads are needed
        int padsNeeded = Mathf.CeilToInt((minWidth - phraseWidth) / padWidth);
        
        // Cap it to prevent massive memory allocations just in case
        padsNeeded = Mathf.Min(padsNeeded, MaxPads);

        // Construct the final string directly
        return phrase + new string(pad, padsNeeded);
    }
}