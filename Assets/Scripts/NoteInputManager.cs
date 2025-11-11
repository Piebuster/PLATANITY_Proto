// file: NoteInputManager.cs
// Purpose: DSP-timestamped input buffer system for precise timing.
// All judgement requests are synchronized with Audio DSP clock.
// written by Donghyeok Hahm + GPT
// updated: 2025-10-22

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
        public int lane;
        public double dspTime;
    }
    // Input buffer list (short lifetime)
    private readonly List<InputStamp> inputBuffer = new List<InputStamp>();
    private const double bufferLife = 0.10; // keep input valid for 100 ms
    void Update() {
        // --- Toggle mode (Tab key) ---
        if (Input.GetKeyDown(KeyCode.Tab)) {
            currentMode = (currentMode == ControlMode.Desk)
                ? ControlMode.Performance
                : ControlMode.Desk;
            Debug.Log($"[NoteInputManager] Mode switched ¨ {currentMode}");
        }
        // --- Auto miss ---
        TimingJudgeCore.I?.AutoMissSweep();
        if (TimingJudgeCore.I == null || !TimingJudgeCore.I.Initialized)
            return;
        double dspNow = AudioSettings.dspTime;
        // --- Record keydown events with DSP timestamp ---
        if (IsStrokePressed()) {
            foreach (var pair in keyLayouts) {
                int lane = pair.Key;
                if (IsLineKeyHeld(lane)) {
                    inputBuffer.Add(new InputStamp {
                        lane = lane,
                        dspTime = dspNow
                    });
                }
            }
        }
        // --- Process buffered inputs ---
        for (int i = inputBuffer.Count - 1; i >= 0; i--) {
            var s = inputBuffer[i];
            var (res, idx, ms) = TimingJudgeCore.I.TryJudgeLane(s.lane, s.dspTime);
            if (res != HitResult.Miss && idx >= 0)
                NoteVisuals.Despawn(idx);
            if (res != HitResult.None)
                JudgeTextController.Instance?.ShowJudge(res.ToString());
            if (dspNow - s.dspTime > bufferLife || res != HitResult.Miss)
                inputBuffer.RemoveAt(i);
        }
    }
    // --- Input helpers ---
    public static bool IsLineKeyHeld(int line) {
        if (!keyLayouts.ContainsKey(line)) return false;
        KeyCode key = (currentMode == ControlMode.Desk)
            ? keyLayouts[line][0]
            : keyLayouts[line][1];
        return Input.GetKey(key);
    }
    private bool IsStrokePressed() {
        if (currentMode == ControlMode.Desk)
            return Input.GetKeyDown(KeyCode.Space);
        else
            return Input.GetKeyDown(KeyCode.RightShift)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter);
    }
}