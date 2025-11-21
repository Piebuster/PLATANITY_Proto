// file: AudioCalibrator.cs
// Purpose: calibrating SongPos-Audiotime (because DSP is faster than actual audio time)
// written by donghyeok hahm + gpt

using UnityEngine;

public class AudioCalibrator : MonoBehaviour {
    public AudioSource audioSource;
    private double scheduledDsp;

    private bool started = false;

    private int sampleCount = 0;
    private double sumError = 0;
    private bool collected = false;

    void Start() {
        scheduledDsp = AudioSettings.dspTime + 1.0;
        /////////////////////audioSource.PlayScheduled(scheduledDsp);

        Debug.Log($"[CALIB] Scheduled DSP = {scheduledDsp:F6}");
    }
    void Update() {
        double now = AudioSettings.dspTime;
        // detect real audio start play time
        if (!started && audioSource.timeSamples > 0) {
            started = true;
            Debug.Log("[CALIB] Audio started");
        }
        if (!started) return;
        double songPos = now - scheduledDsp;
        double audioPos = (double)audioSource.timeSamples / audioSource.clip.frequency;
        double diff = songPos - audioPos;
        if (songPos >= 0 && songPos < 0.5) {
            sampleCount++;
            sumError += diff;
        } else if (!collected && songPos >= 0.5) {
            collected = true;
            double avg = sumError / sampleCount;
            Debug.Log($"[CALIB RESULT] Average Sync Error = {avg:F6} sec");
        }
    }
}