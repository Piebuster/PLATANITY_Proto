// file: NoteData.cs
// written by Donghyeok Hahm

using UnityEngine;
public enum NoteKind {
    Normal,
    Long,
    Mute,    // mute stroke
    Tap
}
[System.Serializable]
public class NoteData {
    public float time;   // absolute time(sec) based on audio start time
    public float endTime; // for longnote (if doesn't exist, handle as single note)
    public int line;     // 1..6 (line number)
    public NoteKind kind = NoteKind.Normal;   
}
