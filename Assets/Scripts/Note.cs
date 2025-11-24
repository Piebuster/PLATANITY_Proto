// file: Note.cs
// Visual-only note that moves from spawn to judge by DSP time.
// Judge is time-based elsewhere; this script does NOT decide hits.
using UnityEngine;
public class Note : MonoBehaviour {
    [Header("Visual Path (copied at spawn)")]
    public Vector3 startPos;
    public Vector3 judgePos;
    [Header("Timing (DSP absolute seconds)")]
    public double appearDspTime;
    public double hitDspTime;
    [Header("Meta (optional/debug)")]
    public int lineNumber = 1;
    public float expectedHitTime;
    [Header("Registry Key")]
    public int chartIndex;
    [Header("Long Note Link")]
    public bool isLongHead = false;
    public LongNoteBody longBody;
    private const float DespawnLateMargin = 5.0f;
    void Update() {
        double now = AudioSettings.dspTime;
        double denom = (hitDspTime - appearDspTime);
        float t = (denom > 1e-6) ? (float)((now - appearDspTime) / denom) : 1f;
        transform.position = Vector3.LerpUnclamped(startPos, judgePos, t);
        if (now > hitDspTime + DespawnLateMargin) {
            Destroy(gameObject);
        }
    }
}
