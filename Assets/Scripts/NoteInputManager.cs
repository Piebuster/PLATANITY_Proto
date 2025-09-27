// file: NoteInputManager.cs
// stroke mechanic implementation

using UnityEngine;
using System.Collections.Generic;

public class NoteInputManager : MonoBehaviour {
    public static AudioSource audioSource;
    public static float AudioTime => audioSource != null ? audioSource.time : 0f;

    // line key mapping (string side)
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
        if (!judges.ContainsKey(judge.lineNumber)) {
            judges.Add(judge.lineNumber, judge);
        }
    }

    public static bool IsLineKeyHeld(int line) {
        return keyMap.ContainsKey(line) && Input.GetKey(keyMap[line]);
    }

    void Update() {
        // Judge only when Space is pressed down
        if (Input.GetKeyDown(KeyCode.Space)) {
            foreach (var pair in judges) {
                int line = pair.Key;

                // Judge only if the string key (line key) is currently held
                if (IsLineKeyHeld(line)) {
                    pair.Value.TryJudge();
                }
            }
        }
    }
}
