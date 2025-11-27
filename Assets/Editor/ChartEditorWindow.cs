// file: ChartEditorWindow.cs
// Full-length scrollable chart editor (1~6 lanes) with snap-to-grid (magnet)
// Supports Normal / Mute / Long / Tap notes
// written by Donghyeok Hahm + GPT

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
    private int quantizeDenom = 4;  // default 1/4 (quarter note)
    private bool snapEnabled = true;
    // Timing cache
    private float secPerBeat;
    private float totalTime;
    private float totalBeats;
    // Edit mode
    private enum EditKind { Normal, Mute, Long, Tap }
    private EditKind editKind = EditKind.Normal;
    // Long-note temp state (head waiting for tail)
    private bool hasPendingLong = false;
    private int pendingLongLane = 1;
    private float pendingLongTime = 0f;

    [MenuItem("Tools/PLATANITY Chart Editor")]
    public static void ShowWindow() { GetWindow<ChartEditorWindow>("Chart Editor"); }

    void OnGUI() {
        GUILayout.Label("Chart Editor", EditorStyles.boldLabel);
        currentChart = (Chart)EditorGUILayout.ObjectField("Chart", currentChart, typeof(Chart), false);
        if (currentChart == null) {
            EditorGUILayout.HelpBox("Select a Chart asset to edit.", MessageType.Info);
            return;
        }
        currentChart.bpm = EditorGUILayout.FloatField("BPM", Mathf.Max(1f, currentChart.bpm));
        currentChart.globalOffset = EditorGUILayout.FloatField("Global Offset", currentChart.globalOffset);
        quantizeDenom = EditorGUILayout.IntPopup("Quantize", quantizeDenom, new[] { "1/1", "1/2", "1/4", "1/8", "1/16" }, new[] { 1, 2, 4, 8, 16 });
        snapEnabled = EditorGUILayout.Toggle(new GUIContent("Snap (magnet)", "Snap notes to the selected musical grid"), snapEnabled);

        GUILayout.Label("Edit Mode", EditorStyles.label);
        EditKind newKind = (EditKind)GUILayout.Toolbar((int)editKind, new[] { "Normal", "Mute", "Long", "Tap" });
        if (newKind != editKind && newKind != EditKind.Long) hasPendingLong = false;
        editKind = newKind;

        zoom = EditorGUILayout.Slider("Zoom (vertical)", zoom, 0.3f, 3f);
        if (GUILayout.Button("Save Chart", GUILayout.Height(22))) SaveChart();
        DrawChartArea();
    }
    private void DrawChartArea() {
        secPerBeat = 60f / currentChart.bpm;
        totalTime = (currentChart.song != null) ? currentChart.song.length : 60f;
        if (currentChart.notes != null && currentChart.notes.Length > 0) {
            float lastNoteTime = currentChart.notes.Max(n => n.time);
            if (lastNoteTime > totalTime) totalTime = lastNoteTime + 2f;
        }
        totalBeats = totalTime / secPerBeat;
        float contentHeight = totalBeats * beatHeight * zoom;
        float contentWidth = 6 * LaneWidth;
        float viewportMinH = 300f;
        float viewportH = Mathf.Max(viewportMinH, position.height - 200f);
        Rect viewport = GUILayoutUtility.GetRect(0, viewportH, GUILayout.ExpandWidth(true));
        GUI.Box(viewport, GUIContent.none);

        scrollPos = GUI.BeginScrollView(viewport, scrollPos, new Rect(0, 0, contentWidth, contentHeight), true, true);
        GUI.BeginGroup(new Rect(0, 0, contentWidth, contentHeight));
        DrawGrid(contentWidth, contentHeight);
        DrawNotes();
        HandleMouseInContent(new Rect(0, 0, contentWidth, contentHeight));
        GUI.EndGroup();
        GUI.EndScrollView();
    }
    private void DrawGrid(float width, float height) {
        Handles.BeginGUI();
        int beatLines = Mathf.CeilToInt(totalBeats);
        for (int b = 0; b <= beatLines; b++) {
            float y = b * beatHeight * zoom;
            Handles.color = (b % 4 == 0) ? new Color(1, 1, 1, 0.9f) : new Color(1, 1, 1, 0.35f);
            Handles.DrawLine(new Vector3(0, y), new Vector3(width, y));
        }
        int subPerBeat = Mathf.Max(1, quantizeDenom / 4);
        if (subPerBeat > 1) {
            Handles.color = new Color(1, 1, 1, 0.2f);
            for (int b = 0; b < beatLines; b++) {
                for (int s = 1; s < subPerBeat; s++) {
                    float y = (b + s / (float)subPerBeat) * beatHeight * zoom;
                    Handles.DrawLine(new Vector3(0, y), new Vector3(width, y));
                }
            }
        }
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
            float x = (note.line - 1) * LaneWidth;
            if (note.kind == NoteKind.Mute) {
                float y = (note.time / secPerBeat) * beatHeight * zoom;
                Rect r = new Rect(5f, y - 6f, 6 * LaneWidth - 10f, 12f);
                EditorGUI.DrawRect(r, new Color(0.2f, 1f, 0.2f, 0.9f));
                Handles.Label(new Vector3(10f, y - 18f), "MUTE", EditorStyles.miniLabel);
            } else if (note.kind == NoteKind.Long) {
                float yHead = (note.time / secPerBeat) * beatHeight * zoom;
                float yTail = (note.endTime / secPerBeat) * beatHeight * zoom;
                float top = Mathf.Min(yHead, yTail);
                float bottom = Mathf.Max(yHead, yTail);
                Rect body = new Rect(x + 18f, top, LaneWidth - 36f, Mathf.Max(4f, bottom - top));
                EditorGUI.DrawRect(body, new Color(0f, 0.7f, 1f, 0.35f));
                Rect headRect = new Rect(x + 10f, yHead - 5f, LaneWidth - 20f, 10f);
                Rect tailRect = new Rect(x + 10f, yTail - 5f, LaneWidth - 20f, 10f);
                EditorGUI.DrawRect(headRect, new Color(1f, 1f, 1f, 0.9f));
                EditorGUI.DrawRect(tailRect, new Color(1f, 0.9f, 0.2f, 0.9f));
                Handles.Label(new Vector3(x + 20f, top - 16f), $"L{note.line} LONG", EditorStyles.miniLabel);
            } else if (note.kind == NoteKind.Tap) {
                float y = (note.time / secPerBeat) * beatHeight * zoom;
                Rect r = new Rect(x + 10f, y - 6f, LaneWidth - 20f, 12f);
                EditorGUI.DrawRect(r, new Color(1f, 0.6f, 0f, 0.95f)); // orange for tap
                Handles.Label(new Vector3(x + 20f, y - 18f), $"L{note.line} TAP", EditorStyles.miniLabel);
            } else { // Normal
                float y = (note.time / secPerBeat) * beatHeight * zoom;
                Rect r = new Rect(x + 10f, y - 6f, LaneWidth - 20f, 12f);
                EditorGUI.DrawRect(r, new Color(0f, 1f, 1f, 0.9f));
                Handles.Label(new Vector3(x + 20f, y - 18f), $"L{note.line}", EditorStyles.miniLabel);
            }
        }
        if (editKind == EditKind.Long && hasPendingLong) {
            float yPending = (pendingLongTime / secPerBeat) * beatHeight * zoom;
            float xPending = (pendingLongLane - 1) * LaneWidth;
            Rect pendingRect = new Rect(xPending + 10f, yPending - 5f, LaneWidth - 20f, 10f);
            EditorGUI.DrawRect(pendingRect, new Color(1f, 1f, 1f, 0.4f));
            Handles.Label(new Vector3(xPending + 20f, yPending - 18f), "LONG HEAD...", EditorStyles.miniLabel);
        }
    }
    private void HandleMouseInContent(Rect contentRect) {
        Event e = Event.current;
        if (e == null || currentChart == null) return;
        if (e.type == EventType.MouseDown && e.button == 0 && contentRect.Contains(e.mousePosition)) {
            Vector2 p = e.mousePosition;
            int line = Mathf.FloorToInt(p.x / LaneWidth) + 1;
            line = Mathf.Clamp(line, 1, 6);
            float time = (p.y / (beatHeight * zoom)) * secPerBeat;
            if (snapEnabled) time = SnapTime(time);

            if (e.alt) {
                // delete
                if (editKind == EditKind.Mute) DeleteMuteNoteAt(time);
                else if (editKind == EditKind.Long) DeleteLongNoteAt(line, time);
                else if (editKind == EditKind.Tap) DeleteTapNoteAt(line, time);
                else DeleteNormalNoteAt(line, time);
            } else {
                // add
                if (editKind == EditKind.Mute) AddNoteAt(1, time, NoteKind.Mute);
                else if (editKind == EditKind.Long) HandleLongClick(line, time);
                else if (editKind == EditKind.Tap) AddNoteAt(line, time, NoteKind.Tap);
                else AddNoteAt(line, time, NoteKind.Normal);
            }
            Repaint();
            e.Use();
        }
    }
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

        bool Exists(NoteData n) {
            if (n.kind != kind) return false;
            if ((kind == NoteKind.Normal || kind == NoteKind.Tap) && n.line != line) return false;
            return Mathf.Abs(n.time - time) < 0.001f;
        }

        if (!list.Any(Exists)) {
            list.Add(new NoteData { line = line, time = Mathf.Max(0f, time), kind = kind });
            currentChart.notes = list.OrderBy(n => n.time).ThenBy(n => n.kind).ThenBy(n => n.line).ToArray();
        }
    }
    private void HandleLongClick(int line, float time) {
        if (!hasPendingLong) {
            hasPendingLong = true;
            pendingLongLane = line;
            pendingLongTime = Mathf.Max(0f, time);
            return;
        }
        if (line != pendingLongLane) {
            // clicking another lane moves head to that lane
            pendingLongLane = line;
            pendingLongTime = Mathf.Max(0f, time);
            return;
        }
        if (Mathf.Abs(time - pendingLongTime) < secPerBeat * 0.05f) {
            // second click very close to first -> cancel
            hasPendingLong = false;
            return;
        }
        float start = Mathf.Min(pendingLongTime, time);
        float end = Mathf.Max(pendingLongTime, time);
        AddLongNoteAt(pendingLongLane, start, end);
        hasPendingLong = false;
    }
    private void AddLongNoteAt(int line, float startTime, float endTime) {
        if (currentChart == null) return;
        if (endTime <= startTime + 0.001f) return;
        var list = new List<NoteData>();
        if (currentChart.notes != null) list.AddRange(currentChart.notes);
        list.Add(new NoteData { line = line, time = Mathf.Max(0f, startTime), endTime = endTime, kind = NoteKind.Long });
        currentChart.notes = list.OrderBy(n => n.time).ThenBy(n => n.kind).ThenBy(n => n.line).ToArray();
    }
    private void DeleteNormalNoteAt(int line, float time) {
        if (currentChart.notes == null) return;
        var list = currentChart.notes.ToList();
        float tol = secPerBeat * 0.12f;
        var target = list.Where(n => n.kind == NoteKind.Normal && n.line == line).OrderBy(n => Mathf.Abs(n.time - time)).FirstOrDefault();
        if (target != null && Mathf.Abs(target.time - time) < tol) {
            list.Remove(target);
            currentChart.notes = list.ToArray();
        }
    }
    private void DeleteTapNoteAt(int line, float time) {
        if (currentChart.notes == null) return;
        var list = currentChart.notes.ToList();
        float tol = secPerBeat * 0.12f;
        var target = list.Where(n => n.kind == NoteKind.Tap && n.line == line).OrderBy(n => Mathf.Abs(n.time - time)).FirstOrDefault();
        if (target != null && Mathf.Abs(target.time - time) < tol) {
            list.Remove(target);
            currentChart.notes = list.ToArray();
        }
    }
    private void DeleteMuteNoteAt(float time) {
        if (currentChart.notes == null) return;
        var list = currentChart.notes.ToList();
        float tol = secPerBeat * 0.12f;
        var target = list.Where(n => n.kind == NoteKind.Mute).OrderBy(n => Mathf.Abs(n.time - time)).FirstOrDefault();
        if (target != null && Mathf.Abs(target.time - time) < tol) {
            list.Remove(target);
            currentChart.notes = list.ToArray();
        }
    }
    private void DeleteLongNoteAt(int line, float time) {
        if (currentChart.notes == null) return;
        var list = currentChart.notes.ToList();
        float tol = secPerBeat * 0.12f;
        var target = list
            .Where(n => n.kind == NoteKind.Long && n.line == line)
            .OrderBy(n => Mathf.Min(Mathf.Abs(n.time - time), Mathf.Abs(n.endTime - time)))
            .FirstOrDefault();
        if (target != null) {
            float minDist = Mathf.Min(Mathf.Abs(target.time - time), Mathf.Abs(target.endTime - time));
            if (minDist < tol) {
                list.Remove(target);
                currentChart.notes = list.ToArray();
            }
        }
    }
    private void SaveChart() {
        EditorUtility.SetDirty(currentChart);
        AssetDatabase.SaveAssets();
        Debug.Log("[ChartEditor] Chart saved.");
    }
}
