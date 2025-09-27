// file: NoteJudge.cs
// written by Donghyeok Hahm
// refactored: 250926

using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Renderer))]
public class NoteJudge : MonoBehaviour {
    public GameJudgeSettings judgeSettings;
    public int lineNumber = 1;

    private List<GameObject> notesInZone = new List<GameObject>();
    private Renderer rend;
    private Color defaultColor = Color.white;
    private Color pressedColor = new Color(0.5f, 1f, 0.9f); // mint

    void Start() {
        rend = GetComponent<Renderer>();
        if (rend != null) defaultColor = rend.material.color;
        NoteInputManager.RegisterJudge(this);
    }

    void Update() {
        CleanUpNotes();

        // 현 누르고 있는지 표시
        if (NoteInputManager.IsLineKeyHeld(lineNumber)) {
            rend.material.color = pressedColor;
        } else {
            rend.material.color = defaultColor;
        }
    }

    public void TryJudge() {
        if (notesInZone.Count == 0) {
            Debug.Log($"MISS! (No note in zone, Line {lineNumber})");
            return;
        }

        GameObject noteObj = notesInZone[0];
        Note note = noteObj.GetComponent<Note>();

        if (note != null && judgeSettings != null) {
            float songTime = NoteInputManager.AudioTime;
            float delta = Mathf.Abs(songTime - note.expectedHitTime);

            string result;
            if (delta <= judgeSettings.perfectTiming) result = "Rock!";
            else if (delta <= judgeSettings.goodTiming) result = "Good!";
            else result = "Miss!";

            Debug.Log($"{result}! (Line {lineNumber})");
            JudgeTextController.Instance?.ShowJudge(result);

            if (result != "Miss!") {
                notesInZone.RemoveAt(0);
                Destroy(noteObj);
            }
        }
    }

    void OnTriggerEnter(Collider other) {
        Note note = other.GetComponent<Note>();
        if (note != null && note.lineNumber == lineNumber) {
            notesInZone.Add(other.gameObject);
        }
    }

    void OnTriggerExit(Collider other) {
        Note note = other.GetComponent<Note>();
        if (note != null && note.lineNumber == lineNumber) {
            if (notesInZone.Contains(other.gameObject)) {
                notesInZone.Remove(other.gameObject);
                // 노트가 판정 없이 빠져나감 → MISS
                Debug.Log("Miss (Note left zone) Line " + lineNumber);
                JudgeTextController.Instance?.ShowJudge("Miss!");
            }
        }
    }

    void CleanUpNotes() {
        notesInZone.RemoveAll(note => note == null);
    }
}
