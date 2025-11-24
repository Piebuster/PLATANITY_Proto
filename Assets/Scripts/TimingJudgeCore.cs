// file: TimingJudgeCore.cs
// Purpose: Pure DSP-time-based judging core (no colliders).
// written by Donghyeok Hahm + GPT
using UnityEngine;
public enum HitResult { None, Rock, Good, Miss }
public class TimingJudgeCore : MonoBehaviour {
    private float TotalOffsetSec => (chart != null ? chart.globalOffset : 0f) + GameSettings.UserOffsetSec;
    public static TimingJudgeCore I { get; private set; }
    [Header("Judge thresholds (seconds)")]
    public GameJudgeSettings judge;
    private Chart chart;
    private double songStartDspTime;
    private readonly int[] head = new int[6];
    private int headMute = 0;
    private class ActiveLong {
        public int chartIndex;
        public int lane;
        public double startTime;
        public double endTime;
        public HitResult startResult;
        public bool broken;
    }
    private readonly ActiveLong[] activeLong = new ActiveLong[6];
    private bool initialized = false;
    public bool Initialized => initialized;
    void Awake() {
        I = this;
        for (int i = 0; i < 6; i++) {
            head[i] = 0;
            activeLong[i] = null;
        }
        headMute = 0;
        initialized = false;
    }
    public void Init(Chart chartRef, double dspStartTime) {
        chart = chartRef;
        songStartDspTime = dspStartTime;
        for (int i = 0; i < 6; i++) {
            head[i] = 0;
            activeLong[i] = null;
        }
        headMute = 0;
        initialized = (chart != null && chart.notes != null && judge != null);
#if UNITY_EDITOR
        Debug.Log($"[TimingJudgeCore] Init() ¨ judge={(judge != null)}, chart={(chart != null)}, notes={chart?.notes?.Length ?? 0}");
#endif
    }
    private double NowSong() {
        if (!initialized || chart == null) return 0.0;
        return (AudioSettings.dspTime - songStartDspTime) - TotalOffsetSec;
    }
    private void MarkLongBroken(int chartIndex) {
        GameObject go = NoteVisuals.Get(chartIndex);
        if (go == null) return;
        Note note = go.GetComponent<Note>();
        if (note == null || note.longBody == null) return;
        note.longBody.SetBrokenVisual();
    }
    public void AutoMissSweep() {
        if (!initialized || judge == null || chart == null || chart.notes == null) return;
        double now = NowSong();
        // 1) Normal / Long notes on lanes 1..6
        for (int lane = 1; lane <= 6; lane++) {
            while (true) {
                int idx = NextIndex(lane);
                if (idx < 0) break;
                var n = chart.notes[idx];
                if (n.kind == NoteKind.Mute) {
                    head[lane - 1] = idx + 1;
                    continue;
                }
                double lateness = now - n.time;
                if (lateness > judge.goodTiming) {
                    head[lane - 1] = idx + 1;
                    JudgeTextController.Instance?.ShowJudge(HitResult.Miss.ToString());
                    if (n.kind == NoteKind.Long) MarkLongBroken(idx);
#if UNITY_EDITOR
                    Debug.Log($"[AutoMiss] Lane={lane} idx={idx} ƒ¢={lateness * 1000.0:F1} ms");
#endif
                } else break;
            }
        }
        // 2) Mute notes
        while (true) {
            int idx = NextMuteIndex();
            if (idx < 0) break;
            var n = chart.notes[idx];
            double lateness = now - n.time;
            if (lateness > judge.goodTiming) {
                headMute = idx + 1;
                JudgeTextController.Instance?.ShowJudge(HitResult.Miss.ToString());
#if UNITY_EDITOR
                Debug.Log($"[AutoMiss Mute] idx={idx} ƒ¢={lateness * 1000.0:F1} ms");
#endif
            } else break;
        }
    }
    public (HitResult res, int idx, double ms) TryJudgeLane(int lane) {
        if (!initialized || chart == null || chart.notes == null || judge == null) return (HitResult.Miss, -1, 0.0);
        int idx = NextIndex(lane);
        if (idx < 0) return (HitResult.Miss, -1, 0.0);
        double delta = NowSong() - chart.notes[idx].time;
        double absDelta = System.Math.Abs(delta);
        HitResult result =
            (absDelta <= judge.perfectTiming) ? HitResult.Rock :
            (absDelta <= judge.goodTiming) ? HitResult.Good :
            HitResult.Miss;
        if (result != HitResult.Miss) head[lane - 1] = idx + 1;
        return (result, result == HitResult.Miss ? -1 : idx, delta * 1000.0);
    }
    private int NextIndex(int lane) {
        if (!initialized || chart == null || chart.notes == null) return -1;
        int i = head[lane - 1];
        while (i < chart.notes.Length && chart.notes[i].line != lane) i++;
        return (i < chart.notes.Length) ? i : -1;
    }
    private int NextMuteIndex() {
        if (!initialized || chart == null || chart.notes == null) return -1;
        int i = headMute;
        while (i < chart.notes.Length && chart.notes[i].kind != NoteKind.Mute) i++;
        return (i < chart.notes.Length) ? i : -1;
    }
    public (HitResult res, int idx, double ms) TryJudgeMute(double inputDspTime) {
        if (!initialized || chart == null || chart.notes == null || judge == null) return (HitResult.Miss, -1, 0.0);
        int idx = NextMuteIndex();
        if (idx < 0) return (HitResult.Miss, -1, 0.0);
        var n = chart.notes[idx];
        double songTimeAtInput = (inputDspTime - songStartDspTime) - TotalOffsetSec;
        double delta = songTimeAtInput - n.time;
        double absDelta = System.Math.Abs(delta);
        HitResult result =
            (absDelta <= judge.perfectTiming) ? HitResult.Rock :
            (absDelta <= judge.goodTiming) ? HitResult.Good :
            HitResult.Miss;
        if (result != HitResult.Miss) headMute = idx + 1;
        return (result, result == HitResult.Miss ? -1 : idx, delta * 1000.0);
    }
    public (HitResult res, int idx, double ms) TryJudgeLane(int lane, double inputDspTime) {
        if (!initialized || chart == null || chart.notes == null || judge == null) return (HitResult.Miss, -1, 0.0);
        int idx = NextIndex(lane);
        if (idx < 0) return (HitResult.Miss, -1, 0.0);
        var n = chart.notes[idx];
        if (n.kind == NoteKind.Mute) return (HitResult.Miss, -1, 0.0);
        double songTimeAtInput = (inputDspTime - songStartDspTime) - TotalOffsetSec;
        double delta = songTimeAtInput - n.time;
        double absDelta = System.Math.Abs(delta);
        HitResult result =
            (absDelta <= judge.perfectTiming) ? HitResult.Rock :
            (absDelta <= judge.goodTiming) ? HitResult.Good :
            HitResult.Miss;
        if (result != HitResult.Miss) {
            bool isLong = (n.kind == NoteKind.Long) && (n.endTime > n.time + 0.001f);
            if (isLong) {
                activeLong[lane - 1] = new ActiveLong {
                    chartIndex = idx,
                    lane = lane,
                    startTime = n.time,
                    endTime = n.endTime,
                    startResult = result,
                    broken = false
                };
#if UNITY_EDITOR
                Debug.Log($"[Long] Start lane={lane} idx={idx} t={n.time}~{n.endTime} res={result}");
#endif
            }
            head[lane - 1] = idx + 1;
        }
        return (result, result == HitResult.Miss ? -1 : idx, delta * 1000.0);
    }
    public void UpdateLongHold(int lane, bool isHeld) {
        if (!initialized || chart == null) return;
        if (lane < 1 || lane > 6) return;
        ActiveLong a = activeLong[lane - 1];
        if (a == null) return;
        double now = NowSong();
        if (now < a.startTime) return;
        if (now >= a.endTime) {
            if (!a.broken) {
#if UNITY_EDITOR
                Debug.Log($"[Long] SUCCESS lane={lane} idx={a.chartIndex} res={a.startResult}");
#endif
            } else {
#if UNITY_EDITOR
                Debug.Log($"[Long] FAILED (broken) lane={lane} idx={a.chartIndex}");
#endif
            }
            activeLong[lane - 1] = null;
            return;
        }
        if (!isHeld && !a.broken) {
            a.broken = true;
#if UNITY_EDITOR
            Debug.Log($"[Long] BREAK lane={lane} idx={a.chartIndex} t={now:F3}");
#endif
            MarkLongBroken(a.chartIndex);
        }
    }
}
