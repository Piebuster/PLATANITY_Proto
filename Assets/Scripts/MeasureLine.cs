// file name : MeasureLine.cs
// written by donghyeok hahm + GPT
// only for measure line (no judge)

using UnityEngine;

public class MeasureLine : MonoBehaviour {
    [Header("Positions")]
    public Vector3 startPos;   
    public Vector3 judgePos;   
    [Header("Timing (DSP)")]
    public double appearDspTime;   
    public double hitDspTime;      // DSP time that arrive to judgezone
    // after life passing judgeline
    public float lifeAfterHit = 0.5f;
    // for caculating speed
    bool inited = false;
    Vector3 moveDir;   // direction
    float moveSpeed;   // move speed
    void InitIfNeeded() {
        if (inited) return;
        double duration = hitDspTime - appearDspTime;
        if (duration <= 0.0001) {
            moveDir = Vector3.down;
            moveSpeed = 0f;
        } else {
            Vector3 delta = judgePos - startPos;
            float distance = delta.magnitude;
            moveDir = delta.normalized;               
            moveSpeed = distance / (float)duration;   
        }
        inited = true;
    }
    void Update() {
        InitIfNeeded();
        double now = AudioSettings.dspTime;
        // stop before appear
        if (now < appearDspTime) {
            transform.position = startPos;
            return;
        }
        // 1) startPos ¨ judgePos area
        if (now <= hitDspTime) {
            double duration = hitDspTime - appearDspTime;
            float t = 0f;
            if (duration > 0.0001) {
                t = Mathf.Clamp01((float)((now - appearDspTime) / duration));
            }
            transform.position = Vector3.Lerp(startPos, judgePos, t);
            return;
        }
        // 2) area that pass judgeline
        double after = now - hitDspTime;          // time after pass judgeline
        float extraDist = moveSpeed * (float)after;
        transform.position = judgePos + moveDir * extraDist;
        // lifeAfterHit sec pass -> destroy
        if (after >= lifeAfterHit) {
            Destroy(gameObject);
        }
    }
}
