// file: GameSettings.cs
// written by Donghyeok Hahm+ GPT
public static class GameSettings {
    // user offset(ms), defalult == 0
    public static int UserOffsetMs { get; private set; } = 0;
    // judge needs second based system, so converting
    public static float UserOffsetSec => UserOffsetMs / 1000f;
    // we will use this function after, push +/- button on UI
    public static void SetUserOffsetMs(int ms) {
        UserOffsetMs = ms;

        // add playerPrefs save data after, here
    }
}

