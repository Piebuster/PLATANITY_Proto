// file: ChartEditorWindow.cs
// Full-length scrollable chart editor (1~6 lanes) with snap-to-grid (magnet)
// Supports Normal / Mute notes
// written by Donghyeok Hahm
// updated: 251121 - Mute note edit

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class ChartEditorWindow : EditorWindow {
    private Chart currentChart;

    // View
    private Vector2 scrollPos;
    private float zoom = 1f;        // vertical scale multiplier
    private float beatHeight = 40f; // pixels per beat (before zoom)
    private const float LaneWidth = 60f;

    // Grid / snap
    private int quantizeDenom = 4;      // default 1/4 (quarter note)
    private bool snapEnabled = true;    // magnet on/off

    // Timing cache
    private float secPerBeat;
    private float totalTime;
    private float totalBeats;

    // Edit mode
    private enum EditKind { Normal, Mute }
    private EditKind editKind = EditKind.Normal;

    [MenuItem("Tools/PLATANITY Chart Editor")]
    public static void ShowWindow() {
        GetWindow<ChartEditorWindow>("Chart Editor");
    }

    void OnGUI() {
        GUILayout.Label("Chart Editor", EditorStyles.boldLabel);

        currentChart = (Chart)EditorGUILayout.ObjectField("Chart", currentChart, typeof(Chart), false);
        if (currentChart == null) {
            EditorGUILayout.HelpBox("Select a Chart asset to edit.", MessageType.Info);
            return;
        }

        currentChart.bpm = EditorGUILayout.FloatField("BPM", Mathf.Max(1f, currentChart.bpm));
        currentChart.globalOffset = EditorGUILayout.FloatField("Global Offset", currentChart.globalOffset);

        // Quantize selection (musical fraction denominator)
        quantizeDenom = EditorGUILayout.IntPopup(
            "Quantize",
            quantizeDenom,
            new[] { "1/1", "1/2", "1/4", "1/8", "1/16" },
            new[] { 1, 2, 4, 8, 16 }
        );

        // Magnet toggle
        snapEnabled = EditorGUILayout.Toggle(new GUIContent("Snap (magnet)", "Snap notes to the selected musical grid"), snapEnabled);

        // Edit mode toolbar
        GUILayout.Label("Edit Mode", EditorStyles.label);
        editKind = (EditKind)GUILayout.Toolbar((int)editKind, new[] { "Normal", "Mute" });

        zoom = EditorGUILayout.Slider("Zoom (vertical)", zoom, 0.3f, 3f);

        if (GUILayout.Button("Save Chart", GUILayout.Height(22))) {
            SaveChart();
        }

        DrawChartArea();
    }

    private void DrawChartArea() {
        // Content height from song length OR last note time
        secPerBeat = 60f / currentChart.bpm;

        totalTime = (currentChart.song != null) ? currentChart.song.length : 60f;
        if (currentChart.notes != null && currentChart.notes.Length > 0) {
            float lastNoteTime = currentChart.notes.Max(n => n.time);
            if (lastNoteTime > totalTime) totalTime = lastNoteTime + 2f; // margin
        }
        totalBeats = totalTime / secPerBeat;
        float contentHeight = totalBeats * beatHeight * zoom;
        float contentWidth = 6 * LaneWidth;

        // Viewport (visible area)
        float viewportMinH = 300f;
        float viewportH = Mathf.Max(viewportMinH, position.height - 200f);
        Rect viewport = GUILayoutUtility.GetRect(0, viewportH, GUILayout.ExpandWidth(true));
        GUI.Box(viewport, GUIContent.none);

        // ScrollView → Content group (content-local coords start at 0,0)
        scrollPos = GUI.BeginScrollView(viewport, scrollPos, new Rect(0, 0, contentWidth, contentHeight), true, true);
        GUI.BeginGroup(new Rect(0, 0, contentWidth, contentHeight));

        // Grid
        DrawGrid(contentWidth, contentHeight);

        // Notes
        DrawNotes();

        // Mouse input
        HandleMouseInContent(new Rect(0, 0, contentWidth, contentHeight));

        GUI.EndGroup();
        GUI.EndScrollView();
    }

    private void DrawGrid(float width, float height) {
        Handles.BeginGUI();

        // Beat lines (thick every 4 beats)
        int beatLines = Mathf.CeilToInt(totalBeats);
        for (int b = 0; b <= beatLines; b++) {
            float y = b * beatHeight * zoom;
            Handles.color = (b % 4 == 0) ? new Color(1, 1, 1, 0.9f) : new Color(1, 1, 1, 0.35f);
            Handles.DrawLine(new Vector3(0, y), new Vector3(width, y));
        }

        // Sub-division lines according to current quantize (snap grid)
        int subPerBeat = Mathf.Max(1, quantizeDenom / 4);
        if (subPerBeat > 1) {
            Handles.color = new Color(1, 1, 1, 0.20f);
            for (int b = 0; b < beatLines; b++) {
                for (int s = 1; s < subPerBeat; s++) {
                    float y = (b + s / (float)subPerBeat) * beatHeight * zoom;
                    Handles.DrawLine(new Vector3(0, y), new Vector3(width, y));
                }
            }
        }

        // Lane separators (1..6)
        for (int lane = 0; lane <= 6; lane++) {
            float x = lane * LaneWidth;
            Handles.color = new Color(1, 1, 1, 0.2f);
            Handles.DrawLine(new Vector3(x, 0), new Vector3(x, height));
        }

        Handles.EndGUI();
    }

    private void DrawNotes() {
        if (currentChart.notes == null) return;

        foreach (var note in currentChart.notes) {
            float y = (note.time / secPerBeat) * beatHeight * zoom;

            if (note.kind == NoteKind.Mute) {
                // full-width bar for Mute note
                Rect r = new Rect(0f + 5f, y - 6f, 6 * LaneWidth - 10f, 12f);
                EditorGUI.DrawRect(r, new Color(0.2f, 1f, 0.2f, 0.9f)); // green-ish
                Handles.Label(new Vector3(10f, y - 18f), $"MUTE", EditorStyles.miniLabel);
            } else {
                float x = (note.line - 1) * LaneWidth;
                Rect r = new Rect(x + 10f, y - 6f, LaneWidth - 20f, 12f);
                EditorGUI.DrawRect(r, new Color(0f, 1f, 1f, 0.9f));
                Handles.Label(new Vector3(x + 20f, y - 18f), $"L{note.line}", EditorStyles.miniLabel);
            }
        }
    }

    private void HandleMouseInContent(Rect contentRect) {
        Event e = Event.current;
        if (e == null || currentChart == null) return;

        if (e.type == EventType.MouseDown && e.button == 0 && contentRect.Contains(e.mousePosition)) {
            Vector2 p = e.mousePosition; // already content-local (due to BeginGroup)

            int line = Mathf.FloorToInt(p.x / LaneWidth) + 1; // 1..6
            if (line < 1) line = 1;
            if (line > 6) line = 6;

            float time = (p.y / (beatHeight * zoom)) * secPerBeat;

            // Snap to musical grid if enabled
            if (snapEnabled) time = SnapTime(time);

            if (e.alt) {
                // Alt + click = delete
                if (editKind == EditKind.Mute)
                    DeleteMuteNoteAt(time);
                else
                    DeleteNormalNoteAt(line, time);
            } else {
                // Left click = add
                if (editKind == EditKind.Mute)
                    AddNoteAt(1, time, NoteKind.Mute);   // lane is visually ignored
                else
                    AddNoteAt(line, time, NoteKind.Normal);
            }

            Repaint();
            e.Use();
        }
    }

    // Snap time (seconds) to the chosen musical grid
    private float SnapTime(float timeSec) {
        int subPerBeat = Mathf.Max(1, quantizeDenom / 4);
        float step = secPerBeat / subPerBeat;

        int k = Mathf.RoundToInt(timeSec / step);
        return Mathf.Max(0f, k * step);
    }

    private void AddNoteAt(int line, float time, NoteKind kind) {
        if (currentChart == null) return;

        var list = new List<NoteData>();
        if (currentChart.notes != null) list.AddRange(currentChart.notes);

        // avoid near-duplicates at same time & kind & (line for Normal)
        bool Exists(NoteData n) {
            if (n.kind != kind) return false;
            if (kind == NoteKind.Normal && n.line != line) return false;
            return Mathf.Abs(n.time - time) < 0.001f;
        }

        if (!list.Any(Exists)) {
            list.Add(new NoteData {
                line = line,
                time = Mathf.Max(0f, time),
                kind = kind
            });

            currentChart.notes = list
                .OrderBy(n => n.time)
                .ThenBy(n => n.kind)
                .ThenBy(n => n.line)
                .ToArray();
        }
    }

    private void DeleteNormalNoteAt(int line, float time) {
        if (currentChart.notes == null) return;
        var list = currentChart.notes.ToList();

        float tol = secPerBeat * 0.12f;
        var target = list
            .Where(n => n.kind == NoteKind.Normal && n.line == line)
            .OrderBy(n => Mathf.Abs(n.time - time))
            .FirstOrDefault();

        if (target != null && Mathf.Abs(target.time - time) < tol) {
            list.Remove(target);
            currentChart.notes = list.ToArray();
        }
    }

    private void DeleteMuteNoteAt(float time) {
        if (currentChart.notes == null) return;
        var list = currentChart.notes.ToList();

        float tol = secPerBeat * 0.12f;
        var target = list
            .Where(n => n.kind == NoteKind.Mute)
            .OrderBy(n => Mathf.Abs(n.time - time))
            .FirstOrDefault();

        if (target != null && Mathf.Abs(target.time - time) < tol) {
            list.Remove(target);
            currentChart.notes = list.ToArray();
        }
    }

    private void SaveChart() {
        EditorUtility.SetDirty(currentChart);
        AssetDatabase.SaveAssets();
        Debug.Log("[ChartEditor] Chart saved.");
    }
}
