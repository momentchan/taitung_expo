using UnityEngine;

/// <summary>
/// Tags baked <c>LyricRings</c> hierarchy with the source <see cref="SongType"/> for prefab lookup at runtime.
/// </summary>
public class LyricRingsPrefabMarker : MonoBehaviour
{
    [SerializeField] SongType songType;

    public SongType SongType => songType;

    public void SetSongType(SongType type)
    {
        songType = type;
    }
}
