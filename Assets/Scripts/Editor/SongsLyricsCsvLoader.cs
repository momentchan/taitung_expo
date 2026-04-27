using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// CSV lyrics import for Songs assets. See Songs.lyricsCsvFolder and lyricsFileNamePattern.
static class SongsLyricsCsvLoader
{
    [MenuItem("Taitung/Load all lyrics from CSV (selected or default asset)")]
    public static void LoadAllFromMenu()
    {
        if (!TryGetTargetSongsForImport(out var songs, out var err))
        {
            Debug.LogError(err);
            return;
        }
        LoadLyricsFromFolderIntoSongs(songs, songs.lyricsCsvFolder, songs.lyricsFileNamePattern);
    }

    [MenuItem("Assets/Load/reload lyrics for selected Songs (CSV)", false, 2010)]
    public static void LoadFromSelectedSongsAsset()
    {
        if (!(Selection.activeObject is Songs songs))
        {
            Debug.LogError("Select a Songs .asset in the Project window first.");
            return;
        }
        if (!TryResolveSongsAsset(songs, out var e))
        {
            Debug.LogError(e);
            return;
        }
        LoadLyricsFromFolderIntoSongs(songs, songs.lyricsCsvFolder, songs.lyricsFileNamePattern);
    }

    [MenuItem("Assets/Load/reload lyrics for selected Songs (CSV)", true)]
    public static bool LoadFromSelectedSongsAssetValidate()
    {
        return Selection.activeObject is Songs;
    }

    [MenuItem("CONTEXT/Songs/Load/reload lyrics from configured CSV folder")]
    static void ContextLoadConfigured(MenuCommand command)
    {
        var songs = (Songs)command.context;
        if (!TryResolveSongsAsset(songs, out var e))
        {
            Debug.LogError(e);
            return;
        }
        LoadLyricsFromFolderIntoSongs(songs, songs.lyricsCsvFolder, songs.lyricsFileNamePattern);
    }

    [MenuItem("CONTEXT/Songs/Set lyrics folder from file picker, then import…")]
    static void ContextPickFolder(MenuCommand command)
    {
        var songs = (Songs)command.context;
        if (!TryResolveSongsAsset(songs, out var e))
        {
            Debug.LogError(e);
            return;
        }

        var startDir = GetAbsolutePathInAssets(songs.lyricsCsvFolder);
        if (startDir == null || !Directory.Exists(startDir))
            startDir = Application.dataPath;

        var abs = EditorUtility.OpenFolderPanel("Choose folder containing SongN.csv", startDir, "");
        if (string.IsNullOrEmpty(abs))
            return;

        if (!TryConvertAbsolutePathToAssetFolder(abs, out var assetFolder, out var pathErr))
        {
            Debug.LogError(pathErr);
            return;
        }

        songs.lyricsCsvFolder = assetFolder;
        EditorUtility.SetDirty(songs);
        LoadLyricsFromFolderIntoSongs(songs, songs.lyricsCsvFolder, songs.lyricsFileNamePattern);
    }

    static void LoadLyricsFromFolderIntoSongs(Songs songs, string assetFolder, string filePattern)
    {
        if (songs == null)
        {
            Debug.LogError("Songs is null.");
            return;
        }

        if (string.IsNullOrWhiteSpace(filePattern))
            filePattern = "Song*.csv";

        if (!TryGetAbsolutePathForAssetFolder(assetFolder, out var dataDir, out var assetFolderNorm, out var folderErr))
        {
            Debug.LogError(folderErr);
            return;
        }

        if (!Directory.Exists(dataDir))
        {
            Debug.LogError($"Data folder not found: {assetFolderNorm}");
            return;
        }

        var paths = Directory.GetFiles(dataDir, filePattern, SearchOption.TopDirectoryOnly);
        if (paths.Length == 0)
        {
            Debug.LogWarning($"No files matching \"{filePattern}\" in {assetFolderNorm}.");
            return;
        }

        Array.Sort(paths, StringComparer.Ordinal);

        Undo.RecordObject(songs, "Load lyrics from CSV");
        if (songs.songs == null)
            songs.songs = new List<Song>();

        var loaded = 0;
        foreach (var fullPath in paths)
        {
            var fileName = Path.GetFileNameWithoutExtension(fullPath);
            if (!Enum.TryParse(fileName, ignoreCase: false, out SongType songType))
            {
                Debug.LogWarning($"Lyrics CSV: skip '{fileName}.csv' — not a valid {nameof(SongType)} name.");
                continue;
            }

            var lines = ReadLyricLinesFromFile(fullPath);
            var song = FindOrCreateSongInList(songs.songs, songType);
            song.lyrics = lines;
            loaded++;
            Debug.Log($"{songType}: applied {lines.Count} lyric line(s) from {assetFolderNorm}/{Path.GetFileName(fullPath)}.");
        }

        EditorUtility.SetDirty(songs);
        AssetDatabase.SaveAssets();
        Debug.Log($"Lyrics CSV: finished, updated {loaded} song(s) from {assetFolderNorm} (pattern: {filePattern}).");
    }

    static bool TryGetTargetSongsForImport(out Songs songs, out string error)
    {
        foreach (var o in Selection.objects)
        {
            if (o is Songs s)
            {
                songs = s;
                error = null;
                return true;
            }
        }

        songs = AssetDatabase.LoadAssetAtPath<Songs>(Songs.DefaultSongsAssetPath);
        if (songs == null)
        {
            error = $"No {nameof(Songs)} in selection and default asset is missing: {Songs.DefaultSongsAssetPath}. Create one or select a {nameof(Songs)} asset, then run the command again.";
            return false;
        }

        error = null;
        return true;
    }

    static bool TryResolveSongsAsset(Songs self, out string error)
    {
        var path = AssetDatabase.GetAssetPath(self);
        if (string.IsNullOrEmpty(path))
        {
            error = "This Songs instance is not saved to disk. Save the asset as a .asset in the project first.";
            return false;
        }
        error = null;
        return true;
    }

    static bool TryGetAbsolutePathForAssetFolder(string rawAssetFolder, out string absolute, out string normalizedAsset, out string error)
    {
        absolute = null;
        normalizedAsset = null;
        error = null;

        if (string.IsNullOrWhiteSpace(rawAssetFolder))
        {
            error = "Lyrics folder path is empty. Set it on the Songs asset (e.g. Assets/Data).";
            return false;
        }

        var s = rawAssetFolder.Trim().Replace('\\', '/');
        if (s == "Assets" || s == "assets")
        {
            normalizedAsset = "Assets";
        }
        else if (!s.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
        {
            s = "Assets/" + s.TrimStart('/');
            normalizedAsset = s;
        }
        else
        {
            normalizedAsset = "Assets" + s.Substring(6);
        }

        if (string.Equals(normalizedAsset, "Assets", StringComparison.Ordinal))
        {
            absolute = Path.GetFullPath(Application.dataPath);
        }
        else
        {
            var relativeFromAssets = normalizedAsset.Length > 7 ? normalizedAsset.Substring(7) : "";
            relativeFromAssets = relativeFromAssets.Replace('/', Path.DirectorySeparatorChar);
            absolute = Path.GetFullPath(Path.Combine(Application.dataPath, relativeFromAssets));
        }

        return true;
    }

    static string GetAbsolutePathInAssets(string rawAssetFolder)
    {
        if (TryGetAbsolutePathForAssetFolder(rawAssetFolder, out var abs, out _, out _))
            return abs;
        return null;
    }

    static bool TryConvertAbsolutePathToAssetFolder(string absolute, out string assetFolder, out string error)
    {
        assetFolder = null;
        error = null;
        var dataPath = Path.GetFullPath(Application.dataPath);
        var a = Path.GetFullPath(absolute);
        if (!a.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            error = "Pick a folder inside this project’s Assets tree.";
            return false;
        }

        var sub = a.Length > dataPath.Length
            ? a.Substring(dataPath.Length).TrimStart(Path.DirectorySeparatorChar, '/')
            : "";
        sub = sub.Replace(Path.DirectorySeparatorChar, '/');
        assetFolder = string.IsNullOrEmpty(sub) ? "Assets" : "Assets/" + sub;
        return true;
    }

    static List<string> ReadLyricLinesFromFile(string fullPath)
    {
        var raw = File.ReadAllText(fullPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false));
        var lines = new List<string>();
        using (var reader = new StringReader(raw))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var t = line.Trim();
                if (t.Length > 0)
                    lines.Add(t);
            }
        }
        return lines;
    }

    static Song FindOrCreateSongInList(List<Song> list, SongType type)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].type == type)
                return list[i];
        }

        var s = new Song { type = type, lyrics = new List<string>() };
        list.Add(s);
        return s;
    }
}
