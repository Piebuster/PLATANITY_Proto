// file: NoteVisuals.cs
// Purpose: Visual registry that maps chartIndex -> spawned GameObject.
using UnityEngine;
using System.Collections.Generic;
public static class NoteVisuals {
    private static readonly Dictionary<int, GameObject> map = new Dictionary<int, GameObject>();
    public static void Register(int chartIndex, GameObject go) {
        map[chartIndex] = go;
    }
    public static GameObject Get(int chartIndex) {
        map.TryGetValue(chartIndex, out var go);
        return go;
    }
    public static void Despawn(int chartIndex) {
        if (!map.TryGetValue(chartIndex, out var go) || go == null) {
            map.Remove(chartIndex);
            return;
        }
        Note note = go.GetComponent<Note>();
        // Long note head: hide only, keep object and mapping for long-body reference
        if (note != null && note.isLongHead && note.longBody != null) {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            map[chartIndex] = go;
            return;
        }
        Object.Destroy(go);
        map.Remove(chartIndex);
    }
    public static void ClearAll() {
        foreach (var kv in map) {
            if (kv.Value != null) Object.Destroy(kv.Value);
        }
        map.Clear();
    }
}
