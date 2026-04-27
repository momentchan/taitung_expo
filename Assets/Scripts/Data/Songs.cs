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

    [Header("Editor CSV import")]
    [Tooltip("Project path to the folder of lyric .csv files (e.g. Assets/Data or Assets/Lyrics/ShowA). Stems must match SongType: Song1, Song2, …")]
    public string lyricsCsvFolder = "Assets/Data";

    [Tooltip("File search pattern in that folder, e.g. Song*.csv or SetA_Song*.csv. Each matching file’s name (without .csv) must be a valid SongType.")]
    public string lyricsFileNamePattern = "Song*.csv";

    public const string DefaultSongsAssetPath = "Assets/Songs.asset";
}
