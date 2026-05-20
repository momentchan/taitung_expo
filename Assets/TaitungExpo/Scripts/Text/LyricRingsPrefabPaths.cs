/// <summary>Asset paths for offline-baked lyric ring prefabs.</summary>
public static class LyricRingsPrefabPaths
{
    public const string PrefabFolder = "Assets/TaitungExpo/Prefabs";
    public const string PrefabNamePrefix = "LyricRings_";

    public static string GetPrefabAssetPath(SongType songType)
    {
        return $"{PrefabFolder}/{PrefabNamePrefix}{songType}.prefab";
    }
}
