// file: Chart.cs

using UnityEngine;

[CreateAssetMenu(fileName = "NewChart", menuName = "PLATANITY/Chart")]
public class Chart : ScriptableObject {
    public string songName;
    public AudioClip song;
    public float bpm = 120f;
    public float globalOffset;
    public NoteData[] notes;
}
