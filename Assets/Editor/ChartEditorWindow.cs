// file: ChartEditorWindow.cs
// simple custom editor window for chart editing
// written with gpt help
// recent update: 2509252318

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ChartEditorWindow : EditorWindow {
    private Chart currentChart;
    private Vector2 scrollPos;
    private float zoom = 1f;
    private float beatHeight = 40f; // 1 tempo vertical size(px)
    private int quantize = 4;       // quarter note is standard

    [MenuItem("Tools/PLATANITY Chart Editor")]
    public static void ShowWindow() {
        GetWindow<ChartEditorWindow>("Chart Editor");
    }

    void OnGUI() {
        GUILayout.Label("Chart Editor", EditorStyles.boldLabel);

        currentChart = (Chart)EditorGUILayout.ObjectField("Chart", currentChart, typeof(Chart), false);

        if (currentChart == null) {
            EditorGUILayout.HelpBox("Select Chart.", MessageType.Info);
            return;
        }

        currentChart.bpm = EditorGUILayout.FloatField("BPM", currentChart.bpm);
        currentChart.globalOffset = EditorGUILayout.FloatField("Global Offset", currentChart.globalOffset);

        quantize = EditorGUILayout.IntPopup("Quantize", quantize,
            new string[] { "1/4", "1/8", "1/16" },
            new int[] { 4, 8, 16 });

        zoom = EditorGUILayout.Slider("Zoom", zoom, 0.5f, 2f);

        DrawChartArea();
    }

    private void DrawChartArea() {
        float secPerBeat = 60f / currentChart.bpm;
        float totalTime = (currentChart.song != null) ? currentChart.song.length : 60f;

        float totalBeats = totalTime / secPerBeat;

        Rect chartRect = GUILayoutUtility.GetRect(800, totalBeats * beatHeight * zoom, GUILayout.ExpandWidth(true));
        GUI.Box(chartRect, GUIContent.none);

        scrollPos = GUI.BeginScrollView(chartRect, scrollPos, new Rect(0, 0, chartRect.width - 20, totalBeats * beatHeight * zoom));

        // draw grid
        Handles.BeginGUI();
        for (int b = 0; b < totalBeats; b++) {
            float y = b * beatHeight * zoom;
            Handles.color = (b % 4 == 0) ? Color.white : Color.gray;
            Handles.DrawLine(new Vector3(0, y), new Vector3(chartRect.width, y));
        }
        Handles.EndGUI();

        // Display Note
        if (currentChart.notes != null) {
            foreach (var note in currentChart.notes) {
                float y = (note.time / secPerBeat) * beatHeight * zoom;
                Rect r = new Rect(note.line * 50, y, 40, 10);
                EditorGUI.DrawRect(r, Color.cyan);
            }
        }

        GUI.EndScrollView();
    }
}


