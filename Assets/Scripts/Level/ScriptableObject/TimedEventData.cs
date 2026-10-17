using UnityEngine;

/// <summary>
/// 运行时由 LevelTimer 检测并触发的事件数据结构。可在 LevelConfig 中配置多个 TimedEventData。
/// </summary>
/// <remarks>
/// enum TriggerMode 定义了触发时间的参考点（从开始或从剩余时间）。
/// enum TimedEventType 定义了事件的类型（激活陷阱、切换玩家阶段、播放动画等）。
/// enum 的值会在 LevelTimer 中的 switch 语句中使用，以执行相应的逻辑。
/// enum 的值应与 LevelTimer.cs 中的实现保持同步。枚举nb
/// </remarks>
[System.Serializable]
public class TimedEventData
{
    public enum TriggerMode
    {
        FromStart, // triggerTime counted from level start (0..)
        FromEnd    // triggerTime counted from remaining time (timeLimit - t <= triggerTime)
    }

    public enum TimedEventType
    {
        None,
        ActivateMachine,
        ChangePlayerPhase,
        DeactivateTrap,
        PlayCutscene,
        SendMessageToObject
    }

    [Tooltip("触发模式（从开始或从剩余时间）")]
    public TriggerMode mode = TriggerMode.FromStart;

    [Tooltip("触发时间（秒），开始多少秒触发或距离结束时间还有多少秒触发")]
    public float triggerTime = 0f;

    [Tooltip("事件类型")]
    public TimedEventType eventType = TimedEventType.None;

    [Tooltip("玩家状态")]
    public PlayerController.PlayerPhase playerPhase;// 仅在 eventType 为 ChangePlayerPhase 时使用

    [Tooltip("机关类型")]
    public MachineController.MachineEvent machineEvent;// 仅在 eventType 为 ActivateMachine 时使用

    [Tooltip("（之后用）参数字符串（例如 trapId、animTrigger、objectName 等）")]
    // 具体含义依据 eventType 而定
    public string parameter;

    // 运行时标志：事件是否已被触发（不序列化到 asset）
    [System.NonSerialized] public bool hasFired = false;
}
