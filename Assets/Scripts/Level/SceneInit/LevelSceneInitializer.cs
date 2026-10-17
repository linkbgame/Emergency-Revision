using UnityEngine;

/// <summary>
/// 场景级初始化器（放在每个 Level Scene 中）。
/// 在 Awake/Start 时把本场景的对象注册到常驻的 LevelManager。
/// 方便常驻 LevelManager 在场景切换后重新绑定引用。
/// </summary>
public class LevelSceneInitializer : MonoBehaviour
{
    [Header("Scene-local references (drag these in the Inspector)")]
    public PlayerController playerController;
    public LevelTimer levelTimer;
    public HUD hud;
    public PauseMenu pauseMenu;
    public ResultUI resultUI;
    public CutsceneManager cutsceneManager;

    // 如果你在场景里还有其他 scene-local 对象，也可以在这里扩展

    private void Start()
    {
        // 尝试把引用注册到 LevelManager（如果还没创建 LevelManager，则等待下一帧再试）
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.RegisterSceneObjects(playerController, levelTimer, hud, pauseMenu, resultUI, cutsceneManager);
        }
        else
        {
            // 保险起见（极小概率：LevelManager 尚未创建），延迟一帧再试
            StartCoroutine(DeferredRegisterOneFrame());
        }
    }

    private System.Collections.IEnumerator DeferredRegisterOneFrame()
    {
        yield return null;
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.RegisterSceneObjects(playerController, levelTimer, hud, pauseMenu, resultUI, cutsceneManager);
        }
        else
        {
            Debug.LogWarning("[LevelSceneInitializer] LevelManager.Instance still null. Ensure LevelManager is in bootstrap/Menu scene or created before scene init.");
        }
    }
}
