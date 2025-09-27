// file: NoteData.cs
// written by Donghyeok Hahm

using UnityEngine;

[System.Serializable]
public class NoteData {
    public float time;   // 오디오 시작 기준 절대 시간(초)
    public int line;     // 1..6 (라인 번호)
}
