// file: JudgeTextController.cs
// written by Donghyeok Hahm

using UnityEngine;
using TMPro;
using System.Collections;

public class JudgeTextController : MonoBehaviour {
    public static JudgeTextController Instance { get; private set; }

    public TextMeshProUGUI judgeText;
    public float displayTime = 1f;
    private Coroutine currentCoroutine;

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start() {
        if (judgeText != null) judgeText.text = "";
    }

    public void ShowJudge(string result) {
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(ShowAndHide(result));
    }

    IEnumerator ShowAndHide(string result) {
        if (judgeText == null) yield break;
        judgeText.text = result;
        yield return new WaitForSeconds(displayTime);
        judgeText.text = "";
    }
}
