// file: NoteSpawner.cs
// DSP-scheduled audio start + time-based spawning
// Sets note speed per-instance so it always reaches the judge line in leadTimeSec
// Compatible with Note.cs (moves by DSP time; visuals only)
// written by Donghyeok Hahm + GPT
// updated: 251021 (time-judge integration)

using UnityEngine;

public class NoteSpawner : MonoBehaviour {
    public Transform[] spawnPoints;   // 6 lane spawn positions
    public Transform[] judgeZones;    // 6 lane judge positions
    public GameObject[] notePrefabs;  // 6 lane prefabs
    public Chart chart;               // ScriptableObject with notes[]
    public AudioSource audioSource;   // AudioSource that plays the song
    [Header("Travel / Scheduling")]
    public float leadTimeSec = 2.0f;  // travel time from spawn to judge
    public float preRollSec = 0.20f; // DSP scheduling margin before playback
    private int nextNoteIndex = 0;     // next note to spawn (chart index)
    private double songStartDspTime;      // DSP time when playback starts
    private bool isStarted = false;
    void Awake() {
        // Basic safety guards
        if (spawnPoints == null || spawnPoints.Length != 6)
            Debug.LogError("[NoteSpawner] spawnPoints must have 6 elements.");
        if (judgeZones == null || judgeZones.Length != 6)
            Debug.LogError("[NoteSpawner] judgeZones must have 6 elements.");
        if (notePrefabs == null || notePrefabs.Length != 6)
            Debug.LogError("[NoteSpawner] notePrefabs must have 6 elements.");
        if (chart == null)
            Debug.LogError("[NoteSpawner] Chart is not assigned.");
        if (audioSource == null)
            Debug.LogError("[NoteSpawner] AudioSource is not assigned.");
    }
    void Start() {
        if (chart == null || audioSource == null) return;
        // Schedule audio playback on the DSP clock for sample-accurate start
        songStartDspTime = AudioSettings.dspTime + preRollSec;
        audioSource.PlayScheduled(songStartDspTime + chart.globalOffset);
        // Initialize the pure time-based judging core (no colliders)
        TimingJudgeCore.I?.Init(chart, songStartDspTime);
        isStarted = true;
        // Ensure notes are ordered by time
        if (chart.notes != null && chart.notes.Length > 1)
            System.Array.Sort(chart.notes, (a, b) => a.time.CompareTo(b.time));
        nextNoteIndex = 0;
        Debug.Log($"[NoteSpawner] Scheduled start at dsp={songStartDspTime:F6}s (preRoll={preRollSec:F2}s)");
    }
    void Update() {
        if (!isStarted || chart == null || audioSource == null) return;
        if (chart.notes == null || chart.notes.Length == 0) return;
        if (nextNoteIndex >= chart.notes.Length) return;
        // Current song time referenced to DSP start, including global offset
        double songTimeSec = (AudioSettings.dspTime - songStartDspTime);
        // Spawn all notes whose "appear time" has come (hitTime - leadTime)
        // Use while-loop to catch multiple notes in the same frame
        while (nextNoteIndex < chart.notes.Length) {
            NoteData next = chart.notes[nextNoteIndex]; // hit-time in seconds (absolute in song)
            // Include globalOffset in appear time for perfect alignment
            double appearTime = next.time - leadTimeSec + chart.globalOffset;
            if (songTimeSec >= appearTime) {
                SpawnNote(next, nextNoteIndex); // pass chart index
                nextNoteIndex++;
            } else {
                // Not yet time to spawn the next note
                break;
            }
        }
    }
    // Spawns a visual note for given NoteData and registers it by chart index
    private void SpawnNote(NoteData noteData, int chartIndex) {
        // Strict validation – skip invalid notes
        if (noteData == null) return;
        if (noteData.line < 1 || noteData.line > 6) {
            Debug.LogWarning($"[NoteSpawner] Skip invalid line={noteData.line}");
            return;
        }
        if (float.IsNaN(noteData.time) || noteData.time < 0f) {
            Debug.LogWarning($"[NoteSpawner] Skip invalid time={noteData.time}");
            return;
        }
        int laneIdx = noteData.line - 1;
        Transform spawnPos = spawnPoints[laneIdx];
        Transform judgePos = judgeZones[laneIdx];
        GameObject prefab = notePrefabs[laneIdx];
        if (spawnPos == null || judgePos == null || prefab == null) {
            Debug.LogWarning($"[NoteSpawner] Missing reference on lane {noteData.line}");
            return;
        }
        GameObject go = Instantiate(prefab, spawnPos.position, Quaternion.identity);
        // Convert song-time (sec) to absolute DSP times
        double hitDsp = songStartDspTime + (noteData.time + chart.globalOffset);
        double appearDsp = hitDsp - leadTimeSec;
        // Fill visual fields
        Note note = go.GetComponent<Note>();
        if (note != null) {
            note.lineNumber = noteData.line;
            note.startPos = spawnPos.position;
            note.judgePos = judgePos.position;
            note.appearDspTime = appearDsp;
            note.hitDspTime = hitDsp;
            note.expectedHitTime = noteData.time;
            note.chartIndex = chartIndex; // registry key
        }
        // Register the visual so the time-judge can despawn by index
        NoteVisuals.Register(chartIndex, go);
    }
}