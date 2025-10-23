// file: TimingJudgeCore.cs
// Purpose: Pure DSP-time-based judging core (no colliders).
//  - Compares AudioSettings.dspTime vs chart note times (seconds)
//  - Maintains per-lane "head" pointers for next unhit note
//  - Handles auto-miss logic only (visuals are managed by NoteVisuals & Note.cs)
//
// How to use:
//  1) Place this on an empty GameObject in the scene.
//  2) Assign GameJudgeSettings in the inspector.
//  3) After AudioSource.PlayScheduled(), call Init(chart, songStartDspTime).
//  4) Call AutoMissSweep() every frame (from Update).
//  5) On input, call TryJudgeLane(lane) to evaluate hits.
//
// written by Donghyeok Hahm + GPT
// updated: 2025-10-21 (refactored & cleaned)

using UnityEngine;
public enum HitResult { None, Rock, Good, Miss }
public class TimingJudgeCore : MonoBehaviour {
    // ---- Singleton ---------------------------------------------------------
    public static TimingJudgeCore I { get; private set; }
    // ---- References --------------------------------------------------------
    [Header("Judge thresholds (seconds)")]
    public GameJudgeSettings judge;    // perfect/good windows
    private Chart chart;               // sorted note data
    private double songStartDspTime;   // DSP time when playback begins
    // ---- Internal state ----------------------------------------------------
    private readonly int[] head = new int[6];  // per-lane next note index
    private bool initialized = false;
    public bool Initialized => initialized;
    // ---- Lifecycle ---------------------------------------------------------
    void Awake() {
        I = this;
        for (int i = 0; i < 6; i++) head[i] = 0;
        initialized = false;
    }
    /// <summary>Initialize judging core (called by NoteSpawner).</summary>
    public void Init(Chart chartRef, double dspStartTime) {
        chart = chartRef;
        songStartDspTime = dspStartTime;
        for (int i = 0; i < 6; i++) head[i] = 0;
        initialized = (chart != null && chart.notes != null && judge != null);
#if UNITY_EDITOR
        Debug.Log($"[TimingJudgeCore] Init() Å® judge={(judge != null)}, chart={(chart != null)}, notes={chart?.notes?.Length ?? 0}");
#endif
    }
    // ---- Core Time Reference ----------------------------------------------
    /// <summary>
    /// Returns current song position (seconds) relative to chart start,
    /// synchronized with DSP clock and including chart.globalOffset.
    /// </summary>
    private double NowSong() {
        if (!initialized || chart == null)
            return 0.0;
        return (AudioSettings.dspTime - songStartDspTime) - chart.globalOffset;
    }
    // ---- Automatic Miss Sweep ---------------------------------------------
    /// <summary>
    /// Marks overdue notes as missed logically (visuals remain until despawn).
    /// Should be called once per frame.
    /// </summary>
    public void AutoMissSweep() {
        if (!initialized || judge == null || chart == null || chart.notes == null)
            return;
        double now = NowSong();
        for (int lane = 1; lane <= 6; lane++) {
            while (true) {
                int idx = NextIndex(lane);
                if (idx < 0) break;
                var n = chart.notes[idx];
                double lateness = now - n.time; // + if late
                if (lateness > judge.goodTiming) {
                    head[lane - 1] = idx + 1;   // Åö FIX: consume the overdue note properly
#if UNITY_EDITOR
                    Debug.Log($"[AutoMiss] Lane={lane} idx={idx} É¢={lateness * 1000.0:F1} ms");
#endif
                } else break;
            }
        }
    }
    // ---- Player Input Judgement -------------------------------------------
    public (HitResult res, int idx, double ms) TryJudgeLane(int lane) {
        if (!initialized || chart == null || chart.notes == null || judge == null)
            return (HitResult.Miss, -1, 0.0);
        int idx = NextIndex(lane);
        if (idx < 0) return (HitResult.Miss, -1, 0.0);
        double delta = NowSong() - chart.notes[idx].time; // +late / -early
        double absDelta = System.Math.Abs(delta);
        HitResult result =
            (absDelta <= judge.perfectTiming) ? HitResult.Rock :
            (absDelta <= judge.goodTiming) ? HitResult.Good :
                                               HitResult.Miss;
        if (result != HitResult.Miss)
            head[lane - 1] = idx + 1;   // Åö FIX: advance to the real next index
        return (result, result == HitResult.Miss ? -1 : idx, delta * 1000.0);
    }
    // ---- Utility -----------------------------------------------------------
    /// <summary>Returns next unhit note index for a lane, or -1 if none.</summary>
    private int NextIndex(int lane) {
        if (!initialized || chart == null || chart.notes == null)
            return -1;
        int i = head[lane - 1];
        while (i < chart.notes.Length && chart.notes[i].line != lane)
            i++;
        return (i < chart.notes.Length) ? i : -1;
    }
}
