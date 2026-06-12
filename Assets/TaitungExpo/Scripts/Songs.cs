using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
[Serializable]
public class Song
{
    public SongType type;

    // Display title from CSV column B (e.g. "給情人的紀念品").
    public string songName;

    public AssetReferenceT<AudioClip> origin;
    public AssetReferenceT<AudioClip> vocal;
    public AssetReferenceT<AudioClip> chord;
    public AssetReferenceT<AudioClip> bass;
    public AssetReferenceT<AudioClip> hidrums;
    public AssetReferenceT<AudioClip> lowdrums;

    // Filming / material video filename from CSV column D (zero-based index 3).
    public string videoFileName;

    [TextArea(3, 20)]
    public string lyrics;

    // 1-based lyric ring numbers to keep visible during interaction mode.
    // Rings not listed here fade out. Default keeps ring 1 and ring 3.
    public int[] interactionVisibleLyricRings = { 1, 3 };

    public bool HasLyrics => !string.IsNullOrWhiteSpace(lyrics);

    /// <summary>Splits stored lyrics on line breaks for ring-per-line display.</summary>
    public string[] GetLyricLines()
    {
        if (string.IsNullOrWhiteSpace(lyrics))
            return Array.Empty<string>();

        var normalized = lyrics.Replace("\r\n", "\n").Replace('\r', '\n');
        var parts = normalized.Split('\n');
        var lines = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
                lines.Add(trimmed);
        }
        return lines.ToArray();
    }
}

public enum SongType
{
    Song1, Song2, Song3, Song4, Song5, Song6, Song7, Song8
}

[CreateAssetMenu(fileName = "Songs", menuName = "Songs")]
public class Songs : ScriptableObject
{
    public List<Song> songs;
}
