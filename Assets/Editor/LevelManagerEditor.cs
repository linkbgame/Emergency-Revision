// Assets/Editor/LevelManagerEditor.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelManager))]
public class LevelManagerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(6);
        GUILayout.Label("Editor Debug (Play Mode Only)", EditorStyles.boldLabel);

        LevelManager lm = (LevelManager)target;

        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying || lm == null);
        if (GUILayout.Button("Start Timer"))
        {
            lm.Editor_StartTimer();
        }
        if (GUILayout.Button("Pause Timer"))
        {
            lm.Editor_PauseTimer();
        }
        if (GUILayout.Button("Trigger Next TimedEvent"))
        {
            lm.Editor_TriggerNextTimedEvent();
        }
        if (GUILayout.Button("Force Win"))
        {
            lm.Editor_ForceWin();
        }
        if (GUILayout.Button("Force Fail"))
        {
            lm.Editor_ForceFail();
        }
        EditorGUI.EndDisabledGroup();

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("这些按钮仅在 Play 模式下可用（并通过 LevelManager 的 Editor 辅助方法执行）。", MessageType.Info);
        }
    }
}
#endif
