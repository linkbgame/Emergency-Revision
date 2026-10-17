// CutsceneManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// CutsceneManager：轻量的场景内 Cutscene 播放器。
/// - 支持按 id 播放场景内的 PlayableDirector（Timeline）。
/// - 播放期间可以通过回调/事件禁用玩家输入 NpcAI，结束后恢复，并通知完成。
/// - 若找不到 id，会立即调用完成回调（容错）。
/// </summary>
public class CutsceneManager : MonoBehaviour
{
    public static CutsceneManager Instance { get; private set; }

    // 可在 Inspector 中预绑定场景常用的 PlayableDirector（可为空，运行时也会自动查找）
    [Tooltip("可选：手动在 Inspector 绑定可重用的 PlayableDirectors（按 id 名称区分）")]
    public PlayableDirector[] directors;

    // 运行时字典（name -> director）
    private Dictionary<string, PlayableDirector> directorMap = new Dictionary<string, PlayableDirector>(StringComparer.OrdinalIgnoreCase);

    public event Action<string> OnCutsceneStarted;
    public event Action<string> OnCutsceneFinished;

    private void Awake()
    {
        BuildMap();
    }

    private void BuildMap()
    {
        directorMap.Clear();

        // 优先使用 Inspector 中绑定的 directors
        if (directors != null)
        {
            foreach (var d in directors)
            {
                if (d == null) continue;
                if (!directorMap.ContainsKey(d.name)) directorMap.Add(d.name, d);
            }
        }

        // 再把场景中所有 PlayableDirector 补进去（避免重复 key）
        var all = FindObjectsOfType<PlayableDirector>();
        foreach (var d in all)
        {
            if (d == null) continue;
            if (!directorMap.ContainsKey(d.name))
                directorMap.Add(d.name, d);
        }
    }

    /// <summary>
    /// 同步调用；如果存在对应 director 则播放并在完成时触发 onComplete 回调；否则立刻回调（容错）。
    /// - disableInputDuring: 若为 true，会触发 LevelManager/PlayerController 禁用输入（仅调用回调，实际禁用由 LevelManager 做）。
    /// </summary>
    public void Play(string id, Action onComplete = null, bool disableInputDuring = true)
    {
        StartCoroutine(PlayCoroutine_Internal(id, onComplete, disableInputDuring));
    }

    /// <summary>
    /// 可用于 StartCoroutine( CutsceneManager.Instance.PlayCoroutine("intro") ) 以等待结束。
    /// </summary>
    public IEnumerator PlayCoroutine(string id, bool disableInputDuring = true)
    {
        bool done = false;
        Play(id, () => done = true, disableInputDuring);
        while (!done) yield return null;
    }

    private IEnumerator PlayCoroutine_Internal(string id, Action onComplete, bool disableInputDuring)
    {
        // 找 director
        if (directorMap.Count == 0) BuildMap();

        PlayableDirector dir = null;
        if (!string.IsNullOrEmpty(id) && directorMap.TryGetValue(id, out var found))
        {
            dir = found;
        }

        // 如果没有对应的 director：直接回调完成
        if (dir == null)
        {
            Debug.LogWarning($"CutsceneManager: 无法找到 id='{id}' 的 PlayableDirector (继续进入游戏)。");
            onComplete?.Invoke();
            OnCutsceneFinished?.Invoke(id);
            yield break;
        }

        // 播放前通知
        OnCutsceneStarted?.Invoke(id);

        // 触发禁用输入/AI 的事件（LevelManager 订阅此事件以禁用 Player/NPC）
        // 为兼容性，LevelManager 会在播放前禁用输入（见集成示例）
        if (disableInputDuring)
        {
            // LevelManager/PlayerController 订阅了 OnCutsceneStarted 才能响应
        }

        bool finished = false;
        void OnStopped(PlayableDirector p)
        {
            finished = true;
        }

        // subscribe to stopped
        dir.stopped += OnStopped;

        // Play (PlayableDirector 会驱动 Timeline，包括相机动画)
        dir.Play();

        // 等待完成或播放停止
        while (!finished)
        {
            yield return null;
        }

        // unsubscribe
        dir.stopped -= OnStopped;

        // 播放结束通知
        OnCutsceneFinished?.Invoke(id);

        // 回调
        onComplete?.Invoke();
    }

    /// <summary>
    /// 强制停止正在播放的 cutscene（如果有）。
    /// </summary>
    public void Stop(string id)
    {
        if (directorMap.Count == 0) BuildMap();
        if (directorMap.TryGetValue(id, out var dir))
        {
            if (dir.state == PlayState.Playing)
            {
                dir.time = dir.duration;
                dir.Stop();
                OnCutsceneFinished?.Invoke(id);
            }
        }
    }

    // 可选：编辑器 / 运行时重新构建 map（例如在动态生成场景时）
    public void RebuildMap()
    {
        BuildMap();
    }
}
