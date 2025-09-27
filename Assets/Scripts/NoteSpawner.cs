// file: NoteSpawner.cs
// final stable with time sorting

using UnityEngine;
using System.Linq;

public class NoteSpawner : MonoBehaviour {
    [Header("Scene References (size must be 6)")]
    public Transform[] spawnPoints;
    public Transform[] judgeZones;
    public GameObject[] notePrefabs;

    [Header("Chart / Audio")]
    public Chart chart;
    public AudioSource audioSource;

    [Header("Timing")]
    public float spawnLeadTime = 1.5f;
    public bool autoPlayOnStart = true;

    private int nextNoteIndex = 0;

    void Start() {
        NoteInputManager.audioSource = audioSource;

        if (chart == null || audioSource == null) {
            Debug.LogError("[NoteSpawner] Missing chart or audioSource");
            enabled = false; return;
        }

        // sort notes by time (then by line for same-time chords)
        if (chart.notes != null && chart.notes.Length > 1) {
            chart.notes = chart.notes
                .OrderBy(n => n.time)
                .ThenBy(n => n.line)
                .ToArray();
        }

        if (autoPlayOnStart && audioSource.clip != null) {
            audioSource.Play();
        }

        Debug.Log($"[NoteSpawner] Ready, notes={chart.notes?.Length ?? 0}");
    }

    void Update() {
        if (chart == null || audioSource == null) return;
        if (chart.notes == null || chart.notes.Length == 0) return;
        if (nextNoteIndex >= chart.notes.Length) return;

        float songTime = audioSource.time + chart.globalOffset;
        NoteData nextNote = chart.notes[nextNoteIndex];

        if (songTime >= nextNote.time - spawnLeadTime) {
            SpawnNote(nextNote);
            nextNoteIndex++;
        }
    }

    void SpawnNote(NoteData noteData) {
        int line = noteData.line - 1;
        if (line < 0 || line >= spawnPoints.Length) return;

        Transform spawnPos = spawnPoints[line];
        Transform judgePos = judgeZones[line];
        GameObject prefab = notePrefabs[line];

        var go = Instantiate(prefab, spawnPos.position, Quaternion.identity);
        var note = go.GetComponent<Note>();

        note.lineNumber = noteData.line;
        note.expectedHitTime = noteData.time;
        note.travelDuration = spawnLeadTime;
        note.travelStartTime = note.expectedHitTime - spawnLeadTime;

        note.spawnPos = spawnPos;
        note.judgePos = judgePos;
    }
}
