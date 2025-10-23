// file: NoteJudge.cs
// purpose: visual-only indicator for each lane (no collision logic)
// written by Donghyeok Hahm
// recent update: 251023

using UnityEngine;
[RequireComponent(typeof(Renderer))]
public class NoteJudge : MonoBehaviour {
    [Header("Line Number (1~6)")]
    public int lineNumber = 1;
    private Renderer rend;
    private Color defaultColor = Color.white;
    private Color pressedColor = new Color(0.5f, 1f, 0.9f); // mint tint
    void Start() {
        // Cache the renderer and remember its original color
        rend = GetComponent<Renderer>();
        if (rend != null)
            defaultColor = rend.material.color;
    }
    void Update() {
        // Highlight when the string key is pressed(just visual)
        if (NoteInputManager.IsLineKeyHeld(lineNumber)) {
            rend.material.color = pressedColor;
        } else {
            rend.material.color = defaultColor;
        }
    }
}