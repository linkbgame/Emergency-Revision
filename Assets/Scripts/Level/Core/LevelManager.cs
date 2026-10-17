using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 管理单关生命周期：加载、初始化、启动、暂停、失败/胜利、卸载。
/// 单例（场景级），在加载关卡时由 MenuManager 或 GameEntry 调用 LoadLevel。
/// 
/// 改进点（与原版比）：
/// - 公开 CurrentConfig 以便运行时读取配置（PE、对象在初始化时可读取）
/// - 新增 OnPlayerPhaseRequested 事件（LevelManager 通知 PlayerController 切换阶段）
/// - InitLevel 改为协程流程，支持 ILevelInitializable 异步初始化（按 LevelConfig per-object 配置）
/// - 订阅使用显式方法以便在 OnDestroy 中解绑，避免内存泄露
/// - Pause/Resume 现在显式禁用/启用 playerController 输入（调用 EnableInput）
/// - 对 CutsceneManager 做 null-check 容错处理
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("引用（Menu场景中只用设置前三个，其他场景自动引用）")]
    public LoadingUI loadingUI;   // 显示加载进度（在 Menu 场景中分配）
    [Tooltip("倒计时")] public float loadingTime = 1f; // 最短 loading UI 显示时间

    [Tooltip("是否在关卡内显示倒计时")] public bool showHUD = true;
    [Tooltip("倒计时")] public HUD hud;               // 游戏内 HUD（在关卡 Scene 中查找或由关卡 prefab 提供）
    
    public PauseMenu pauseMenu;   // 暂停菜单（在关卡场景中）
    public ResultUI resultUI;     // 失败界面（在场景中）
    public PlayerController playerController; // 关卡中的玩家控制器（在场景中）
    public LevelTimer levelTimer; // LevelTimer 组件（在场景中）
    public CutsceneManager cutsceneManager; // CutsceneManager（在场景中）



    // 当前加载的 LevelConfig（私有存储，公开只读访问）
    private LevelConfig currentConfig;
    public LevelConfig CurrentConfig => currentConfig;

    private bool isLoading = false;

    #region Events
    public event Action OnLevelLoaded;
    public event Action OnLevelStarted;
    public event Action OnLevelPaused;
    public event Action OnLevelResumed;
    public event Action OnLevelSucceeded;
    public event Action OnLevelFailed;
    public event Action<float> OnTimerTick;

    /// <summary>
    /// 当关卡内需要切换玩家相位（phase）时，LevelManager 通过此事件通知 PlayerController。
    /// 建议 PlayerController 在 Init 时订阅此事件： LevelManager.Instance.OnPlayerPhaseRequested += pc.HandleRequestedPhase;
    /// </summary>
    public event Action<PlayerController.PlayerPhase> OnPlayerPhaseRequested;

    public event Action<MachineController.MachineEvent> OnMachineEventRequested;
    #endregion

    // 记录我们为 pauseMenu/resultUI 注册的回调，以便在 OnDestroy 时解绑
    private Action onPauseReturnToMenuHandler;
    private Action onResultRetryHandler;
    private Action onResultReturnToMenuHandler;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        DontDestroyOnLoad(gameObject);
        DontDestroyOnLoad(loadingUI);
    }

    private void Update()
    {
        if (pauseMenu != null)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                //判断pauseMenu是否已经显示
                if (pauseMenu.gameObject.activeSelf)
                {
                    Resume();
                }
                else
                {
                    Pause();
                }
            }
        }
    }

    #region Public API

    /// <summary>
    /// 异步加载关卡 Scene 并在加载完成后初始化关卡（显示 loadingUI）。
    /// </summary>
    public void LoadLevel(LevelConfig config)
    {
        Debug.Log($"[LevelManager] LoadLevel called. isLoading={isLoading}, config={config?.name}");
        if (isLoading) return;
        StartCoroutine(LoadLevelCoroutine(config));
    }

    /// <summary>
    /// 重新开始当前关卡（重载 Scene）。
    /// </summary>
    public void RestartLevel()
    {
        if (currentConfig != null)
            LoadLevel(currentConfig);
    }

    /// <summary>
    /// 暂停：暂停计时器并打开 PauseMenu，禁用玩家输入。
    /// </summary>
    public void Pause()
    {
        if (levelTimer != null) levelTimer.PauseTimer();

        // 关闭玩家输入
        playerController?.EnableInput(false);
        Time.timeScale = 0f;

        pauseMenu?.Show();
        OnLevelPaused?.Invoke();
    }

    /// <summary>
    /// 恢复：关闭 PauseMenu，则恢复计时器与输入。
    /// </summary>
    public void Resume()
    {
        pauseMenu?.Hide();

        // 恢复玩家输入
        playerController?.EnableInput(true);
        Time.timeScale = 1f;

        if (levelTimer != null) levelTimer.ResumeTimer();
        OnLevelResumed?.Invoke();
    }

    /// <summary>
    /// 返回主菜单：清理并加载 Menu Scene（同步/异步均可，这里用 async）。
    /// </summary>
    public void ReturnToMenu()
    {
        Time.timeScale = 1f;

        // 清理状态（HUD/Timer 等）
        isLoading = false;
        StopAllCoroutines();
        StartCoroutine(ReturnToMenuCoroutine());

        AudioManager.Instance?.PlayBGM("menu");

    }
    #endregion

    #region Internal Coroutines

    private IEnumerator LoadLevelCoroutine(LevelConfig config)
    {
        isLoading = true;
        currentConfig = config;

        // Show loading UI
        loadingUI?.Show(0f);

        yield return new WaitForSecondsRealtime(loadingTime);

        // Begin async load
        AsyncOperation op = SceneManager.LoadSceneAsync(config.sceneName);     
        op.allowSceneActivation = true;

        // Track progress and update UI
        while (!op.isDone)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f);
            loadingUI?.Show(progress);
            yield return null;
        }

        /*//停顿4秒
        yield return new WaitForSeconds(4f);*/

        // 初始化关卡（现在为协程实现，以支持异步初始化）
        yield return StartCoroutine(InitLevelCoroutine(config));

        // Hide loading UI
        loadingUI?.Hide();

        isLoading = false;
        OnLevelLoaded?.Invoke();
    }

    private IEnumerator ReturnToMenuCoroutine()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync("Menu");
        while (!op.isDone)
        {
            yield return null;
        }
    }

    #endregion

    #region Init / Start / Handle Timed Events

    /// <summary>
    /// 初始化当前场景内的关卡对象（异步版本）。
    /// 流程：
    /// 1. 查找 Player/Timer/HUD 等引用（如未在 Inspector 赋值）；
    /// 2. 初始化 LevelTimer（SetTime）并订阅其事件；
    /// 3. 针对场景中所有实现 ILevelInitializable 的组件，查找 currentConfig 中对应的 PerObjectConfig（如果有），并调用 Initialize()；
    ///    如果组件返回 IEnumerator，则等待其完成（允许组件异步加载资源）。
    /// 4. 绑定 UI 回调（使用显式方法便于解绑）。
    /// 5. 播放 intro cutscene（若配置）然后 StartLevel。
    /// </summary>
    private IEnumerator InitLevelCoroutine(LevelConfig config)
    {
        if (playerController == null) playerController = FindObjectOfType<PlayerController>(); 
        if (levelTimer == null) levelTimer = FindObjectOfType<LevelTimer>(); 
        if (hud == null) hud = FindObjectOfType<HUD>(); 
        if (pauseMenu == null) pauseMenu = FindObjectOfType<PauseMenu>(); 
        if (resultUI == null) resultUI = FindObjectOfType<ResultUI>();
        if (cutsceneManager == null) cutsceneManager = FindObjectOfType<CutsceneManager>();

        // 初始化 timer
        if (levelTimer != null)
        {
            levelTimer.SetTime(config.timeLimitSeconds);
            levelTimer.OnTick += HandleTimerTick;
            levelTimer.OnTimeUp += HandleTimeUp;
        }

        // 重置 timedEvents
        if (config.timedEvents != null)
        {
            foreach (var e in config.timedEvents)
                e.hasFired = false;
        }

        // HUD 设置初始时间
        if (hud != null && showHUD)
            hud?.SetTime(config.timeLimitSeconds);

        // 绑定/注册 UI 回调（使用显式方法以便在 OnDestroy 中解绑）
        if (pauseMenu != null)
        {
            onPauseReturnToMenuHandler = HandlePauseReturnToMenu;
            pauseMenu.OnReturnToMenu += onPauseReturnToMenuHandler;
        }

        if (resultUI != null)
        {
            onResultRetryHandler = HandleResultRetry;
            onResultReturnToMenuHandler = HandleResultReturnToMenu;
            resultUI.OnRetry += onResultRetryHandler;
            resultUI.OnReturnToMenu += onResultReturnToMenuHandler;
        }

        // 初始化场景内实现 ILevelInitializable 的组件
        Dictionary<string, PerObjectConfigSO> perObjectDict = null;
        if (config.perObjectConfigs != null && config.perObjectConfigs.Length > 0)
        {
            perObjectDict = new Dictionary<string, PerObjectConfigSO>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in config.perObjectConfigs)
            {
                if (p == null || string.IsNullOrEmpty(p.objectId)) continue;
                if (!perObjectDict.ContainsKey(p.objectId))
                    perObjectDict.Add(p.objectId, p);
                else
                    Debug.LogWarning($"Duplicate perObject config id: {p.objectId} in {config.name}");
            }
        }


        // 寻找场景中所有 initializables
        var initializables = FindObjectsOfType<MonoBehaviour>().OfType<ILevelInitializable>().ToArray();

        // Iterate and call Initialize (allow IEnumerator)
        foreach (var initComp in initializables)
        {
            object cfgForThis = null;
            if (perObjectDict != null && !string.IsNullOrEmpty(initComp.ObjectId))
            {
                perObjectDict.TryGetValue(initComp.ObjectId, out var v);
                cfgForThis = v;
            }

            IEnumerator initRoutine = initComp.Initialize(cfgForThis, new LevelContext(config, this));
            if (initRoutine != null)
            {
                // wait for component's asynchronous init to complete
                yield return StartCoroutine(initRoutine);
            }
            else
            {
                // synchronous init - continue
            }
        }

        //AudioManager.Instance?.PlayBGM(config.levelId);  通过 levelId 播放对应 BGM
        AudioManager.Instance?.PlayBGM("level");

        // 5) intro cutscene -> start game, or directly start
        if (config.playIntroCutscene && !string.IsNullOrEmpty(config.introCutsceneId))
        {
            yield return StartCoroutine(PlayIntroThenStart(config.introCutsceneId));
        }
        else
        {
            StartLevel();
        }

        
    }

    /// <summary>
    /// 启动关卡（开始倒计时并通知）
    /// </summary>
    private void StartLevel()
    {
        // 切换摄像机到玩家（委托/更智能的切换可替代）
        ActivatePlayerCamera();

        // 启用玩家输入（如果 playerController 存在）
        playerController?.EnableInput(true);

        levelTimer?.StartTimer();
        OnLevelStarted?.Invoke();
    }

    /// <summary>
    /// 播放 intro 并在结束后启用玩家与计时器（做容错判空）
    /// </summary>
    private IEnumerator PlayIntroThenStart(string introId)
    {
        playerController?.EnableInput(false);

        if (cutsceneManager != null)
        {
            // wait for cutscene to complete
            yield return cutsceneManager.PlayCoroutine(introId, true);
        }
        else
        {
            Debug.LogWarning("LevelManager: CutsceneManager not found. Skipping intro.");
        }

        // 切换摄像机到玩家（委托/更智能的切换可替代）
        ActivatePlayerCamera();

        // 启用玩家输入并启动计时器
        playerController?.EnableInput(true);
        levelTimer?.StartTimer();

        //Npc订阅了该事件，在此时启用Npc移动
        OnLevelStarted?.Invoke();
    }

    /// <summary>
    /// 切换到玩家摄像机（注意：如果使用 Cinemachine，应改为改优先级或调用对应管理器）
    /// </summary>
    /// <remarks>
    /// 先是全部禁用，然后启用主相机是不行的，因为禁用相机会导致其 GameObject 也被禁用，从而 Camera.main 也会变成 null。
    /// </remarks>
    private void ActivatePlayerCamera()
    {
        Debug.Log("[ActivatePlayerCamera] called", this);

        // 先获取标记为 MainCamera 的摄像机（注意：Camera.main 基于 Tag "MainCamera"）
        Camera main = Camera.main;

        // 输出当前场景中所有摄像机的信息（便于调试）
        var cams = Camera.allCameras;
        /*Debug.Log($"[ActivatePlayerCamera] allCameras count={cams.Length}");
        foreach (var c in cams)
        {
            Debug.Log($"  cam: name={c.name}, enabled={c.enabled}, goActive={c.gameObject.activeInHierarchy}", c);
        }

        if (main == null)
        {
            Debug.LogWarning("[ActivatePlayerCamera] Camera.main is null — no camera tagged 'MainCamera' or it's inactive/destroyed.");
            return;
        }*/

        // 只禁用其他摄像机，保留 main
        foreach (var c in cams)
        {
            if (c == main) continue;
            c.enabled = false;
        }

        // 启用 main 并打印名字
        main.enabled = true;
        Debug.Log("[ActivatePlayerCamera] 切换到主相机: " + main.name, main);
    }


    /// <summary>
    /// LevelTimer 每次 tick 回调（更新 HUD 并触发配置中 TimedEvent）
    /// </summary>
    private void HandleTimerTick(float remaining)
    {
        OnTimerTick?.Invoke(remaining);
        if (showHUD)
            hud?.SetTime(remaining);

        if (currentConfig != null && currentConfig.timedEvents != null)
        {
            foreach (var evt in currentConfig.timedEvents)
            {
                if (evt.hasFired) continue;

                float checkTime = evt.mode == TimedEventData.TriggerMode.FromStart
                    ? (currentConfig.timeLimitSeconds - remaining) // elapsed time since start
                    : remaining;

                //从开始计算事件触发时间
                if (evt.mode == TimedEventData.TriggerMode.FromStart)
                {
                    if (checkTime >= evt.triggerTime)
                    {
                        HandleTimedEvent(evt);
                        evt.hasFired = true;
                    }
                        
                }

                //从结束计算事件触发时间
                if (evt.mode == TimedEventData.TriggerMode.FromEnd)
                {
                    if (checkTime <= evt.triggerTime)
                        HandleTimedEvent(evt);
                }

                /*if (evt.triggerTime - checkTime < float.Epsilon )
                {
                    HandleTimedEvent(evt);
                    evt.hasFired = true;
                }*/
            }
        }
    }

    /// <summary>
    /// 触发指定的 TimedEvent（如驱动 trap / 改变 player phase）。
    /// </summary>
    private void HandleTimedEvent(TimedEventData evt)
    {
        switch (evt.eventType)
        {
            case TimedEventData.TimedEventType.ChangePlayerPhase:
                OnPlayerPhaseRequested?.Invoke(evt.playerPhase); // evt.playerPhase 是 PlayerController.PlayerPhase 类型
                break;
            case TimedEventData.TimedEventType.ActivateMachine:
                OnMachineEventRequested?.Invoke(evt.machineEvent);//evt.machineEvent 是 MachineController.MachineEvent 类型
                break;
            case TimedEventData.TimedEventType.DeactivateTrap:
                // TrapManager.Instance?.Deactivate(evt.parameter);
                break;
            case TimedEventData.TimedEventType.PlayCutscene:
                if (CutsceneManager.Instance != null)
                    CutsceneManager.Instance.Play(evt.parameter);
                break;
            case TimedEventData.TimedEventType.SendMessageToObject:
                GameObject obj = GameObject.Find(evt.parameter);
                obj?.SendMessage("OnTimedEvent", SendMessageOptions.DontRequireReceiver);
                break;
            default:
                break;
        }
    }

    #endregion

    #region TimeUp / Win / Fail

    private void HandleTimeUp()
    {
        StartCoroutine(RunFailureFlow());
    }

    /// <summary>
    /// 失败流程：停止 timer、播放失败动画、显示失败界面。
    /// </summary>
    private IEnumerator RunFailureFlow()
    {
        yield return null; // wait a frame if needed

        playerController?.EnableInput(false);

        levelTimer?.StopTimer();

        // 这里你可以让 playerController 返回一个 IEnumerator 来执行失败动画
        // eg: var anim = playerController.PlayFailureAnimation(); if (anim != null) yield return StartCoroutine(anim);

        resultUI?.ShowFailure();
        OnLevelFailed?.Invoke();
    }

    public void RunVictoryFlow()
    {
        StartCoroutine(VictoryFlowCoroutine());
    }

    private IEnumerator VictoryFlowCoroutine()
    {
        yield return null;

        // 停止计时器
        levelTimer?.StopTimer();

        //禁用玩家输入
        playerController?.EnableInput(false);

        // 可选：如果 PlayerController 提供了一个返回 IEnumerator 的胜利动画方法（非必需）

        // 播放 victory cutscene（如果配置并且 CutsceneManager 可用）
        if (currentConfig != null && currentConfig.playVictoryCutscene)
        {
            // 推荐在 LevelConfig 中增加一个 victoryCutsceneId 字段以独立设置胜利片头
            // fallback: 如果未配置 victoryCutsceneId，则回退到 introCutsceneId（可删掉此回退逻辑）
            string victoryId = null;
            // 尝试读取 strong-typed field（如果你已在 LevelConfig 添加）
            var victoryField = currentConfig.GetType().GetField("victoryCutsceneId");
            if (victoryField != null)
                victoryId = victoryField.GetValue(currentConfig) as string;

            if (string.IsNullOrEmpty(victoryId))
                victoryId = currentConfig.introCutsceneId; // fallback 可选

            if (!string.IsNullOrEmpty(victoryId))
            {
                if (cutsceneManager != null)
                {
                    // PlayCoroutine 会等待 cutscene 播放完成
                    yield return cutsceneManager.PlayCoroutine(victoryId, true);
                }
                else
                {
                    Debug.LogWarning($"LevelManager: victoryCutscene '{victoryId}' configured but CutsceneManager not found. Skipping cutscene.");
                }
            }
        }


        OnLevelSucceeded?.Invoke();

        ReturnToMenu();
    }

    #endregion

    #region UI Callback Handlers (explicit so we can unsubscribe)

    private void HandlePauseReturnToMenu()
    {
        ReturnToMenu();
    }

    private void HandleResultRetry()
    {
        RestartLevel();
    }

    private void HandleResultReturnToMenu()
    {
        ReturnToMenu();
    }

    #endregion

    #region Cleanup
    private void OnDestroy()
    {
        UnbindSceneEvents();
    }
    #endregion

    #region Editor helpers (Debugging)

    public void Editor_StartTimer()
    {
        if (levelTimer == null)
        {
            levelTimer = FindObjectOfType<LevelTimer>();
            if (levelTimer == null)
            {
                Debug.LogWarning("Editor_StartTimer: 未找到 LevelTimer 实例。");
                return;
            }
        }
        levelTimer.StartTimer();
        Debug.Log("[LevelManager] Editor_StartTimer invoked.");
    }

    public void Editor_PauseTimer()
    {
        if (levelTimer == null) levelTimer = FindObjectOfType<LevelTimer>();
        levelTimer?.PauseTimer();
        Debug.Log("[LevelManager] Editor_PauseTimer invoked.");
    }

    public void Editor_TriggerNextTimedEvent()
    {
        if (currentConfig == null || currentConfig.timedEvents == null)
        {
            Debug.LogWarning("Editor_TriggerNextTimedEvent: currentConfig 或 timedEvents 为空。");
            return;
        }
        var evt = currentConfig.timedEvents.FirstOrDefault(e => !e.hasFired);
        if (evt == null)
        {
            Debug.Log("[LevelManager] Editor_TriggerNextTimedEvent: 没有未触发事件。");
            return;
        }
        HandleTimedEvent(evt);
        evt.hasFired = true;
        Debug.Log($"[LevelManager] Editor triggered timed event: {evt.eventType} ({evt.parameter})");
    }

    public void Editor_ForceWin()
    {
        Debug.Log("[LevelManager] Editor_ForceWin invoked.");
        StartCoroutine(VictoryFlowCoroutine());
    }

    public void Editor_ForceFail()
    {
        Debug.Log("[LevelManager] Editor_ForceFail invoked.");
        StartCoroutine(RunFailureFlow());
    }

    #endregion

    #region References Registration
    /// <summary>
    /// 解绑当前 scene-local 引用上订阅的事件（在替换引用前调用，或在 OnDestroy 中调用）
    /// </summary>
    private void UnbindSceneEvents()
    {
        // LevelTimer 事件解绑
        if (levelTimer != null)
        {
            try
            {
                levelTimer.OnTick -= HandleTimerTick;
                levelTimer.OnTimeUp -= HandleTimeUp;
            }
            catch { /* 忽略在已销毁对象上解绑的异常 */ }
        }

        // PauseMenu / ResultUI 解绑
        if (pauseMenu != null && onPauseReturnToMenuHandler != null)
            pauseMenu.OnReturnToMenu -= onPauseReturnToMenuHandler;

        if (resultUI != null)
        {
            if (onResultRetryHandler != null) resultUI.OnRetry -= onResultRetryHandler;
            if (onResultReturnToMenuHandler != null) resultUI.OnReturnToMenu -= onResultReturnToMenuHandler;
        }

        // 不清空 playerController/hud 等字段，这由 ClearSceneObjects 控制（见下）
    }

    /// <summary>
    /// 场景级对象注册（由每个 scene 的 LevelSceneInitializer 调用）。
    /// 会先解绑旧引用的事件，再赋予新引用并订阅事件。
    /// </summary>
    public void RegisterSceneObjects(
        PlayerController pc,
        LevelTimer lt,
        HUD h,
        PauseMenu pm,
        ResultUI ru,
        CutsceneManager cm)
    {
        // 先解绑旧引用的事件（避免内存泄漏 / 重复订阅）
        UnbindSceneEvents();

        // 绑定新引用
        playerController = pc;
        levelTimer = lt;
        hud = h;
        pauseMenu = pm;
        resultUI = ru;
        cutsceneManager = cm;

        // 订阅新的事件
        if (levelTimer != null)
        {
            // 如果 currentConfig 已有时间，确保 timer 被正确初始化
            if (currentConfig != null)
                levelTimer.SetTime(currentConfig.timeLimitSeconds);

            levelTimer.OnTick += HandleTimerTick;
            levelTimer.OnTimeUp += HandleTimeUp;
        }

        if (pauseMenu != null)
        {
            onPauseReturnToMenuHandler = HandlePauseReturnToMenu;
            pauseMenu.OnReturnToMenu += onPauseReturnToMenuHandler;
        }

        if (resultUI != null)
        {
            onResultRetryHandler = HandleResultRetry;
            onResultReturnToMenuHandler = HandleResultReturnToMenu;
            resultUI.OnRetry += onResultRetryHandler;
            resultUI.OnReturnToMenu += onResultReturnToMenuHandler;
        }

        // HUD 初始显示
        if (hud != null && showHUD)
        {
            //Debug.Log("[LevelManager] RegisterSceneObjects: Setting HUD time.");
            hud?.SetTime(currentConfig != null ? currentConfig.timeLimitSeconds : 0f);
        }
            
    }

    /// <summary>
    /// 清空并解绑所有 scene-local 引用（可在卸载场景或 ReturnToMenu 时调用）
    /// </summary>
    public void ClearSceneObjects()
    {
        UnbindSceneEvents();

        playerController = null;
        levelTimer = null;
        hud = null;
        pauseMenu = null;
        resultUI = null;
        cutsceneManager = null;

        onPauseReturnToMenuHandler = null;
        onResultRetryHandler = null;
        onResultReturnToMenuHandler = null;
    }

    #endregion
}

