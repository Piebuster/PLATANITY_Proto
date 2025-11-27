// file: NoteSpawner.cs
// DSP-scheduled audio start + time-based spawning
// Sets note speed per-instance so it always reaches the judge line in leadTimeSec
// Compatible with Note.cs (moves by DSP time; visuals only)
// written by Donghyeok Hahm + GPT
// updated: 251127 (added Tap notes)

using UnityEngine;
public class NoteSpawner : MonoBehaviour {
    public Transform[] spawnPoints;   // 6 lane spawn positions
    public Transform[] judgeZones;    // 6 lane judge positions
    public GameObject[] notePrefabs;  // normal note prefabs per lane (1..6)
    public Chart chart;               // ScriptableObject with notes[]
    public AudioSource audioSource;   // AudioSource that plays the song

    [Header("Travel / Scheduling")]
    public float leadTimeSec = 0.5f;  // travel time from spawn to judge
    public float preRollSec = 0.20f;  // DSP scheduling margin before playback
    private int nextNoteIndex = 0;    // next note to spawn (chart index)
    private double songStartDspTime;  // DSP time when playback starts
    [Header("Measure Lines")]
    public GameObject measureLinePrefab;  // MeasureLine prefab (MeasureLineQuad)
    public Transform measureSpawn;        // starting point where measure line starts to fly
    public Transform measureJudge;        // location that measure line arrives
    public int beatsPerMeasure = 4;       // beat per bar (default 4/4)
    private int nextMeasureIndex = 0;     // next measure index number
    private float secPerBeat = 0f;        // 1 beat duration in seconds
    [Header("Mute Notes")]
    public GameObject muteNotePrefab;     // visual for mute note (full-width bar)
    public Transform muteSpawn;           // spawn position for mute notes
    public Transform muteJudge;           // judge position for mute notes
    [Header("Long Notes")]
    public GameObject longBodyPrefab;     // LongBodyQuad prefab
    [Header("Tap Notes")]
    public GameObject[] tapNotePrefabs;   // optional tap-note prefabs per lane (1..6)
    private bool isStarted = false;
    void Awake() {
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
        // 1) schedule audio by DSP time
        songStartDspTime = AudioSettings.dspTime + preRollSec;
        audioSource.playOnAwake = false;
        audioSource.Stop();
        audioSource.PlayScheduled(songStartDspTime);
        // 2) init timing core
        TimingJudgeCore.I?.Init(chart, songStartDspTime);
        isStarted = true;
        // 3) sort notes by time
        if (chart.notes != null && chart.notes.Length > 1)
            System.Array.Sort(chart.notes, (a, b) => a.time.CompareTo(b.time));
        nextNoteIndex = 0;
        // 4) measure line setup
        if (chart != null && chart.bpm > 0f)
            secPerBeat = 60f / chart.bpm;
        nextMeasureIndex = 0;
    }
    void Update() {
        if (!isStarted || chart == null || audioSource == null) return;
        if (chart.notes == null || chart.notes.Length == 0) return;

        float totalOffset = chart.globalOffset + GameSettings.UserOffsetSec;
        double songTimeSec = (AudioSettings.dspTime - songStartDspTime) - totalOffset;

        // spawn notes
        while (nextNoteIndex < chart.notes.Length) {
            NoteData next = chart.notes[nextNoteIndex];
            if (next == null) { nextNoteIndex++; continue; }
            double appearTime = next.time - leadTimeSec;
            if (songTimeSec >= appearTime) {
                SpawnNote(next, nextNoteIndex, totalOffset);
                nextNoteIndex++;
            } else break;
        }

        // spawn measure lines
        SpawnMeasuresIfNeeded(songTimeSec, totalOffset);
    }
    // Spawn a visual note for given NoteData and register it by chart index
    private void SpawnNote(NoteData noteData, int chartIndex, float totalOffset) {
        if (noteData == null) return;
        if (float.IsNaN(noteData.time) || noteData.time < 0f) {
            Debug.LogWarning($"[NoteSpawner] Skip invalid time={noteData.time}");
            return;
        }
        // ----- Mute note -----
        if (noteData.kind == NoteKind.Mute) {
            if (muteNotePrefab == null || muteSpawn == null || muteJudge == null) {
                Debug.LogWarning("[NoteSpawner] Mute note references not set.");
                return;
            }
            GameObject go = Instantiate(muteNotePrefab, muteSpawn.position, Quaternion.identity);
            double hitDspMute = songStartDspTime + (noteData.time + totalOffset);
            double appearDspMute = hitDspMute - leadTimeSec;
            Note note = go.GetComponent<Note>();
            if (note != null) {
                note.lineNumber = 0; // logical "mute lane"
                note.startPos = muteSpawn.position;
                note.judgePos = muteJudge.position;
                note.appearDspTime = appearDspMute;
                note.hitDspTime = hitDspMute;
                note.expectedHitTime = noteData.time;
                note.chartIndex = chartIndex;
            }
            NoteVisuals.Register(chartIndex, go);
            return;
        }
        // ----- Long note -----
        if (noteData.kind == NoteKind.Long) {
            SpawnLongNote(noteData, chartIndex, totalOffset);
            return;
        }
        // ----- Tap / Normal notes (lane-based) -----
        if (noteData.line < 1 || noteData.line > 6) {
            Debug.LogWarning($"[NoteSpawner] Skip invalid line={noteData.line}");
            return;
        }
        int laneIdx = noteData.line - 1;
        Transform spawnPos = spawnPoints[laneIdx];
        Transform judgePos = judgeZones[laneIdx];
        if (spawnPos == null || judgePos == null) {
            Debug.LogWarning($"[NoteSpawner] Missing spawn/judge on lane {noteData.line}");
            return;
        }
        // choose prefab: tap-specific if available, otherwise normal prefab
        GameObject prefab = null;
        if (noteData.kind == NoteKind.Tap && tapNotePrefabs != null && tapNotePrefabs.Length == 6 && tapNotePrefabs[laneIdx] != null)
            prefab = tapNotePrefabs[laneIdx];
        else
            prefab = notePrefabs[laneIdx];

        if (prefab == null) {
            Debug.LogWarning($"[NoteSpawner] Missing prefab on lane {noteData.line} (kind={noteData.kind})");
            return;
        }
        GameObject goNote = Instantiate(prefab, spawnPos.position, Quaternion.identity);
        double hitDsp = songStartDspTime + (noteData.time + totalOffset);
        double appearDsp = hitDsp - leadTimeSec;
        Note noteComp = goNote.GetComponent<Note>();
        if (noteComp != null) {
            noteComp.lineNumber = noteData.line;
            noteComp.startPos = spawnPos.position;
            noteComp.judgePos = judgePos.position;
            noteComp.appearDspTime = appearDsp;
            noteComp.hitDspTime = hitDsp;
            noteComp.expectedHitTime = noteData.time;
            noteComp.chartIndex = chartIndex;
        }
        NoteVisuals.Register(chartIndex, goNote);
    }
    private void SpawnMeasuresIfNeeded(double songTimeSec, float totalOffset) {
        if (measureLinePrefab == null || measureSpawn == null || measureJudge == null) return;
        if (secPerBeat <= 0f || beatsPerMeasure <= 0) return;
        double measureDuration = secPerBeat * beatsPerMeasure;
        while (true) {
            double measureTime = nextMeasureIndex * measureDuration;
            double appearTime = measureTime - leadTimeSec;
            if (songTimeSec >= appearTime) {
                SpawnMeasureLine(measureTime, totalOffset);
                nextMeasureIndex++;
            } else break;
        }
    }
    private void SpawnMeasureLine(double measureTimeSec, float totalOffset) {
        GameObject go = Instantiate(measureLinePrefab, measureSpawn.position, Quaternion.identity);
        double hitDsp = songStartDspTime + (measureTimeSec + totalOffset);
        double appearDsp = hitDsp - leadTimeSec;
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
            tailNote.chartIndex = -1; // tail is visual-only
        }
        // BODY (between head and tail)
        if (longBodyPrefab != null) {
            GameObject body = Instantiate(longBodyPrefab, spawnPos.position, Quaternion.identity);
            LongNoteBody bodyComp = body.GetComponent<LongNoteBody>();
            if (bodyComp != null) {
                bodyComp.head = head.transform;
                bodyComp.tail = tail.transform;
                bodyComp.judge = judgePos;
                LongNoteBody.Register(chartIndex, bodyComp);
                //
                //
                //
#if UNITY_EDITOR
                Debug.Log($"[SpawnLong] lane={noteData.line} idx={chartIndex} bodyOK={bodyComp != null} tailOK={bodyComp.tail != null}");
#endif
                //
                //
                //
            }
            if (headNote != null) headNote.longBody = bodyComp;
        }
    }
}
