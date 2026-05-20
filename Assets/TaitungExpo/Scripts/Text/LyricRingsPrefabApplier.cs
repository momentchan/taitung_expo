using System.Collections.Generic;
using UnityEngine;
using TaitungExpo;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// At runtime, instantiates the baked lyric-rings prefab for the active song from <see cref="SongManager"/>.
/// Prefab references are auto-collected from <c>Assets/TaitungExpo/Prefabs</c> in the Editor.
/// </summary>
public class LyricRingsPrefabApplier : MonoBehaviour
{
    const string PrefabFolder = "Assets/TaitungExpo/Prefabs";
    const string PrefabNamePrefix = "LyricRings_";

    [Header("Source")]
    [SerializeField] GameObject[] prefabs;

    static SongManager Manager => SongManager.Instance;

    readonly Dictionary<SongType, GameObject> _lookup = new Dictionary<SongType, GameObject>();
    GameObject _activeInstance;
    void Awake()
    {
        RebuildLookup();
    }

    void OnEnable()
    {   
        BindSongManager();
        ApplyForCurrentSong();
    }

    void OnDisable()
    {
        UnbindSongManager();
        ClearActiveInstance();
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
