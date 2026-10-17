using UnityEngine;

public class MenuManager : MonoBehaviour
{
    /*[Header("可选：Menu 的 LoadingUI（会被注册到常驻 LevelManager）")]
    public LoadingUI menuLoadingUI;

    private void Start()
    {
        // 如果 LevelManager 已常驻，把 menu 的 loadingUI 给它，方便 LoadLevel 时显示进度
        if (LevelManager.Instance != null && menuLoadingUI != null)
        {
            LevelManager.Instance.loadingUI = menuLoadingUI;
        }
    }*/

    /*private static MenuManager _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
    }*/

    /// <summary>
    /// 给 UI Button 在 Inspector 上直接调用的入口：
    /// 将一个 LevelConfig asset 作为参数传入（在 Inspector 把 LevelConfig asset 拖到回调参数槽）。
    /// </summary>
    public void LoadLevelDirect(LevelConfig cfg)
    {
        if (cfg == null)
        {
            Debug.LogWarning("[MenuManager] LoadLevelDirect called with null LevelConfig.");
            return;
        }

        if (LevelManager.Instance == null)
        {
            Debug.LogWarning("[MenuManager] LevelManager.Instance is null. Make sure LevelManager prefab is instantiated (e.g. in Splash or in Menu).");
            // 可选：在这里实例化 LevelManager prefab（如果你想）
            // Instantiate(levelManagerPrefab);
            return;
        }

        Debug.Log($"[MenuManager] LoadLevelDirect called with LevelConfig: {cfg.name}");
        LevelManager.Instance.LoadLevel(cfg);
    }
}
