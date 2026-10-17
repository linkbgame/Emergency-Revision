using UnityEngine;

/// <summary>
/// 可编辑的关卡配置（每个关卡一个 LevelConfig asset）。
/// 包含 sceneName、时间限制、定时事件、等可视化字段，方便策划在 Inspector 配置关卡。
/// </summary>
[CreateAssetMenu(menuName = "Level/LevelConfig")]
public class LevelConfig : ScriptableObject
{
    [Header("用于唯一标识关卡，例如 'level_01'")]
    public string levelId;//（可用于存档/解锁）

    [Header("要加载的场景名称（Build Settings 中的场景名）")]
    public string sceneName;

    [Header("该关卡中的倒计时（秒）")]
    public float timeLimitSeconds = 60f;

    [Header("玩家头完全变红所需时长（秒）")]
    public float turningRedDuration;

    [Header("着火效果持续时间（秒）")]
    public float onFireDuration;

    [Header("关卡内部的定时事件（在指定时间触发）")]
    public TimedEventData[] timedEvents;

    [Header("入场退场动画（镜头动画、321倒计时、胜利横幅）")]
    public bool playIntroCutscene = true;
    public string introCutsceneId;
    public bool playVictoryCutscene = true;
    public string victoryCutsceneId;

    [Header("按场景对象配置每个对象（直接引用 PerObjectConfigSO asset）")]
    public PerObjectConfigSO[] perObjectConfigs;

    //Npc生成器
}
