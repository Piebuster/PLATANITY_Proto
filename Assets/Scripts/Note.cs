// file: Note.cs
// Purpose: Visual-only note that moves from spawn to judge by DSP time.
// Judge is time-based elsewhere; this script does NOT decide hits.

using UnityEngine;
public class Note : MonoBehaviour {
    [Header("Visual Path (copied at spawn)")]
    public Vector3 startPos;                 // spawn position
    public Vector3 judgePos;                 // judge line position
    [Header("Timing (DSP absolute seconds)")]
    public double appearDspTime;             // when the note should start moving
    public double hitDspTime;                // when the note should be hit
    [Header("Meta (optional/debug)")]
    public int lineNumber = 1;               // lane 1..6
    public float expectedHitTime;            // song-time seconds (for logs/UI)
    [Header("Registry Key")]
    public int chartIndex;                   // ★ added: chart index for visuals map
    // Safety: auto-despawn after some time past the hit moment
    private const float DespawnLateMargin = 5.0f; // seconds after hit to auto-despawn
    void Update() {
        // Current absolute time from audio DSP clock
        double now = AudioSettings.dspTime;
        // Normalize progress from appear -> hit (can overshoot after 1.0)
        double denom = (hitDspTime - appearDspTime);
        float t = (denom > 1e-6)
            ? (float)((now - appearDspTime) / denom)
            : 1f;
        // Move along the line; allow overshoot past the judge line for nicer feel
        // (no Clamp01 — paired with LerpUnclamped)
        transform.position = Vector3.LerpUnclamped(startPos, judgePos, t);
        // Auto-despawn a bit after the judged moment if still present
        if (now > hitDspTime + DespawnLateMargin) {
            Destroy(gameObject);
        }
    }
}
