using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Song
{
    public SongType type;
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
