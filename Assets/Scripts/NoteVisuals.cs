// file: NoteVisuals.cs
// Purpose: Visual registry that maps chartIndex -> spawned GameObject.
// Usage:
//   - On spawn:   NoteVisuals.Register(index, go);
//   - On despawn: NoteVisuals.Despawn(index);
using UnityEngine;
using System.Collections.Generic;
public static class NoteVisuals {
    // Stores the latest GameObject for each chart index.
    private static readonly Dictionary<int, GameObject> map = new Dictionary<int, GameObject>();
    /// <summary>Registers (or replaces) the visual instance for a chart index.</summary>
    public static void Register(int chartIndex, GameObject go) {
        map[chartIndex] = go;
    }
    /// <summary>Safely destroys and removes the visual for a chart index (if any).</summary>
    public static void Despawn(int chartIndex) {
        if (map.TryGetValue(chartIndex, out var go) && go != null) {
            Object.Destroy(go);
        }
        map.Remove(chartIndex);
    }
    /// <summary>Optional: clears all visuals (e.g., on song stop/reset).</summary>
    public static void ClearAll() {
        foreach (var kv in map) {
            if (kv.Value != null) Object.Destroy(kv.Value);
        }
        map.Clear();
    }
}

