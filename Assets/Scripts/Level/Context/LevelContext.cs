
using System;
using UnityEngine;

/// <summary>
/// 运行时上下文，传入给 ILevelInitializable.Initialize，方便组件访问 LevelConfig / LevelManager / 服务等
/// （轻量、只读引用）
/// </summary>
public class LevelContext
{
    public LevelConfig LevelConfig { get; private set; }
    public LevelManager LevelManager { get; private set; }

    // 常用全局服务（按需扩展）
    public AudioManager Audio => AudioManager.Instance;
    public CutsceneManager Cutscene => CutsceneManager.Instance;

    public LevelContext(LevelConfig cfg, LevelManager lm)
    {
        LevelConfig = cfg;
        LevelManager = lm;
    }
}
