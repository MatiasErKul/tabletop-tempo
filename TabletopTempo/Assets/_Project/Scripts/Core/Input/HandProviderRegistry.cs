using System;
using System.Collections.Generic;
using UnityEngine;

namespace TabletopTempo.Core.Input
{
    /// <summary>
    /// Platform assemblies register hand-provider factories here (from RuntimeInitializeOnLoadMethod),
    /// so the game never references Meta or MediaPipe directly. Keys: <see cref="Meta"/>, <see cref="Webcam"/>.
    /// </summary>
    public static class HandProviderRegistry
    {
        public const string Meta = "meta";
        public const string Webcam = "webcam";

        private static readonly Dictionary<string, Func<GameObject, IHandFrameProvider>> Factories =
            new Dictionary<string, Func<GameObject, IHandFrameProvider>>();

        public static void Register(string key, Func<GameObject, IHandFrameProvider> factory) => Factories[key] = factory;

        public static bool Has(string key) => Factories.ContainsKey(key);

        /// <summary>Creates the provider on <paramref name="host"/>, or returns null if not registered or it throws.</summary>
        public static IHandFrameProvider Create(string key, GameObject host)
        {
            if (!Factories.TryGetValue(key, out var f)) return null;
            try { return f(host); }
            catch (Exception e)
            {
                Debug.LogWarning($"[TT] Hand provider '{key}' failed to start: {e.Message}");
                return null;
            }
        }
    }
}
