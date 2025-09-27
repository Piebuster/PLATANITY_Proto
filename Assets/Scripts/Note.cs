// file: Note.cs
// final stable option B (off-screen cleanup)

using UnityEngine;

public class Note : MonoBehaviour {
    [Header("Note Info")]
    public int lineNumber = 1;
    public float expectedHitTime;     // absolute audio time when note should be hit
    public float travelStartTime;     // absolute audio time when travel starts
    public float travelDuration;      // how long it takes from spawn to judge

    [Header("Positions (set by NoteSpawner)")]
    public Transform spawnPos;
    public Transform judgePos;

    [Header("Fallback")]
    public float speed = 5f;          // only used if positions are missing

    // off-screen cleanup threshold
    public float offscreenX = -50f;

    void Update() {
        if (spawnPos != null && judgePos != null && travelDuration > 0f) {
            float songTime = NoteInputManager.AudioTime;
            float t = (songTime - travelStartTime) / travelDuration;

            // use LerpUnclamped so notes keep moving past the judge line
            transform.position = Vector3.LerpUnclamped(spawnPos.position, judgePos.position, t);
        } else {
            // fallback: simple constant speed movement
            transform.Translate(Vector3.left * speed * Time.deltaTime);
        }

        // cleanup when far off-screen
        if (transform.position.x < offscreenX) {
            Destroy(gameObject);
        }
    }
}
