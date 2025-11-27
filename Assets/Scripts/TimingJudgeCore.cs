// file: TimingJudgeCore.cs
// Purpose: Pure DSP-time-based judging core (no colliders).
// written by Donghyeok Hahm + GPT

using UnityEngine;
public enum HitResult { None, Rock, Good, Miss }

public class TimingJudgeCore : MonoBehaviour {
    private float TotalOffsetSec => (chart != null ? chart.globalOffset : 0f) + GameSettings.UserOffsetSec;
    public static TimingJudgeCore I { get; private set; }

    [Header("Long Note Options")]
    public float longEarlyReleaseHelper = 0.10f; // early-release grace (sec)

    [Header("Judge thresholds (seconds)")]
    public GameJudgeSettings judge;

    private Chart chart;
    private double songStartDspTime;

    // stroke-based notes (Normal / Long)
    private readonly int[] head = new int[6];
    // tap notes
    private readonly int[] headTap = new int[6];
    // mute notes
    private int headMute = 0;

    private class ActiveLong {
        public int chartIndex;
        public int lane;
        public double startTime;
        public double endTime;
        public HitResult startResult;
        public bool broken;
        public LongNoteBody body; // direct reference to body (and tail)
    }

    private readonly ActiveLong[] activeLong = new ActiveLong[6];
    private bool initialized = false;
    public bool Initialized => initialized;

    void Awake() {
        I = this;
        for (int i = 0; i < 6; i++) {
            head[i] = 0;
            headTap[i] = 0;
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
            headTap[i] = 0;
            activeLong[i] = null;
        }
        headMute = 0;
        LongNoteBody.ClearRegistry(); // safe reset
        initialized = (chart != null && chart.notes != null && judge != null);
#if UNITY_EDITOR
        Debug.Log($"[TimingJudgeCore] Init() ¨ judge={(judge != null)}, chart={(chart != null)}, notes={chart?.notes?.Length ?? 0}");
#endif
    }

    private double NowSong() {
        if (!initialized || chart == null) return 0.0;
        return (AudioSettings.dspTime - songStartDspTime) - TotalOffsetSec;
    }

    // helper: try to get body by chart index (fallback)
    private LongNoteBody FindBodyByChartIndex(int chartIndex) {
        GameObject go = NoteVisuals.Get(chartIndex);
        if (go != null) {
            Note note = go.GetComponent<Note>();
            if (note != null && note.longBody != null) return note.longBody;
        }
        return LongNoteBody.GetByChartIndex(chartIndex);
    }

    private void MarkLongBroken(int chartIndex, LongNoteBody bodyHint = null) {
        LongNoteBody body = bodyHint ?? FindBodyByChartIndex(chartIndex);
        if (body != null) body.SetBrokenVisual();
    }

    private void MarkLongSuccess(int chartIndex, LongNoteBody bodyHint = null) {
        LongNoteBody body = bodyHint ?? FindBodyByChartIndex(chartIndex);
        if (body != null) body.OnLongSuccess();
    }

    public void AutoMissSweep() {
        if (!initialized || judge == null || chart == null || chart.notes == null) return;
        double now = NowSong();

        // 1) Normal / Long (stroke-based lanes)
        for (int lane = 1; lane <= 6; lane++) {
            while (true) {
                int idx = NextStrokeIndex(lane);
                if (idx < 0) break;
                var n = chart.notes[idx];
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

        // 3) Tap notes
        for (int lane = 1; lane <= 6; lane++) {
            while (true) {
                int idx = NextTapIndex(lane);
                if (idx < 0) break;
                var n = chart.notes[idx];
                double lateness = now - n.time;
                if (lateness > judge.goodTiming) {
                    headTap[lane - 1] = idx + 1;
                    JudgeTextController.Instance?.ShowJudge(HitResult.Miss.ToString());
#if UNITY_EDITOR
                    Debug.Log($"[AutoMiss Tap] Lane={lane} idx={idx} ƒ¢={lateness * 1000.0:F1} ms");
#endif
                } else break;
            }
        }
    }

    // legacy (debug only)
    public (HitResult res, int idx, double ms) TryJudgeLane(int lane) {
        if (!initialized || chart == null || chart.notes == null || judge == null) return (HitResult.Miss, -1, 0.0);
        int idx = NextStrokeIndex(lane);
        if (idx < 0) return (HitResult.Miss, -1, 0.0);
        double delta = NowSong() - chart.notes[idx].time;
        double absDelta = System.Math.Abs(delta);
        HitResult result = (absDelta <= judge.perfectTiming) ? HitResult.Rock : (absDelta <= judge.goodTiming) ? HitResult.Good : HitResult.Miss;
        if (result != HitResult.Miss) head[lane - 1] = idx + 1;
        return (result, result == HitResult.Miss ? -1 : idx, delta * 1000.0);
    }

    private int NextStrokeIndex(int lane) {
        if (!initialized || chart == null || chart.notes == null) return -1;
        int i = head[lane - 1];
        while (i < chart.notes.Length) {
            var n = chart.notes[i];
            if (n.line == lane && (n.kind == NoteKind.Normal || n.kind == NoteKind.Long)) break;
            i++;
        }
        return (i < chart.notes.Length) ? i : -1;
    }

    private int NextTapIndex(int lane) {
        if (!initialized || chart == null || chart.notes == null) return -1;
        int i = headTap[lane - 1];
        while (i < chart.notes.Length) {
            var n = chart.notes[i];
            if (n.line == lane && n.kind == NoteKind.Tap) break;
            i++;
        }
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
        HitResult result = (absDelta <= judge.perfectTiming) ? HitResult.Rock : (absDelta <= judge.goodTiming) ? HitResult.Good : HitResult.Miss;
        if (result != HitResult.Miss) headMute = idx + 1;
        return (result, result == HitResult.Miss ? -1 : idx, delta * 1000.0);
    }

    public (HitResult res, int idx, double ms) TryJudgeLane(int lane, double inputDspTime) {
        if (!initialized || chart == null || chart.notes == null || judge == null) return (HitResult.Miss, -1, 0.0);
        int idx = NextStrokeIndex(lane);
        if (idx < 0) return (HitResult.Miss, -1, 0.0);
        var n = chart.notes[idx];
        double songTimeAtInput = (inputDspTime - songStartDspTime) - TotalOffsetSec;
        double delta = songTimeAtInput - n.time;
        double absDelta = System.Math.Abs(delta);
        HitResult result = (absDelta <= judge.perfectTiming) ? HitResult.Rock : (absDelta <= judge.goodTiming) ? HitResult.Good : HitResult.Miss;

        if (result != HitResult.Miss) {
            bool isLong = (n.kind == NoteKind.Long) && (n.endTime > n.time + 0.001f);
            if (isLong) {
                // capture body reference at the moment long starts
                LongNoteBody bodyRef = FindBodyByChartIndex(idx);
                activeLong[lane - 1] = new ActiveLong {
                    chartIndex = idx,
                    lane = lane,
                    startTime = n.time,
                    endTime = n.endTime,
                    startResult = result,
                    broken = false,
                    body = bodyRef
                };
#if UNITY_EDITOR
                Debug.Log($"[Long] Start lane={lane} idx={idx} t={n.time}~{n.endTime} res={result}, bodyNull={bodyRef == null}");
#endif
            }
            head[lane - 1] = idx + 1;
        }
        return (result, result == HitResult.Miss ? -1 : idx, delta * 1000.0);
    }

    public (HitResult res, int idx, double ms) TryJudgeTapLane(int lane, double inputDspTime) {
        if (!initialized || chart == null || chart.notes == null || judge == null) return (HitResult.None, -1, 0.0);
        int idx = NextTapIndex(lane);
        if (idx < 0) return (HitResult.None, -1, 0.0);
        var n = chart.notes[idx];
        double songTimeAtInput = (inputDspTime - songStartDspTime) - TotalOffsetSec;
        double delta = songTimeAtInput - n.time;
        double absDelta = System.Math.Abs(delta);
        if (absDelta > judge.goodTiming) return (HitResult.None, -1, delta * 1000.0);
        HitResult result = (absDelta <= judge.perfectTiming) ? HitResult.Rock : HitResult.Good;
        headTap[lane - 1] = idx + 1;
        return (result, idx, delta * 1000.0);
    }

    public void UpdateLongHold(int lane, bool isHeld) {
        if (!initialized || chart == null) return;
        if (lane < 1 || lane > 6) return;
        ActiveLong a = activeLong[lane - 1];
        if (a == null) return;

        double now = NowSong();
        if (now < a.startTime) return;

        // success / end
        if (now >= a.endTime) {
            if (!a.broken) {
                // prefer direct body ref; fall back to search
                MarkLongSuccess(a.chartIndex, a.body);
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

        // early release while still inside duration
        if (!isHeld && !a.broken) {
            double remaining = a.endTime - now;
            if (remaining > longEarlyReleaseHelper) {
                a.broken = true;
                MarkLongBroken(a.chartIndex, a.body);
#if UNITY_EDITOR
                Debug.Log($"[Long] BREAK lane={lane} idx={a.chartIndex} t={now:F3}");
#endif
            } else {
                // treat as on-time release (success) by shortening endTime
                a.endTime = now;
            }
        }
    }
}
