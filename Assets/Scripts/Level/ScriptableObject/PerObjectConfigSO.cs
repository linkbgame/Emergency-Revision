// PerObjectConfigSO.cs
using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Level/PerObjectConfig")]
public class PerObjectConfigSO : ScriptableObject
{
    [Tooltip("场景中用于匹配的唯一 id（同一个 LevelConfig 内必须唯一）")]
    public string objectId;

    [Tooltip("可选类型标签，便于策划过滤/分类")]
    public string type;

    [TextArea(4, 20), Tooltip("自由 JSON 或额外文本数据（如需）")]
    public string jsonData;

    // 可扩展：如果你期望强类型字段，直接在这里加入（例如 int damage; float delay; 等）
}
