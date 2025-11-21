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
    public float leadTimeSec = 0.5f;  // travel time from spawn to judge
    public float preRollSec = 0.20f; // DSP scheduling margin before playback
    private int nextNoteIndex = 0;     // next note to spawn (chart index)
    private double songStartDspTime;      // DSP time when playback starts
    [Header("Measure Lines")]
    public GameObject measureLinePrefab;  // MesureLine prefab (MeasureLineQuad)
    public Transform measureSpawn;        // starting point where measure line start fly
    public Transform measureJudge;        // location that measure line arrive
    public int beatsPerMeasure = 4;       // beat per one bar? (default 4)
    private int nextMeasureIndex = 0;     // next measure index number
    private float secPerBeat = 0f;        // 1 beat == how long sec?
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
        // 1) set play time based on DSP
        songStartDspTime = AudioSettings.dspTime + preRollSec;
        // 2) turn off playOnAwake , playscheduled exactly on code
        audioSource.playOnAwake = false;
        audioSource.Stop();
        audioSource.PlayScheduled(songStartDspTime);
        // 3) initialize timing judge core
        TimingJudgeCore.I?.Init(chart, songStartDspTime);
        isStarted = true;
        // 4) align notes
        if (chart.notes != null && chart.notes.Length > 1)
            System.Array.Sort(chart.notes, (a, b) => a.time.CompareTo(b.time));
        nextNoteIndex = 0;
        // +) initialize for MesureLine
        if (chart != null && chart.bpm > 0f) {
            secPerBeat = 60f / chart.bpm;   // 1 beat == ?sec
        }
        nextMeasureIndex = 0;
    }
    void Update() {
        if (!isStarted || chart == null || audioSource == null) return;
        if (chart.notes == null || chart.notes.Length == 0) return;
        if (nextNoteIndex >= chart.notes.Length) return;
        // make total offset first
        float totalOffset = chart.globalOffset + GameSettings.UserOffsetSec;
        // chart time = time based on DSP - start Time - totaloffset
        double songTimeSec = (AudioSettings.dspTime - songStartDspTime) - totalOffset;
        while (nextNoteIndex < chart.notes.Length) {
            NoteData next = chart.notes[nextNoteIndex];
            // it's chart time so don't add global offset
            double appearTime = next.time - leadTimeSec;   
            if (songTimeSec >= appearTime) {
                SpawnNote(next, nextNoteIndex, totalOffset);
                nextNoteIndex++;
            } else {
                break;
            }
        }
        // ==== spawn MeasureLine ====
        SpawnMeasuresIfNeeded(songTimeSec, totalOffset);
    }
    // Spawns a visual note for given NoteData and registers it by chart index
    private void SpawnNote(NoteData noteData, int chartIndex, float totalOffset) {
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
        double hitDsp = songStartDspTime + (noteData.time + totalOffset);
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
    private void SpawnMeasuresIfNeeded(double songTimeSec, float totalOffset) {
        // if prefab or point empty -> quit
        if (measureLinePrefab == null || measureSpawn == null || measureJudge == null)
            return;
        if (secPerBeat <= 0f || beatsPerMeasure <= 0)
            return;
        // length of one bar(sec) = 1 bar length * beat per bar
        double measureDuration = secPerBeat * beatsPerMeasure;
        // about nextMeasureIndex = 0,1,2,… 
        while (true) {
            // hit timing of this bar
            double measureTime = nextMeasureIndex * measureDuration;
            // same logic with note : appearTime = hitTime - leadTimeSec
            double appearTime = measureTime - leadTimeSec;
            // songTime pass by appearTime -> spawn this MeasureLine
            if (songTimeSec >= appearTime) {
                SpawnMeasureLine(measureTime, totalOffset);
                nextMeasureIndex++;   // prepare next bar
            } else {
                // not timing yet → no more thing to spawn
                break;
            }
        }
    }
    private void SpawnMeasureLine(double measureTimeSec, float totalOffset) {
        // always spawn from same point (measureSpawn)
        GameObject go = Instantiate(measureLinePrefab,
                                    measureSpawn.position,
                                    Quaternion.identity);
        // calculate DSP time based on same rule with note
        double hitDsp = songStartDspTime + (measureTimeSec + totalOffset);
        double appearDsp = hitDsp - leadTimeSec;
        // fill value in MeasureLine script
        MeasureLine line = go.GetComponent<MeasureLine>();
        if (line != null) {
            line.startPos = measureSpawn.position;
            line.judgePos = measureJudge.position;
            line.appearDspTime = appearDsp;
            line.hitDspTime = hitDsp;
        }
    }
}
