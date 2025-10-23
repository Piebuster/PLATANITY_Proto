// file: NoteInputManager.cs
// Purpose: Handle key inputs (strings + stroke) and ask TimingJudgeCore for judgement.
// Judge zones are no longer used for hit detection; only visuals remain.
using UnityEngine;
using System.Collections.Generic;
public class NoteInputManager : MonoBehaviour {
    public static AudioSource audioSource;
    public static float AudioTime => audioSource != null ? audioSource.time : 0f;
    // Line key mapping (string side)
    public static Dictionary<int, KeyCode> keyMap = new Dictionary<int, KeyCode> {
        {1, KeyCode.Y},
        {2, KeyCode.T},
        {3, KeyCode.R},
        {4, KeyCode.E},
        {5, KeyCode.W},
        {6, KeyCode.Q},
    };
    private static Dictionary<int, NoteJudge> judges = new Dictionary<int, NoteJudge>();
    public static void RegisterJudge(NoteJudge judge) {
        if (!judges.ContainsKey(judge.lineNumber))
            judges.Add(judge.lineNumber, judge);
    }
    public static bool IsLineKeyHeld(int line) {
        return keyMap.ContainsKey(line) && Input.GetKey(keyMap[line]);
    }
    void Update() {
        // --- 1) sweep overdue notes each frame (auto-miss)
        TimingJudgeCore.I?.AutoMissSweep();
        // --- 2) judge only when the stroke key (Space) is pressed
        if (Input.GetKeyDown(KeyCode.Space)) {
            foreach (var pair in keyMap) {
                int line = pair.Key;
                // Judge only if this string key is currently held
                if (IsLineKeyHeld(line)) {
                    var (res, idx, ms) = TimingJudgeCore.I.TryJudgeLane(line);

                    string msg = res == HitResult.Rock ? "Rock!"
                               : res == HitResult.Good ? "Good!"
                               : "Miss!";
                    Debug.Log($"{msg} (Lane {line})  ƒ¢={ms:F1} ms  idx={idx}");
                    JudgeTextController.Instance?.ShowJudge(msg);
                    // Remove the visual on successful hit
                    if (idx >= 0)
                        NoteVisuals.Despawn(idx);
                }
            }
        }
    }
}