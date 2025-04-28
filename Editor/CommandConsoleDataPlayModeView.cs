using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;


public class CommandConsoleDataPlayModeView : ScriptableObject
{
    public class CommandConsolePlayModeView : MonoBehaviour
    {
        public event System.Action onGuiEvent;

        public CommandConsoleDataPlayModeView data;

        private bool isActive;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Keyboard.current[data.consoleKey].wasPressedThisFrame)
            {
                isActive = !isActive;
            }
        }

        private void OnGUI()
        {
            if (!isActive)
                return;

            onGuiEvent?.Invoke();
        }
    }


    [SerializeField]
    private Key consoleKey;

    private static CommandConsolePlayModeView instanceInScene;

    private static CommandConsoleDataPlayModeView instance;

#if UNITY_EDITOR
    [MenuItem("Debug/Create SO")]
    public static void Create()
    {
        var ret = CreateInstance<CommandConsoleDataPlayModeView>();

        if (!AssetDatabase.GetSubFolders("Assets").Any(s => s == "Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        AssetDatabase.CreateAsset(ret, "Assets/Resources/CommandConsoleData.asset");

        EditorUtility.SetDirty(ret);
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        instance = Resources.Load<CommandConsoleDataPlayModeView>("CommandConsoleData");

        if (instance == null)
            return;

        var go = new GameObject("OnGuiEventHandler");

        instanceInScene = go.AddComponent<CommandConsolePlayModeView>();

        instanceInScene.data = instance;

        instanceInScene.onGuiEvent += CommandConsole.OnGUI;
    }
}
