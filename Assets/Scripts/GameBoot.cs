// file: GameBoot.cs
// Initial setting for whole game(global)
using UnityEngine;

public class GameBoot : MonoBehaviour {
    [SerializeField] int targetFps = 60;   // change fps here(if want to fit to monitor refrest, -1)
    void Awake() {  
        QualitySettings.vSyncCount = 1;    
        Application.targetFrameRate = targetFps;  
    }
}