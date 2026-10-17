// ILevelInitializable.cs
using System.Collections;

public interface ILevelInitializable
{
    /// <summary>
    /// 用于与 LevelConfig.perObjectConfigs 中的 objectId 匹配。
    /// 实现类通常在 Inspector 用 [SerializeField] 提供该值。
    /// </summary>
    string ObjectId { get; }

    /// <summary>
    /// 初始化入口。
    /// cfgForThisObject: 传入 PerObjectConfigSO（或 null）。
    /// 返回 IEnumerator 可用于异步初始化（加载资源、等待动画等），若无需异步则返回 null。
    /// </summary>
    IEnumerator Initialize(object cfgForThisObject, LevelContext context);
}
