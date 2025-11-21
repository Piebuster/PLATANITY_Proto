using TMPro;
using UnityEngine;

public class OffsetSettingUI : MonoBehaviour {
    [Header("UI")]
    public TextMeshProUGUI valueText;   // display text number
    [Header("Config (ms)")]
    public int stepMs = 1;              // one click +/- amount 
    public int minMs = -2000;           // minimum
    public int maxMs = 2000;            // maximum
    // current display value (not global, just for this pannel)
    private int currentMs = 0;

    void Start() {
        // synchronization(get value from GameSettings) when start
        currentMs = GameSettings.UserOffsetMs;
        RefreshText();
    }
    // + button
    public void AddOffset() {
        SetOffset(currentMs + stepMs);
    }
    // - button
    public void SubOffset() {
        SetOffset(currentMs - stepMs);
    }
    private void SetOffset(int newValue) {
        // 1) cut in range
        currentMs = Mathf.Clamp(newValue, minMs, maxMs);
        // 2) apply value to GameSettings(global)
        GameSettings.SetUserOffsetMs(currentMs);
        // 3) refresh display number
        RefreshText();
        Debug.Log($"[OFFSET UI] currentMs={currentMs}, GameSettings.UserOffsetMs={GameSettings.UserOffsetMs}, Sec={GameSettings.UserOffsetSec}");
    }
    private void RefreshText() {
        if (valueText != null) {
            valueText.text = currentMs.ToString();
        }
    }
}
