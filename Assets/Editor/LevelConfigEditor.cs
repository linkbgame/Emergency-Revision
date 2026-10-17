// Assets/Editor/LevelConfigEditor.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.Linq;

[CustomEditor(typeof(LevelConfig))]
public class LevelConfigEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        GUILayout.Space(8);
        if (GUILayout.Button("Load Level (Editor)"))
        {
            var cfg = (LevelConfig)target;
            if (!string.IsNullOrEmpty(cfg.sceneName))
            {
                // 尝试根据 sceneName 在 Build Settings 中找到对应的场景路径
                var sceneEntry = EditorBuildSettings.scenes
                    .FirstOrDefault(s => !string.IsNullOrEmpty(s.path) && s.path.EndsWith(cfg.sceneName + ".unity"));

                if (sceneEntry != null)
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        EditorSceneManager.OpenScene(sceneEntry.path);
                    }
                }
                else
                {
                    // 作为降级：尝试在 Assets/Scenes 下打开 sceneName.unity（常见约定）
                    string fallback = $"Assets/Scenes/{cfg.sceneName}.unity";
                    var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(fallback);
                    if (asset != null)
                    {
                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        {
                            EditorSceneManager.OpenScene(fallback);
                        }
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Load Level", $"无法在 Build Settings 中找到场景名 '{cfg.sceneName}'。\n请确认 sceneName 是否正确并已加入 Build Settings，或将场景放置到 Assets/Scenes/ 并命名为 {cfg.sceneName}.unity。", "OK");
                    }
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Load Level", "LevelConfig.sceneName 为空，请先配置 sceneName 字段。", "OK");
            }
        }
    }
}
#endif
