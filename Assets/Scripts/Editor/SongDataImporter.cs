#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.AddressableAssets;

namespace TaitungExpo
{
    public class SongDataImporter
    {
        [MenuItem("Tools/Parse Songs CSV")]
        public static void ParseAndLink()
        {
            // 1. Find the Songs ScriptableObject in the project
            string[] soGuids = AssetDatabase.FindAssets("t:Songs");
            if (soGuids.Length == 0)
            {
                Debug.LogError("Cannot find 'Songs' ScriptableObject.");
                return;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(soGuids[0]);
            Songs songsSO = AssetDatabase.LoadAssetAtPath<Songs>(assetPath);

            // 2. Open file dialog to select the CSV
            string csvPath = EditorUtility.OpenFilePanel("Select CSV File", Application.dataPath, "csv");
            if (string.IsNullOrEmpty(csvPath)) return;

            if (songsSO.songs == null) songsSO.songs = new List<Song>();
            songsSO.songs.Clear();

            // 3. Read and parse CSV
            string csvContent = File.ReadAllText(csvPath);
            List<string[]> rows = ParseCSV(csvContent);

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Length < 2) continue; // Skip invalid rows

                string songName = rows[i][0].Trim();
                string rawLyrics = rows[i][1].Trim();
                // Column C → index 2
                string videoName = rows[i].Length > 2 && rows[i][2] != null ? rows[i][2].Trim() : "";

                Song newSong = new Song();

                // Map row index to SongType Enum
                newSong.type = (SongType)i;

                // Parse lyrics by new lines
                newSong.lyrics = new List<string>(rawLyrics.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries));
                newSong.videoFileName = videoName;

                // 4. Auto-link Addressable stems by searching filenames
                newSong.origin = FindStemReference(songName, "示意原曲");
                newSong.vocal = FindStemReference(songName, "Vocal");
                newSong.chord = FindStemReference(songName, "Chords");
                newSong.bass = FindStemReference(songName, "Bass");
                newSong.hidrums = FindStemReference(songName, "hi-Drums");
                newSong.lowdrums = FindStemReference(songName, "lo-Drums");

                songsSO.songs.Add(newSong);
            }

            // 5. Save SO changes to disk
            EditorUtility.SetDirty(songsSO);
            AssetDatabase.SaveAssets();
            Debug.Log("CSV parsed and Addressable AudioClips linked successfully.");
        }

        private static AssetReferenceT<AudioClip> FindStemReference(string baseName, string suffix)
        {
            // Construct exact filename search query, e.g., "04 宴會歌舞 Vocal t:AudioClip"
            string searchQuery = $"{baseName} {suffix} t:AudioClip";
            string[] guids = AssetDatabase.FindAssets(searchQuery);

            if (guids.Length > 0)
            {
                return new AssetReferenceT<AudioClip>(guids[0]);
            }

            Debug.LogWarning($"Stem not found: {baseName} {suffix}");
            return new AssetReferenceT<AudioClip>("");
        }

        // Custom CSV parser to handle commas and newlines inside lyrics quotes
        private static List<string[]> ParseCSV(string content)
        {
            List<string[]> data = new List<string[]>();
            bool inQuotes = false;
            string currentField = "";
            List<string> currentRow = new List<string>();

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];

                if (c == '\"')
                {
                    if (inQuotes && i + 1 < content.Length && content[i + 1] == '\"')
                    {
                        currentField += '\"';
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    currentRow.Add(currentField);
                    currentField = "";
                }
                else if ((c == '\r' || c == '\n') && !inQuotes)
                {
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;

                    currentRow.Add(currentField);
                    data.Add(currentRow.ToArray());
                    currentRow = new List<string>();
                    currentField = "";
                }
                else
                {
                    currentField += c;
                }
            }

            if (currentRow.Count > 0 || !string.IsNullOrEmpty(currentField))
            {
                currentRow.Add(currentField);
                data.Add(currentRow.ToArray());
            }

            return data;
        }
    }
}
#endif