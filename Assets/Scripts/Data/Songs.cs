using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
[Serializable]
public class Song
{
    public SongType type;

    public AssetReferenceT<AudioClip> origin;
    public AssetReferenceT<AudioClip> vocal;
    public AssetReferenceT<AudioClip> chord;
    public AssetReferenceT<AudioClip> bass;
    public AssetReferenceT<AudioClip> hidrums;
    public AssetReferenceT<AudioClip> lowdrums;

    // Filming / material video filename from CSV column C only (zero-based field index 2).
    public string videoFileName;

    public List<string> lyrics;
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
