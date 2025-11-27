// file: GameJudgeSettings.cs
// written by Donghyeok Hahm

using UnityEngine;

[CreateAssetMenu(fileName = "GameJudgeSettings", menuName = "PLATANITY/Judge Settings")]
public class GameJudgeSettings : ScriptableObject {
    [Header("Judge Time Range (sec)")]
    public float perfectTiming = 0.070f;
    public float goodTiming = 0.090f;
}
