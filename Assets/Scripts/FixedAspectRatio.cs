// file: FixedAspectRatio.cs
// Force the camera viewport to maintain a fixed aspect ratio (e.g., 16:9)
// written by Donghyeok Hahm + GPT
// recent update: 251020

using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FixedAspectRatio : MonoBehaviour {
    [Header("Target Aspect Ratio (e.g., 16:9 = 1.7777)")]
    public float targetAspect = 16f / 9f;
    private Camera cam;
    void Start() {
        cam = GetComponent<Camera>();
        UpdateViewport();
    }
    void Update() {
        // Keep checking while resizing window in editor or runtime
        UpdateViewport();
    }
    private void UpdateViewport() {
        float windowAspect = (float)Screen.width / Screen.height;
        float scaleHeight = windowAspect / targetAspect;
        Rect rect = cam.rect;
        if (scaleHeight < 1.0f) {
            // Add letterbox (black bars top and bottom)
            rect.width = 1.0f;
            rect.height = scaleHeight;
            rect.x = 0;
            rect.y = (1.0f - scaleHeight) / 2.0f;
        } else {
            // Add pillarbox (black bars on left and right)
            float scaleWidth = 1.0f / scaleHeight;
            rect.width = scaleWidth;
            rect.height = 1.0f;
            rect.x = (1.0f - scaleWidth) / 2.0f;
            rect.y = 0;
        }
        cam.rect = rect;
    }
}
