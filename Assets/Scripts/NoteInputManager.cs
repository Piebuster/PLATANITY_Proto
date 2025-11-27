// file: NoteInputManager.cs
// Purpose: DSP-timestamped input buffer system for precise timing.
// All judgement requests are synchronized with Audio DSP clock.
// written by Donghyeok Hahm + GPT
// updated: 2025-11-27 (added Tap note)

using UnityEngine;
using System.Collections.Generic;

public class NoteInputManager : MonoBehaviour {
    public static AudioSource audioSource;
    public static float AudioTime => audioSource != null ? audioSource.time : 0f;
    public enum ControlMode { Desk, Performance }
    public static ControlMode currentMode = ControlMode.Desk;
    // Lane key mapping (string side)
    private static readonly Dictionary<int, KeyCode[]> keyLayouts = new Dictionary<int, KeyCode[]> {
        {1, new KeyCode[]{ KeyCode.Y, KeyCode.Alpha1 }},
        {2, new KeyCode[]{ KeyCode.T, KeyCode.Alpha2 }},
        {3, new KeyCode[]{ KeyCode.R, KeyCode.Alpha3 }},
        {4, new KeyCode[]{ KeyCode.E, KeyCode.Alpha4 }},
        {5, new KeyCode[]{ KeyCode.W, KeyCode.Alpha5 }},
        {6, new KeyCode[]{ KeyCode.Q, KeyCode.Alpha6 }},
    };
    private struct InputStamp {
        public int lane;      // 1..6 = lanes, 0 = mute stroke
        public double dspTime;
        public bool isTap;    // true = tap note, false = stroke/mute
    }
    private readonly List<InputStamp> inputBuffer = new List<InputStamp>();
    private const double bufferLife = 0.10; // keep input valid for 100 ms
    void Update() {
        // toggle control mode
        if (Input.GetKeyDown(KeyCode.Tab)) {
            currentMode = (currentMode == ControlMode.Desk) ? ControlMode.Performance : ControlMode.Desk;
            Debug.Log($"[NoteInputManager] Mode switched Å® {currentMode}");
        }
        // auto-miss
        TimingJudgeCore.I?.AutoMissSweep();
        if (TimingJudgeCore.I == null || !TimingJudgeCore.I.Initialized) return;
        double dspNow = AudioSettings.dspTime;
        // long note hold status
        bool strokeHeld = IsStrokeHeld();
        for (int lane = 1; lane <= 6; lane++) {
            bool lineHeld = IsLineKeyHeld(lane);
            bool isHolding = lineHeld && strokeHeld;
            TimingJudgeCore.I.UpdateLongHold(lane, isHolding);
        }
        // stroke pressed Å® Normal / Long / Mute
        if (IsStrokePressed()) {
            bool anyHeld = false;
            foreach (var pair in keyLayouts) {
                int lane = pair.Key;
                if (IsLineKeyHeld(lane)) {
                    anyHeld = true;
                    inputBuffer.Add(new InputStamp { lane = lane, dspTime = dspNow, isTap = false });
                }
            }
            if (!anyHeld) {
                // pure stroke Å® mute
                inputBuffer.Add(new InputStamp { lane = 0, dspTime = dspNow, isTap = false });
            }
        }
        // tap input Å® Tap notes (no stroke)
        for (int lane = 1; lane <= 6; lane++) {
            if (IsLineKeyDown(lane)) {
                inputBuffer.Add(new InputStamp { lane = lane, dspTime = dspNow, isTap = true });
            }
        }
        // process buffered inputs
        for (int i = inputBuffer.Count - 1; i >= 0; i--) {
            var s = inputBuffer[i];
            HitResult res;
            int idx;
            double ms;
            if (s.isTap) {
                (res, idx, ms) = TimingJudgeCore.I.TryJudgeTapLane(s.lane, s.dspTime);
            } else {
                if (s.lane == 0) (res, idx, ms) = TimingJudgeCore.I.TryJudgeMute(s.dspTime);
                else (res, idx, ms) = TimingJudgeCore.I.TryJudgeLane(s.lane, s.dspTime);
            }
            if (res != HitResult.Miss && res != HitResult.None && idx >= 0)
                NoteVisuals.Despawn(idx);
            if (res != HitResult.None)
                JudgeTextController.Instance?.ShowJudge(res.ToString());
            // remove when expired or judged (including Miss)
            if (dspNow - s.dspTime > bufferLife || res != HitResult.Miss)
                inputBuffer.RemoveAt(i);
        }
    }
    // --- Input helpers ---
    public static bool IsLineKeyHeld(int line) {
        if (!keyLayouts.ContainsKey(line)) return false;
        KeyCode key = (currentMode == ControlMode.Desk) ? keyLayouts[line][0] : keyLayouts[line][1];
        return Input.GetKey(key);
    }
    private static bool IsLineKeyDown(int line) {
        if (!keyLayouts.ContainsKey(line)) return false;
        KeyCode key = (currentMode == ControlMode.Desk) ? keyLayouts[line][0] : keyLayouts[line][1];
        return Input.GetKeyDown(key);
    }
    private bool IsStrokePressed() {
        if (currentMode == ControlMode.Desk) return Input.GetKeyDown(KeyCode.Space);
        else return Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }
    private static bool IsStrokeHeld() {
        if (currentMode == ControlMode.Desk) return Input.GetKey(KeyCode.Space);
        else return Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
    }
}
