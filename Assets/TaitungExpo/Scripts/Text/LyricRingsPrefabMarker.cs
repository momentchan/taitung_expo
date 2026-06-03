using UnityEngine;
using TMPro;

/// <summary>
/// Tags baked <c>LyricRings</c> hierarchy with source song metadata and generated text references.
/// </summary>
public class LyricRingsPrefabMarker : MonoBehaviour
{
    [SerializeField] SongType songType;
    [SerializeField] TMP_Text[] lyricTexts;
    [SerializeField] TMP_Text[] songNameTexts;

    public SongType SongType => songType;
    public TMP_Text[] LyricTexts => lyricTexts;
    public TMP_Text[] SongNameTexts => songNameTexts;

    public void SetSongType(SongType type)
    {
        songType = type;
    }

    public void SetGeneratedTextReferences(TMP_Text[] lyrics, TMP_Text[] songNames)
    {
        lyricTexts = lyrics ?? System.Array.Empty<TMP_Text>();
        songNameTexts = songNames ?? System.Array.Empty<TMP_Text>();
    }
}
