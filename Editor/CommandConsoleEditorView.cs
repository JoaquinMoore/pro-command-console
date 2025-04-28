using UnityEditor;

public class CommandConsoleEditorView : EditorWindow
{
#if UNITY_EDITOR
    /// <summary>
    /// Shows the console window.
    /// </summary>
    [MenuItem("Debug/Console")]
    public static void ShowWindow()
    {
        GetWindow(typeof(CommandConsoleEditorView));
    }

    private void OnGUI()
    {
        CommandConsole.OnGUI();
    }
#endif
}
