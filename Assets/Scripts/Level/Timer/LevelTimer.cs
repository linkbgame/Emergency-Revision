using System;
using UnityEngine;

/// <summary>
/// 关卡倒计时组件（可作为 LevelManager 的子系统或单独组件）。
/// 负责精确控制剩余时间、Pause/Resume、以及广播 Tick/TimeUp 事件。
/// </summary>
public class LevelTimer : MonoBehaviour
{
    private float timeRemaining;
    private bool running = false;
    private bool paused = false;

    /// <summary>每帧广播剩余时间（参数：剩余时间，单位秒）。订阅者可以更新 HUD。</summary>
    public event Action<float> OnTick;

    /// <summary>当时间归零时触发一次。</summary>
    public event Action OnTimeUp;

    /// <summary>是否正在计时。</summary>
    public bool IsRunning => running && !paused;

    /// <summary>剩余时间（秒）。</summary>
    public float TimeRemaining => Mathf.Max(0f, timeRemaining);

    /// <summary>设置起始时间（在 StartTimer 前调用）。</summary>
    public void SetTime(float seconds)
    {
        timeRemaining = Mathf.Max(0f, seconds);
    }

    /// <summary>开始计时（从 SetTime 的值开始）。</summary>
    public void StartTimer()
    {
        running = true;
        paused = false;
    }

    /// <summary>暂停计时（保留剩余时间）。</summary>
    public void PauseTimer()
    {
        paused = true;
    }

    /// <summary>恢复计时。</summary>
    public void ResumeTimer()
    {
        if (running)
            paused = false;
    }

    /// <summary>彻底停止计时（并重置状态，但不改变 timeRemaining）。</summary>
    public void StopTimer()
    {
        running = false;
        paused = false;
    }

    private void Update()
    {
        // 只在运行且未暂停时减少时间
        if (!running || paused) return;

        // 使用真实世界 deltaTime（不依赖 timeScale）
        float dt = Time.unscaledDeltaTime;
        timeRemaining -= dt;

        // 广播剩余时间，所有订阅者将收到更新（HUD 等）
        OnTick?.Invoke(Mathf.Max(0f, timeRemaining));

        // 时间到，触发一次 OnTimeUp
        if (timeRemaining <= 0f)
        {
            running = false;
            OnTimeUp?.Invoke();
        }
    }
}
