using System.Collections;
using UnityEngine;

/// <summary>
/// 负责根据 PlayerController 的相位（phase）做视觉与音效表现。
/// </summary>
/// <remark>
/// 设计要点（按你的要求实现的做法 A）：
/// - 正常模式（deBugMode == false）：不主动驱动时序；订阅 PlayerController.OnPhaseChanged，响应 LevelManager 通过 TimedEvent 触发的 Phase 切换。
/// - 测试模式（deBugMode == true）：忽略 LevelManager 的时序，使用本脚本的内部时长依次播放转换流程，便于单场景调试。
/// - 阶段：Normal -> HeadTurningRed -> HeadOnFire -> HeadBoom
/// - 在阶段切换时，脚本会确保取消前一个阶段的协程，然后启动相应的表现协程（颜色插值、粒子开关、音效接口调用等）。
/// 
/// 备注：
/// - 正常模式下时长优先读取 LevelManager.Instance.CurrentConfig 的 turningRedDuration、onFireDuration 字段；
///   若未设置，会尝试从 CurrentConfig.timedEvents 中推算（根据配置的 ChangePlayerPhase 事件的 triggerTime 差值）。
/// - 所有对外音频调用通过 AudioManager.Instance（若存在）来播放；若项目尚未实现 AudioManager，调用为容错（不会抛异常）。
/// </remark>
public class PlayerEffect : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("依赖的 PlayerController")]
    public PlayerController player;

    [SerializeField, Tooltip("头部渲染器（使用材质 color 属性来做变色）")]
    private Renderer headRenderer;

    [SerializeField, Tooltip("速度线")] private GameObject speedLines;

    [Header("特效粒子（按需拖入）")]
    [SerializeField, Tooltip("燃烧特效（在 HeadOnFire 时启用）")] private ParticleSystem burningEffect;
    [SerializeField, Tooltip("烟雾特效（在 HeadOnFire 时启用）")] private ParticleSystem smokeEffect;
    [SerializeField, Tooltip("闪光特效（爆炸/特殊时刻）")] private ParticleSystem sparkleEffect;

    [Header("音源与音效（占位：通过 AudioManager 调用）")]
    [SerializeField] private AudioSource burningSource;
    [SerializeField] private AudioClip burningSFX;
    [SerializeField] private AudioClip explosionSFX;

    [Header("--------------------")]
    [Header("测试模式（启用后忽略 LevelManager 的 timed events）")]
    public bool deBugMode = false;

    [SerializeField, Tooltip("测试模式：逐步变红时长（仅测试模式生效）")]
    private float m_turningRedDuration = 5f;

    [SerializeField, Tooltip("测试模式：着火持续时长（仅测试模式生效）")]
    private float m_onFireDuration = 10f;

    [SerializeField, Tooltip("测试模式：变黑（爆炸前的固色）时长")]
    private float m_blackDuration = 1f;


    private Color startColor = Color.white;

    private Color targetColor = new Color(170f / 255f, 0f, 0f, 1f); // 170,0,0

    private Color blackColor = Color.black;

    // 内部状态
    private Coroutine runningCoroutine = null;

    // 本地跟踪当前相位，用于控制速度线等即时展示
    private PlayerController.PlayerPhase currentPhaseLocal = PlayerController.PlayerPhase.Normal;

    private void OnEnable()
    {
        // 尝试自动寻址 player（如果未在 Inspector 指定）
        if (player == null)
        {
            player = GetComponentInParent<PlayerController>();
            if (player == null)
                player = FindObjectOfType<PlayerController>();
        }

        // 尝试自动寻址 renderer（仅在未指定时）
        if (headRenderer == null)
        {
            headRenderer = GetComponent<Renderer>();
            if (headRenderer == null)
            {
                var r = GetComponentInChildren<Renderer>();
                if (r != null) headRenderer = r;
            }
        }

        // 订阅 PlayerController 的相位变化事件（若存在）
        if (player != null)
        {
            player.OnPhaseChanged += OnPlayerPhaseChanged;
        }

        // 如果处于 Debug 模式，立刻开始本地测试序列
        if (deBugMode)
        {
            StartDebugSequence();
        }
        else
        {
            // 正常模式：确保材质处于初始颜色
            ApplyColorImmediate(startColor);
            StopAllEffects();
        }
    }

    private void OnDisable()
    {
        // 解绑事件
        if (player != null)
        {
            player.OnPhaseChanged -= OnPlayerPhaseChanged;
        }

        // 停止任何正在运行的协程与特效
        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }
        StopAllEffects();
    }

    private void Update()
    {
        // 每帧根据当前阶段与 Shift 键状态更新速度线显示
        UpdateSpeedLinesState();
    }

    /// <summary>
    /// 每帧决定 speedLines 是否显示
    /// 规则：
    /// - HeadOnFire => 一直显示
    /// - HeadBoom => 一直隐藏
    /// - 其他（Normal / HeadTurningRed） => 仅在按住 LeftShift 时显示
    /// </summary>
    private void UpdateSpeedLinesState()
    {
        if (speedLines == null) return;

        bool shouldShow = false;

        switch (currentPhaseLocal)
        {
            case PlayerController.PlayerPhase.HeadOnFire:
                shouldShow = true;
                break;
            case PlayerController.PlayerPhase.HeadBoom:
                shouldShow = false;
                break;
            default:
                // Normal 或 HeadTurningRed 等
                shouldShow = Input.GetKey(KeyCode.LeftShift);
                break;
        }

        // 仅在状态改变时调用 SetActive 可以少量优化，但直接赋值也没问题
        if (speedLines.activeSelf != shouldShow)
            speedLines.SetActive(shouldShow);
    }

    #region Phase handling (entry point)

    /// <summary>
    /// 当 PlayerController 广播相位变化时调用（正常模式的入口）。
    /// 仅根据 phase 启动相应表现（协程会管理时序与过渡）。
    /// </summary>
    private void OnPlayerPhaseChanged(PlayerController.PlayerPhase newPhase)
    {
        // 如果处于测试模式，忽略外部相位驱动
        if (deBugMode) return;

        // 更新本地相位（并让 Update 控制速度线）
        currentPhaseLocal = newPhase;

        // 取消当前进行中的表现
        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }

        switch (newPhase)
        {
            case PlayerController.PlayerPhase.Normal:
                runningCoroutine = StartCoroutine(DoNormal());
                break;
            case PlayerController.PlayerPhase.HeadTurningRed:
                runningCoroutine = StartCoroutine(DoTurningRed_NormalMode());
                break;
            case PlayerController.PlayerPhase.HeadOnFire:
                runningCoroutine = StartCoroutine(DoOnFire_NormalMode());
                break;
            case PlayerController.PlayerPhase.HeadBoom:
                runningCoroutine = StartCoroutine(DoBoom_NormalMode());
                break;
            default:
                break;
        }
    }

    #endregion

    #region Normal mode coroutines (driven by LevelManager timings)

    /// <summary>
    /// Normal 模式：静止回到初始颜色并关闭所有特效
    /// </summary>
    private IEnumerator DoNormal()
    {
        StopAllEffects();
        ApplyColorImmediate(startColor);
        yield break;
    }

    /// <summary>
    /// HeadTurningRed（正常模式）：
    /// - 计算变红所需时间（优先 LevelManager.CurrentConfig.turningRedDuration；若为0则尝试从 timedEvents 推算；否则使用默认 m_turningRedDuration）
    /// - 在该时长内把头部材质颜色从 startColor 渐变到 targetColor
    /// </summary>
    private IEnumerator DoTurningRed_NormalMode()
    {
        StopAllEffects();

        AudioManager.Instance?.PlayPhaseSfx(PhaseSfxKey.HeadTurningRed);

        float dur = ResolveTurningRedDuration();
        if (dur <= 0f) dur = m_turningRedDuration; // fallback

        yield return StartCoroutine(LerpHeadColor(startColor, targetColor, dur));

        // 保证最终颜色为 target
        ApplyColorImmediate(targetColor);

        
    }

    /// <summary>
    /// HeadOnFire（正常模式）：
    /// - 进入该状态时应该已经是 targetColor（或 DoTurningRed 已完成）
    /// - 启动燃烧与烟雾特效、播放音效，并根据 onFireDuration 计时（优先 LevelManager.CurrentConfig）
    /// </summary>
    private IEnumerator DoOnFire_NormalMode()
    {
        // ensure color is target
        ApplyColorImmediate(targetColor);

        AudioManager.Instance?.PlayPhaseSfx(PhaseSfxKey.HeadOnFire);


        StartBurningEffects();

        float dur = ResolveOnFireDuration();
        if (dur <= 0f) dur = m_onFireDuration; // fallback to test value if config absent

        // 等待燃烧持续时间（在正常模式下LevelManager控制相变，这里我们仍然等待以防其他系统依赖）
        // 但是不要在这里触发下一阶段- LevelManager会根据TimedEvent来做
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        
    }

    /// <summary>
    /// HeadBoom（正常模式）：
    /// - 停止燃烧特效
    /// - 固定颜色为 targetColor 并在 m_blackDuration 内渐变到黑色（设计里你要的是到达 Boom 前 1 秒逐步变黑）
    /// - 触发爆炸特效与音效（接口留白以便你扩展）
    /// </summary>
    private IEnumerator DoBoom_NormalMode()
    {
        // stop burning immediately
        StopBurningEffects();

        // solid target color at moment of transition
        ApplyColorImmediate(targetColor);

        // usually LevelManager's timed events control WHEN HeadBoom happens; here we do the black transition then explosion
        float blackDur = m_blackDuration;
        if (blackDur <= 0f) blackDur = 1f;

        yield return StartCoroutine(LerpHeadColor(targetColor, blackColor, blackDur));

        ApplyColorImmediate(blackColor);

        //头从红色变黑色之后，爆炸同时播放音效
        AudioManager.Instance?.PlayPhaseSfx(PhaseSfxKey.HeadBoom);

        // explosion
        DoExplode();

        yield break;
    }

    #endregion

    #region Debug (test-mode) sequence

    /// <summary>
    /// 测试模式：使用内部变量顺序执行 TurningRed -> OnFire -> Boom
    /// </summary>
    private void StartDebugSequence()
    {
        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }
        runningCoroutine = StartCoroutine(DebugSequence());
    }

    private IEnumerator DebugSequence()
    {
        // ensure initial
        ApplyColorImmediate(startColor);
        StopAllEffects();

        // Normal
        currentPhaseLocal = PlayerController.PlayerPhase.Normal;
        yield return null;

        // 1) Turning red (local duration m_turningRedDuration)
        currentPhaseLocal = PlayerController.PlayerPhase.HeadTurningRed;
        yield return StartCoroutine(LerpHeadColor(startColor, targetColor, m_turningRedDuration));
        ApplyColorImmediate(targetColor);

        // 2) On fire (duration m_onFireDuration)
        currentPhaseLocal = PlayerController.PlayerPhase.HeadOnFire;
        StartBurningEffects();
        float t = 0f;
        while (t < m_onFireDuration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        // 3) Boom, fade to black over m_blackDuration, then explosion
        currentPhaseLocal = PlayerController.PlayerPhase.HeadBoom;
        StopBurningEffects();
        yield return StartCoroutine(LerpHeadColor(targetColor, blackColor, m_blackDuration));
        ApplyColorImmediate(blackColor);

        DoExplode();

        runningCoroutine = null;
    }

    #endregion

    #region Helpers: color / effects / audio

    /// <summary>线性插值头部材质颜色</summary>
    private IEnumerator LerpHeadColor(Color from, Color to, float duration)
    {
        if (headRenderer == null)
        {
            yield break;
        }
        if (duration <= 0f)
        {
            ApplyColorImmediate(to);
            yield break;
        }

        float t = 0f;
        var mat = headRenderer.material;
        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / duration);
            Color c = Color.Lerp(from, to, alpha);
            mat.color = c;
            yield return null;
        }
        mat.color = to;
    }

    /// <summary>立即应用颜色（直接设置材质）</summary>
    private void ApplyColorImmediate(Color c)
    {
        if (headRenderer == null) return;
        var mat = headRenderer.material;
        mat.color = c;
    }

    /// <summary>启动燃烧/烟雾/音效</summary>
    private void StartBurningEffects()
    {
        if (burningEffect != null && !burningEffect.isPlaying) burningEffect.Play();
        //AI 以为smokeEffect是燃烧的一部分，实则smokeEffect是爆炸的一部分
        //if (smokeEffect != null && !smokeEffect.isPlaying) smokeEffect.Play();
        if (burningSource != null && burningSFX != null)
        {
            burningSource.clip = burningSFX;
            burningSource.loop = true;
            burningSource.Play();
        }
        else
        {
            // 如果有 AudioManager 单例，可通过它播放（占位实现）
            //AudioManager.Instance?.PlaySFX(burningSFX, loop: true); // 仅当你实现了此方法时会生效
        }
    }

    /// <summary>停止燃烧/烟雾/音效</summary>
    private void StopBurningEffects()
    {
        if (burningEffect != null && burningEffect.isPlaying) burningEffect.Stop();
        //待改进
        //if (smokeEffect != null && smokeEffect.isPlaying) smokeEffect.Stop();
        if (burningSource != null && burningSource.isPlaying)
        {
            burningSource.Stop();
            burningSource.loop = false;
        }
        else
        {
            //AudioManager.Instance?.StopSFX(burningSFX); // 占位：依赖 AudioManager 的实现
        }
    }

    /// <summary>停止一切特效（燃烧、烟雾、闪光）和音效</summary>
    private void StopAllEffects()
    {
        StopBurningEffects();
        if (sparkleEffect != null && sparkleEffect.isPlaying) sparkleEffect.Stop();
    }

    /// <summary>触发爆炸（播放粒子、音效，并留空位置用于后续逻辑如伤害判定、相机震动等）</summary>
    private void DoExplode()
    {
        // stop burn
        StopBurningEffects();

        // sparkle/explosion particle
        //真的爆炸特效是smoke+fire+debris等，这里用sparkle占位
        if (smokeEffect != null) smokeEffect.Play();

        // play explosion sound
        if (burningSource != null && explosionSFX != null)
        {
            burningSource.PlayOneShot(explosionSFX);
        }
        else
        {
            //AudioManager.Instance?.PlayOneShot(explosionSFX);
        }

        // TODO: 扩展点：广播一个事件或调用 LevelManager/其它系统处理爆炸效果（比如对周围物体造成伤害、切换摄像机等）
        // e.g.: EventBus.Trigger("PlayerExploded", transform.position);
    }

    #endregion

    #region Utilities: resolve durations from LevelConfig / timed events

    /// <summary>
    /// 正常模式下尝试解析 turning red 的时长：
    /// 优先使用 LevelManager.CurrentConfig.turningRedDuration（若 >0）；
    /// 否则尝试从 timedEvents 中找到 HeadTurningRed 到 HeadOnFire 的时间差作为 duration；
    /// 若都失败，返回 0（调用方会 fallback 到组件内默认字段）。
    /// </summary>
    private float ResolveTurningRedDuration()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentConfig != null)
        {
            var cfg = LevelManager.Instance.CurrentConfig;
            if (cfg.turningRedDuration > 0f) return cfg.turningRedDuration;

            // 从 timedEvents 中推断时长(HeadTurningRed -> HeadOnFire)
            if (cfg.timedEvents != null)
            {
                float tStart = float.NaN, tNext = float.NaN;
                foreach (var evt in cfg.timedEvents)
                {
                    if (evt.eventType == TimedEventData.TimedEventType.ChangePlayerPhase)
                    {
                        if (evt.parameter.Equals(PlayerController.PlayerPhase.HeadTurningRed.ToString(), System.StringComparison.OrdinalIgnoreCase))
                            tStart = evt.triggerTime;
                        if (evt.parameter.Equals(PlayerController.PlayerPhase.HeadOnFire.ToString(), System.StringComparison.OrdinalIgnoreCase))
                            tNext = evt.triggerTime;
                    }
                }
                if (!float.IsNaN(tStart) && !float.IsNaN(tNext))
                {
                    // careful with TriggerMode: if one is FromEnd, semantics differ — for simplicity assume FromStart here
                    return Mathf.Max(0f, tNext - tStart);
                }
            }
        }
        return 0f;
    }

    /// <summary>
    /// 正常模式下尝试解析 onFireDuration：
    /// 优先使用 LevelManager.CurrentConfig.onFireDuration；否则尝试从 HeadOnFire 到 HeadBoom 的 timedEvents 差值。
    /// </summary>
    private float ResolveOnFireDuration()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentConfig != null)
        {
            var cfg = LevelManager.Instance.CurrentConfig;
            if (cfg.onFireDuration > 0f) return cfg.onFireDuration;

            if (cfg.timedEvents != null)
            {
                float tStart = float.NaN, tNext = float.NaN;
                foreach (var evt in cfg.timedEvents)
                {
                    if (evt.eventType == TimedEventData.TimedEventType.ChangePlayerPhase)
                    {
                        if (evt.parameter.Equals(PlayerController.PlayerPhase.HeadOnFire.ToString(), System.StringComparison.OrdinalIgnoreCase))
                            tStart = evt.triggerTime;
                        if (evt.parameter.Equals(PlayerController.PlayerPhase.HeadBoom.ToString(), System.StringComparison.OrdinalIgnoreCase))
                            tNext = evt.triggerTime;
                    }
                }
                if (!float.IsNaN(tStart) && !float.IsNaN(tNext))
                {
                    return Mathf.Max(0f, tNext - tStart);
                }
            }
        }
        return 0f;
    }

    #endregion
}
