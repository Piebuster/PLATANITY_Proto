// file: ChartEditorWindow.cs
// Full-length scrollable chart editor (1~6 lanes) with robust click mapping via content group
// written by Donghyeok Hahm + GPT
// updated: 251009-fix-v3

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

    // Grid
    private int quantize = 4; // 4/8/16 (UI only for now)

    // Timing cache
    private float secPerBeat;
    private float totalTime;
    private float totalBeats;

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

        quantize = EditorGUILayout.IntPopup("Quantize", quantize,
            new[] { "1/4", "1/8", "1/16" },
            new[] { 4, 8, 16 });

        zoom = EditorGUILayout.Slider("Zoom (vertical)", zoom, 0.3f, 3f);

        if (GUILayout.Button("Save Chart", GUILayout.Height(22))) {
            SaveChart();
        }

        DrawChartArea();
    }

    private void DrawChartArea() {
        // --- Content height from song length OR last note time ---
        secPerBeat = 60f / currentChart.bpm;

        totalTime = (currentChart.song != null) ? currentChart.song.length : 60f;
        if (currentChart.notes != null && currentChart.notes.Length > 0) {
            float lastNoteTime = currentChart.notes.Max(n => n.time);
            if (lastNoteTime > totalTime) totalTime = lastNoteTime + 2f; // margin
        }

        totalBeats = totalTime / secPerBeat;
        float contentHeight = totalBeats * beatHeight * zoom; // very tall
        float contentWidth = 6 * LaneWidth;

        // --- Viewport (visible area) independent from content ---
        float viewportMinH = 300f;
        float viewportH = Mathf.Max(viewportMinH, position.height - 200f);
        // Use GUILayout version for stable layout
        Rect viewport = GUILayoutUtility.GetRect(0, viewportH, GUILayout.ExpandWidth(true));
        GUI.Box(viewport, GUIContent.none);

        // Begin scroll view (GUILayout version keeps GUI state consistent)
        // We still pass a height to clamp viewport; content is drawn inside a group.
        scrollPos = GUI.BeginScrollView(viewport, scrollPos, new Rect(0, 0, contentWidth, contentHeight), true, true);

        // --- Begin content group: from now on (0,0) is the top-left of the entire chart content ---
        GUI.BeginGroup(new Rect(0, 0, contentWidth, contentHeight));

        // Grid
        DrawGrid(contentWidth, contentHeight);

        // Notes
        DrawNotes();

        // Mouse input (use group's local coordinates directly)
        HandleMouseInContent(new Rect(0, 0, contentWidth, contentHeight));

        GUI.EndGroup();          // end content group
        GUI.EndScrollView();     // end scroll view
    }

    private void DrawGrid(float width, float height) {
        Handles.BeginGUI();

        // Beat lines
        int beatLines = Mathf.CeilToInt(totalBeats);
        for (int b = 0; b <= beatLines; b++) {
            float y = b * beatHeight * zoom;
            Handles.color = (b % 4 == 0) ? new Color(1, 1, 1, 0.9f) : new Color(1, 1, 1, 0.35f);
            Handles.DrawLine(new Vector3(0, y), new Vector3(width, y));
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
            float x = (note.line - 1) * LaneWidth;
            Rect r = new Rect(x + 10f, y - 6f, LaneWidth - 20f, 12f);
            EditorGUI.DrawRect(r, new Color(0f, 1f, 1f, 0.9f));
            Handles.Label(new Vector3(x + 20f, y - 18f), $"L{note.line}", EditorStyles.miniLabel);
        }
    }

    private void HandleMouseInContent(Rect contentRect) {
        Event e = Event.current;
        if (e == null || currentChart == null) return;

        // We are inside the content group:
        // Event.current.mousePosition is already LOCAL to contentRect (0,0 at top-left).
        if (e.type == EventType.MouseDown && e.button == 0 && contentRect.Contains(e.mousePosition)) {
            Vector2 p = e.mousePosition; // already content-local

            int line = Mathf.FloorToInt(p.x / LaneWidth) + 1; // 1..6
            if (line < 1 || line > 6) return;

            float time = (p.y / (beatHeight * zoom)) * secPerBeat;

            if (e.alt) {
                DeleteNoteAt(line, time);
            } else {
                AddNoteAt(line, time);
            }

            Repaint();
            e.Use();
        }
    }

    private void AddNoteAt(int line, float time) {
        if (line < 1 || line > 6) return;

        var list = new List<NoteData>();
        if (currentChart.notes != null) list.AddRange(currentChart.notes);

        // Prevent near-duplicate on the same lane & time
        if (!list.Any(n => n.line == line && Mathf.Abs(n.time - time) < 0.01f)) {
            list.Add(new NoteData { line = line, time = Mathf.Max(0f, time) });
            currentChart.notes = list.OrderBy(n => n.time).ThenBy(n => n.line).ToArray();
        }
    }

    private void DeleteNoteAt(int line, float time) {
        if (currentChart.notes == null) return;
        var list = currentChart.notes.ToList();
        var target = list
            .Where(n => n.line == line)
            .OrderBy(n => Mathf.Abs(n.time - time))
            .FirstOrDefault();

        if (target != null && Mathf.Abs(target.time - time) < 0.05f) {
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
