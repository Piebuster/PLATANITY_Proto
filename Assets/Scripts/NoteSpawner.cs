// file: NoteSpawner.cs
// DSP-scheduled audio start + time-based spawning
// Sets note speed per-instance so it always reaches the judge line in leadTimeSec
// Compatible with Note.cs (moves by DSP time; visuals only)
// written by Donghyeok Hahm + GPT
// updated: 251121 (added Mute notes)

using UnityEngine;
public class NoteSpawner : MonoBehaviour {
    public Transform[] spawnPoints;   // 6 lane spawn positions
    public Transform[] judgeZones;    // 6 lane judge positions
    public GameObject[] notePrefabs;  // 6 lane prefabs
    public Chart chart;               // ScriptableObject with notes[]
    public AudioSource audioSource;   // AudioSource that plays the song
    [Header("Travel / Scheduling")]
    public float leadTimeSec = 0.5f;  // travel time from spawn to judge
    public float preRollSec = 0.20f;  // DSP scheduling margin before playback
    private int nextNoteIndex = 0;    // next note to spawn (chart index)
    private double songStartDspTime;  // DSP time when playback starts
    [Header("Measure Lines")]
    public GameObject measureLinePrefab;  // MeasureLine prefab (MeasureLineQuad)
    public Transform measureSpawn;        // starting point where measure line start fly
    public Transform measureJudge;        // location that measure line arrive
    public int beatsPerMeasure = 4;       // beat per bar (default 4/4)
    private int nextMeasureIndex = 0;     // next measure index number
    private float secPerBeat = 0f;        // 1 beat duration in seconds
    [Header("Mute Notes")]
    public GameObject muteNotePrefab;     // visual for mute note (full-width bar)
    public Transform muteSpawn;           // spawn position for mute notes
    public Transform muteJudge;           // judge position for mute notes
    [Header("Long Notes")]
    public GameObject longBodyPrefab;   // LongBodyQuad prefab
    //
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
        // 2) turn off playOnAwake , PlayScheduled exactly on code
        audioSource.playOnAwake = false;
        audioSource.Stop();
        audioSource.PlayScheduled(songStartDspTime);
        // 3) initialize timing judge core
        TimingJudgeCore.I?.Init(chart, songStartDspTime);
        isStarted = true;
        // 4) align notes (sort by time)
        if (chart.notes != null && chart.notes.Length > 1)
            System.Array.Sort(chart.notes, (a, b) => a.time.CompareTo(b.time));
        nextNoteIndex = 0;
        // +) initialize for MeasureLine
        if (chart != null && chart.bpm > 0f) {
            secPerBeat = 60f / chart.bpm;   // 1 beat duration
        }
        nextMeasureIndex = 0;
    }
    void Update() {
        if (!isStarted || chart == null || audioSource == null) return;
        if (chart.notes == null || chart.notes.Length == 0) return;
        // make total offset first
        float totalOffset = chart.globalOffset + GameSettings.UserOffsetSec;
        // chart time = time based on DSP - start Time - totalOffset
        double songTimeSec = (AudioSettings.dspTime - songStartDspTime) - totalOffset;
        // ==== spawn notes ====
        while (nextNoteIndex < chart.notes.Length) {
            NoteData next = chart.notes[nextNoteIndex];
            if (next == null) {
                nextNoteIndex++;
                continue;
            }
            // chart time (no offset added here)
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
    // Spawns a visual note for given NoteData and registers it by chart index
    private void SpawnNote(NoteData noteData, int chartIndex, float totalOffset) {
        // Strict validation – skip invalid notes
        if (noteData == null) return;
        if (float.IsNaN(noteData.time) || noteData.time < 0f) {
            Debug.LogWarning($"[NoteSpawner] Skip invalid time={noteData.time}");
            return;
        }
        // ----- Mute note path -----
        if (noteData.kind == NoteKind.Mute) {
            if (muteNotePrefab == null || muteSpawn == null || muteJudge == null) {
                Debug.LogWarning("[NoteSpawner] Mute note references not set.");
                return;
            }
            GameObject go = Instantiate(muteNotePrefab, muteSpawn.position, Quaternion.identity);
            double hitDsp = songStartDspTime + (noteData.time + totalOffset);
            double appearDsp = hitDsp - leadTimeSec;
            Note note = go.GetComponent<Note>();
            if (note != null) {
                note.lineNumber = 0; // logical "mute lane"
                note.startPos = muteSpawn.position;
                note.judgePos = muteJudge.position;
                note.appearDspTime = appearDsp;
                note.hitDspTime = hitDsp;
                note.expectedHitTime = noteData.time;
                note.chartIndex = chartIndex;
            }
            NoteVisuals.Register(chartIndex, go);
            return;
        }
        // ----- Long note path -----
        if (noteData.kind == NoteKind.Long) {
            SpawnLongNote(noteData, chartIndex, totalOffset);
            return;
        }
        // ----- Normal note path -----
        if (noteData.line < 1 || noteData.line > 6) {
            Debug.LogWarning($"[NoteSpawner] Skip invalid line={noteData.line}");
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
        GameObject goNormal = Instantiate(prefab, spawnPos.position, Quaternion.identity);
        double hitDspNormal = songStartDspTime + (noteData.time + totalOffset);
        double appearDspNormal = hitDspNormal - leadTimeSec;
        Note noteComp = goNormal.GetComponent<Note>();
        if (noteComp != null) {
            noteComp.lineNumber = noteData.line;
            noteComp.startPos = spawnPos.position;
            noteComp.judgePos = judgePos.position;
            noteComp.appearDspTime = appearDspNormal;
            noteComp.hitDspTime = hitDspNormal;
            noteComp.expectedHitTime = noteData.time;
            noteComp.chartIndex = chartIndex;
        }
        NoteVisuals.Register(chartIndex, goNormal);
    }
    private void SpawnMeasuresIfNeeded(double songTimeSec, float totalOffset) {
        // if prefab or point empty -> quit
        if (measureLinePrefab == null || measureSpawn == null || measureJudge == null)
            return;
        if (secPerBeat <= 0f || beatsPerMeasure <= 0)
            return;
        // length of one bar(sec) = secPerBeat * beatsPerMeasure
        double measureDuration = secPerBeat * beatsPerMeasure;
        // about nextMeasureIndex = 0,1,2,…
        while (true) {
            // hit timing of this bar
            double measureTime = nextMeasureIndex * measureDuration;
            // same logic with note : appearTime = hitTime - leadTimeSec
            double appearTime = measureTime - leadTimeSec;

            // songTime passes appearTime -> spawn this MeasureLine
            if (songTimeSec >= appearTime) {
                SpawnMeasureLine(measureTime, totalOffset);
                nextMeasureIndex++;   // prepare next bar
            } else {
                // not time yet → no more thing to spawn
                break;
            }
        }
    }
    private void SpawnMeasureLine(double measureTimeSec, float totalOffset) {
        // always spawn from same point (measureSpawn)
        GameObject go = Object.Instantiate(measureLinePrefab,
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
    // Spawn visual objects for a long note (head + tail + body)
    private void SpawnLongNote(NoteData noteData, int chartIndex, float totalOffset) {
        if (noteData.line < 1 || noteData.line > 6) {
            Debug.LogWarning($"[NoteSpawner] Long note invalid line={noteData.line}");
            return;
        }
        if (noteData.endTime <= noteData.time + 0.001f) {
            Debug.LogWarning($"[NoteSpawner] Long note has invalid endTime, fallback to Normal. time={noteData.time}, end={noteData.endTime}");
            noteData.kind = NoteKind.Normal;
            SpawnNote(noteData, chartIndex, totalOffset);
            return;
        }
        int laneIdx = noteData.line - 1;
        Transform spawnPos = spawnPoints[laneIdx];
        Transform judgePos = judgeZones[laneIdx];
        GameObject prefab = notePrefabs[laneIdx];
        if (spawnPos == null || judgePos == null || prefab == null) {
            Debug.LogWarning($"[NoteSpawner] Missing reference on lane {noteData.line} (Long)");
            return;
        }
        // HEAD (registered for judge/despawn)
        GameObject head = Instantiate(prefab, spawnPos.position, Quaternion.identity);
        double headHitDsp = songStartDspTime + (noteData.time + totalOffset);
        double headAppearDsp = headHitDsp - leadTimeSec;
        Note headNote = head.GetComponent<Note>();
        if (headNote != null) {
            headNote.lineNumber = noteData.line;
            headNote.startPos = spawnPos.position;
            headNote.judgePos = judgePos.position;
            headNote.appearDspTime = headAppearDsp;
            headNote.hitDspTime = headHitDsp;
            headNote.expectedHitTime = noteData.time;
            headNote.chartIndex = chartIndex;
            headNote.isLongHead = true;
        }
        NoteVisuals.Register(chartIndex, head);
        // TAIL (visual only)
        GameObject tail = Instantiate(prefab, spawnPos.position, Quaternion.identity);
        double tailHitDsp = songStartDspTime + (noteData.endTime + totalOffset);
        double tailAppearDsp = tailHitDsp - leadTimeSec;
        Note tailNote = tail.GetComponent<Note>();
        if (tailNote != null) {
            tailNote.lineNumber = noteData.line;
            tailNote.startPos = spawnPos.position;
            tailNote.judgePos = judgePos.position;
            tailNote.appearDspTime = tailAppearDsp;
            tailNote.hitDspTime = tailHitDsp;
            tailNote.expectedHitTime = noteData.endTime;
            tailNote.chartIndex = -1;
        }
        // BODY (between head and tail)
        if (longBodyPrefab != null) {
            GameObject body = Instantiate(longBodyPrefab, spawnPos.position, Quaternion.identity);
            LongNoteBody bodyComp = body.GetComponent<LongNoteBody>();
            if (bodyComp != null) {
                bodyComp.head = head.transform;
                bodyComp.tail = tail.transform;
                bodyComp.judge = judgePos; // <- important
                                           // we can use baseWidth value set in prefab
                                           // if we need, it's ok to adjust by Lane
            }
            if (headNote != null)
                headNote.longBody = bodyComp; 
        }
    }
}
