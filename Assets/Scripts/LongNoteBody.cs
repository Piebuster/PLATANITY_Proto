// file: LongNoteBody.cs
// Visual body between long note head and tail + tail color & lifetime

using UnityEngine;
using System.Collections.Generic;

public class LongNoteBody : MonoBehaviour {
    [Header("References")]
    public Transform head;   // head note transform
    public Transform tail;   // tail note transform
    public Transform judge;  // judge line (same lane)
    [Header("Body Settings")]
    public float minLength = 0.1f; // minimum visual length (world units)
    public float baseWidth = 0.5f; // width of one normal note (world units)
    [Header("Color Settings")]
    public Color activeColor = new Color(0.3f, 1f, 0.3f, 0.5f);
    public Color brokenColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
    Renderer bodyRenderer;
    Renderer tailRenderer;
    bool isBroken = false;
    bool finished = false;   // true after success or break

    int chartIndex = -1;
    static readonly Dictionary<int, LongNoteBody> registry = new Dictionary<int, LongNoteBody>();
    void Awake() {
        bodyRenderer = GetComponent<Renderer>();
        if (bodyRenderer != null) bodyRenderer.material.color = activeColor;
    }
    // ----- registry helpers -----
    public static void Register(int chartIdx, LongNoteBody body) {
        if (body == null) return;
        body.chartIndex = chartIdx;
        registry[chartIdx] = body;
    }
    public static LongNoteBody GetByChartIndex(int chartIdx) {
        registry.TryGetValue(chartIdx, out var body);
        return body;
    }
    public static void ClearRegistry() { registry.Clear(); }
    // ----- visual state changes -----
    public void SetBrokenVisual() {
        isBroken = true;
        finished = true;
        if (bodyRenderer == null) bodyRenderer = GetComponent<Renderer>();
        if (bodyRenderer != null) bodyRenderer.material.color = brokenColor;
        if (tail != null && tailRenderer == null) tailRenderer = tail.GetComponent<Renderer>();
        if (tailRenderer != null) tailRenderer.material.color = brokenColor;
    }
    public void OnLongSuccess() {
        finished = true;
#if UNITY_EDITOR
        Debug.Log($"[LongBody] SUCCESS chartIndex={chartIndex}, tailNull={(tail == null)}");
#endif
        if (tail != null) Destroy(tail.gameObject);
        registry.Remove(chartIndex);
        Destroy(gameObject);
    }
    // ----- body positioning / clipping -----
    void Update() {
        if (head == null || tail == null) {
            Destroy(gameObject);
            return;
        }
        Vector3 hp = head.position;
        Vector3 tp = tail.position;
        // travel direction is X axis (right Å® left)
        float left = Mathf.Min(hp.x, tp.x);
        float right = Mathf.Max(hp.x, tp.x);
        // clip part that already passed judge line (only while active)
        if (judge != null && !isBroken) {
            float jx = judge.position.x;
            left = Mathf.Max(left, jx);
        }
        // after success/break, when whole body passed judge line, we can safely destroy
        if (finished && judge != null) {
            float jx = judge.position.x;
            if (right <= jx + 0.001f) {
                Destroy(gameObject);
                return;
            }
        }
        // center between visible left/right
        float centerX = (left + right) * 0.5f;
        transform.position = new Vector3(centerX, hp.y, hp.z);
        // visible length minus one note width (head half + tail half)
        float inner = right - left - baseWidth;
        inner = Mathf.Max(inner, minLength);
        Vector3 s = transform.localScale;
        s.x = inner;
        transform.localScale = s;
    }
}