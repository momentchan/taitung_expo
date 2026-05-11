using System;
using System.Reflection;
using Klak.Hap;
using UnityEngine;

namespace TaitungExpo
{
    /// <summary>
    /// Klak HapPlayer.Open rejects a second call while a stream is active. This mirrors HapPlayer.OnDestroy
    /// cleanup so the same component can open another file (field names match jp.keijiro.klak.hap runtime).
    /// </summary>
    public static class KlakHapPlayerStreamClose
    {
        static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly Type HapType = typeof(HapPlayer);

        public static void CloseStreamIfOpen(this HapPlayer player)
        {
            if (player == null) return;

            HapType.GetField("_filePath", Flags)?.SetValue(player, string.Empty);

            DisposeAndClearField(player, "_updater");
            DisposeAndClearField(player, "_decoder");
            DisposeAndClearField(player, "_stream");
            DisposeAndClearField(player, "_demuxer");

            DestroyUnityObjectField(player, "_texture");
            DestroyUnityObjectField(player, "_blitMaterial");
            player.enabled = true;
        }

        static void DisposeAndClearField(HapPlayer player, string fieldName)
        {
            var field = HapType.GetField(fieldName, Flags);
            if (field == null) return;

            object value = field.GetValue(player);
            if (value is IDisposable d)
            {
                try { d.Dispose(); }
                catch (Exception e) { Debug.LogWarning($"Hap dispose ({fieldName}): {e.Message}"); }
            }

            field.SetValue(player, null);
        }

        static void DestroyUnityObjectField(HapPlayer player, string fieldName)
        {
            var field = HapType.GetField(fieldName, Flags);
            if (field == null) return;

            var obj = field.GetValue(player) as UnityEngine.Object;
            if (obj != null)
                UnityEngine.Object.Destroy(obj);

            field.SetValue(player, null);
        }
    }
}
