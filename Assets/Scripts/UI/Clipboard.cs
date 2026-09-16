using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Puts text on the system clipboard. <see cref="GUIUtility.systemCopyBuffer"/> never reaches the
/// browser's clipboard in a WebGL build, so there it goes through <c>Plugins/WebGL/Clipboard.jslib</c>.
/// </summary>
public static class Clipboard
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ClipboardCopy(string text);
#endif

    public static void Copy(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

#if UNITY_WEBGL && !UNITY_EDITOR
        ClipboardCopy(text);
#else
        GUIUtility.systemCopyBuffer = text;
#endif
    }
}
