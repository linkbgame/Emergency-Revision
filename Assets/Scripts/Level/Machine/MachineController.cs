using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MachineController : MonoBehaviour
{
    //public static MachineController Instance { get; private set; }

    public enum MachineEvent
    {
        None,
        Car,
        AirWall,
        Light
    }

    public event Action<MachineEvent> OnMEventPublish;
    private Action<MachineEvent> onMachineEventRequestedHandler;

    #region Unity生命周期

    private void OnEnable()
    {
        // 准备 handler 引用并尝试自动订阅（若 LevelManager 已存在）
        onMachineEventRequestedHandler = HandleRequestedMEvent;
        TrySubscribeToLevelManager();
    }

    //单例模式没搞懂
    /*private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }*/

    private void Start()
    {
        // 再尝试一次（有时单例是在 Start 之后创建）
        TrySubscribeToLevelManager();
    }

    private void OnDisable()
    {
        // 确保解绑（防止在场景切换 / disable 时残留）
        UnsubscribeFromLevelManager();
    }

    private void OnDestroy()
    {
        // 保险性解绑（实例销毁）
        UnsubscribeFromLevelManager();
    }

    #endregion




















    #region 订阅绑定助手
    /// <summary>
    /// 尝试订阅 LevelManager（若 Instance 已就绪）。此方法可安全频繁调用（内部会检查）。
    /// </summary>
    private void TrySubscribeToLevelManager()
    {
        if (LevelManager.Instance != null)
        {
            // 防止重复订阅
            LevelManager.Instance.OnMachineEventRequested -= onMachineEventRequestedHandler;
            LevelManager.Instance.OnMachineEventRequested += onMachineEventRequestedHandler;
        }
    }

    /// <summary>
    /// 显式绑定到 LevelManager 的入口（供 LevelSceneInitializer 在注册 playerController 时调用）。
    /// 使用此方法可以保证在 LevelManager 常驻但 PlayerController 在场景后创建时及时绑定。
    /// </summary>
    public void BindToLevelManager()
    {
        TrySubscribeToLevelManager();
    }

    /// <summary>
    /// 取消订阅（在 OnDisable / OnDestroy / 替换绑定时调用）
    /// </summary>
    private void UnsubscribeFromLevelManager()
    {
        if (LevelManager.Instance != null && onMachineEventRequestedHandler != null)
        {
            LevelManager.Instance.OnMachineEventRequested -= onMachineEventRequestedHandler;
        }
    }

    #endregion

    #region LevelManager事件处理器

    /// <summary>
    /// LevelManager 发来机制事件请求时被调用（通过事件）。广播新的机制事件。
    /// 具体内容由 车 灯 空气墙 等订阅者实现。
    /// </summary>
    /// <param name="phase"></param>
    private void HandleRequestedMEvent(MachineEvent machineEvent)
    {
        //如果不做相同事件的判断会怎么样
        /*if (phase == currentPhase) return;

        // 更新当前相位
        currentPhase = phase;

        // 广播变化（表现层订阅者会执行渐变、粒子等）
        OnPhaseChanged?.Invoke(currentPhase);*/

        OnMEventPublish?.Invoke(machineEvent);

        Debug.Log($"[MachineController] 广播了当下机制事件 {machineEvent}");
    }

    #endregion
}
