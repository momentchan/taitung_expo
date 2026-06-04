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
                // A=stem key, B=song name, C=lyrics, D=video (optional), E=interaction lyric rings to keep (optional, e.g. "1,3")
                if (rows[i].Length < 3) continue;

                string stemSearchKey = rows[i][0].Trim();
                string displayName = rows[i][1].Trim();
                string rawLyrics = rows[i][2].Trim();
                string videoName = rows[i].Length > 3 && rows[i][3] != null ? rows[i][3].Trim() : "";

                Song newSong = new Song();

                // Map row index to SongType Enum
                newSong.type = (SongType)i;
                newSong.songName = displayName;

                // Keep CSV lyrics as one string; line breaks are preserved for ring splitting at display time.
                newSong.lyrics = NormalizeLyrics(rawLyrics);
                newSong.videoFileName = videoName;
                newSong.interactionVisibleLyricRings = ParseRingNumberList(
                    JoinFieldsFrom(rows[i], 4),
                    newSong.interactionVisibleLyricRings);

                // 4. Auto-link Addressable stems by searching filenames (column A prefix, e.g. "01 給情人的紀念品")
                newSong.origin = FindStemReference(stemSearchKey, "示意原曲");
                newSong.vocal = FindStemReference(stemSearchKey, "Vocal");
                newSong.chord = FindStemReference(stemSearchKey, "Chords");
                newSong.bass = FindStemReference(stemSearchKey, "Bass");
                newSong.hidrums = FindStemReference(stemSearchKey, "hi-Drums");
                newSong.lowdrums = FindStemReference(stemSearchKey, "lo-Drums");

                songsSO.songs.Add(newSong);
            }

            // 5. Save SO changes to disk
            EditorUtility.SetDirty(songsSO);
            AssetDatabase.SaveAssets();
            Debug.Log("CSV parsed and Addressable AudioClips linked successfully.");
        }

        static string NormalizeLyrics(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            return raw.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        }

        static string JoinFieldsFrom(string[] row, int startIndex)
        {
            if (row == null || startIndex < 0 || startIndex >= row.Length)
                return string.Empty;

            return string.Join(",", row, startIndex, row.Length - startIndex).Trim();
        }

        static int[] ParseRingNumberList(string raw, int[] fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback ?? Array.Empty<int>();

            string normalized = raw
                .Replace('，', ',')
                .Replace('、', ',')
                .Replace(';', ',');

            string[] parts = normalized.Split(',');
            var result = new List<int>();
            var seen = new HashSet<int>();
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length == 0)
                    continue;

                if (!int.TryParse(trimmed, out int ringNumber) || ringNumber <= 0)
                {
                    Debug.LogWarning($"Invalid lyric ring number \"{trimmed}\" in CSV. Expected positive integers like 1,3.");
                    continue;
                }

                if (seen.Add(ringNumber))
                    result.Add(ringNumber);
            }

            return result.Count > 0 ? result.ToArray() : fallback ?? Array.Empty<int>();
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
