using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[Serializable]
public class SoundEntry
{
    public string key;            // 用于代码调用的 key（例如 "menu", "level", "jump"）
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f; // 本条目的相对音量（0..1）
    public bool loop = false;     // 对 BGM 有用
}

public enum PhaseSfxKey
{
    None,
    HeadTurningRed,
    HeadOnFire,
    HeadBoom,
}

/// <summary>
/// AudioManager: 单例（常驻）。管理 BGM / SFX / PhaseSFX，支持淡入/淡出、SFX 池、分组音量和持久化设置。
/// - 把这个 prefab 放在 Menu 场景（或 Core 场景），Inspector 上配置 BGM 和 SFX 列表、AudioMixerGroup（可选）以及音量默认值。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("AudioSources (默认两个AudioSource)")]
    [Tooltip("用于播放正在播放的 BGM（单通道）")]
    public AudioSource bgmSource;
    [Tooltip("备用 BGM source，用于 crossfade（可选）")]
    public AudioSource bgmSourceSecondary;

    [Tooltip("用于短 SFX 的 one-shot 池，避免频繁创建销毁 AudioSource")]
    public int oneShotPoolSize = 8;
    private List<AudioSource> oneShotPool = new List<AudioSource>();

    [Header("Mixer (可选但程序没做)")]
    public AudioMixerGroup masterMixerGroup; // 如果使用 AudioMixer，则把 group 指定到 sources 的 output

    [Header("BGM entries (设置 clips + keys)")]
    public SoundEntry[] bgmEntries;

    [Header("SFX entries (设置 clips + keys)")]
    public SoundEntry[] sfxEntries;

    [Header("Phase SFX (explicit keys)")]
    public SoundEntry headTurningRedSfx;
    public SoundEntry headOnFireSfx;
    public SoundEntry headBoomSfx;

    [Header("Volume (0..1, persisted)")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Fade 设置")]
    [Tooltip("淡入/淡出时间（秒），使用 UnscaledDeltaTime，以在暂停时仍能淡出）")]
    [Range(0, 0.5f)]
    public float defaultFadeTime = 0.2f;

    // runtime maps
    private Dictionary<string, SoundEntry> bgmMap = new Dictionary<string, SoundEntry>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, SoundEntry> sfxMap = new Dictionary<string, SoundEntry>(StringComparer.OrdinalIgnoreCase);

    // fade coroutine handles
    private Coroutine bgmFadeCoroutine;
    private Coroutine crossfadeCoroutine;

    private void Awake()
    {
        // singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 初始化映射表
        BuildMaps();

        // 初始化 one-shot pool
        for (int i = 0; i < oneShotPoolSize; i++)
        {
            var go = new GameObject($"AudioManager_OneShot_{i}");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            if (masterMixerGroup != null) src.outputAudioMixerGroup = masterMixerGroup;
            src.spatialBlend = 0f; // UI / non-spatial by default
            oneShotPool.Add(src);
        }

        // 把 mixer group 同步给 sources（如果有）
        if (masterMixerGroup != null)
        {
            if (bgmSource != null) bgmSource.outputAudioMixerGroup = masterMixerGroup;
            if (bgmSourceSecondary != null) bgmSourceSecondary.outputAudioMixerGroup = masterMixerGroup;
            foreach (var src in oneShotPool) src.outputAudioMixerGroup = masterMixerGroup;
        }

        // load persisted volume prefs (optional)
        masterVolume = PlayerPrefs.GetFloat("audio.master", masterVolume);
        musicVolume = PlayerPrefs.GetFloat("audio.music", musicVolume);
        sfxVolume = PlayerPrefs.GetFloat("audio.sfx", sfxVolume);

        ApplyVolumeImmediate();

        PlayBGM("menu");
    }

    private void BuildMaps()
    {
        bgmMap.Clear();
        sfxMap.Clear();
        if (bgmEntries != null)
        {
            foreach (var e in bgmEntries)
            {
                if (e == null || string.IsNullOrEmpty(e.key)) continue;
                if (!bgmMap.ContainsKey(e.key)) bgmMap.Add(e.key, e);
            }
        }
        if (sfxEntries != null)
        {
            foreach (var e in sfxEntries)
            {
                if (e == null || string.IsNullOrEmpty(e.key)) continue;
                if (!sfxMap.ContainsKey(e.key)) sfxMap.Add(e.key, e);
            }
        }
    }

    #region BGM Controls

    /// <summary>播放指定 key 的 BGM（按配置找 clip）。若 fadeTime &lt;= 0 则立即切换，否则做淡入/淡出。</summary>
    /// <param name="key">BGM 的 key（在 bgmMap 中查找）</param>
    /// <param name="fadeTime">淡入/淡出时长（秒）。若为负数使用 defaultFadeTime；若为 0 或者非常小，则立即切换播放。</param>
    public void PlayBGM(string key, float fadeTime = -1f)
    {
        if (!bgmMap.TryGetValue(key, out var entry) || entry.clip == null)
        {
            Debug.LogWarning($"[AudioManager] PlayBGM: key '{key}' not found or clip null.");
            return;
        }
        float targetVol = Mathf.Clamp01(entry.volume * musicVolume * masterVolume);

        // 如果 fadeTime 未传或为负，则使用默认值
        if (fadeTime < 0f) fadeTime = defaultFadeTime;

        PlayBGMClip(entry.clip, targetVol, entry.loop, fadeTime);
    }

    /// <summary>播放指定 Clip（内部使用）。会选择 crossfade（若 secondary 存在）或单源淡切。</summary>
    /// <param name="clip">要播放的 AudioClip</param>
    /// <param name="targetVolume">播放时的目标音量（0..1）</param>
    /// <param name="loop">是否循环</param>
    /// <param name="fadeTime">淡入/淡出时间（秒）</param>
    public void PlayBGMClip(AudioClip clip, float targetVolume = 1f, bool loop = true, float fadeTime = -1f)
    {
        if (bgmSource == null)
        {
            Debug.LogWarning("[AudioManager] PlayBGMClip: bgmSource is null.");
            return;
        }

        if (fadeTime < 0f) fadeTime = defaultFadeTime;

        // 护栏：当 fadeTime <= 0 时直接切换（同步播放），避免除以 0 或插值问题
        if (fadeTime <= 0f)
        {
            // 直接替换并播放（立即生效）
            StopBgmSourcesImmediate(); // 停止 secondary（如果存在）
            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.volume = Mathf.Clamp01(targetVolume);
            bgmSource.Play();
            return;
        }

        // 有淡入时间时，选择 crossfade / fade 方法
        if (bgmSourceSecondary != null)
        {
            if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
            crossfadeCoroutine = StartCoroutine(CrossfadeBgmCoroutine(clip, targetVolume, loop, fadeTime));
        }
        else
        {
            if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
            bgmFadeCoroutine = StartCoroutine(FadeToBgmSimple(clip, targetVolume, loop, fadeTime));
        }
    }

    /// <summary>立即停止并清理两个 BGM source（不淡出）。</summary>
    private void StopBgmSourcesImmediate()
    {
        if (bgmSourceSecondary != null)
        {
            bgmSourceSecondary.Stop();
            bgmSourceSecondary.clip = null;
            bgmSourceSecondary.volume = 0f;
        }
        // Note: 我们不停止主 source（bgmSource）前面的逻辑会覆盖它
    }


    /// <summary>停止当前 BGM（带淡出）</summary>
    public void StopBGM(float fadeTime = -1f)
    {
        if (fadeTime < 0f) fadeTime = defaultFadeTime;
        if (bgmSource == null) return;
        if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
        bgmFadeCoroutine = StartCoroutine(FadeOutAndStop(bgmSource, fadeTime));
        if (bgmSourceSecondary != null)
        {
            if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
            crossfadeCoroutine = StartCoroutine(FadeOutAndStop(bgmSourceSecondary, fadeTime));
        }
    }

    /// <summary>单通道淡切实现：先淡出当前 source（若在播放），然后替换 clip 并淡入</summary>
    private IEnumerator FadeToBgmSimple(AudioClip newClip, float targetVolume, bool loop, float fadeTime)
    {
        float t = 0f;
        float startVol = bgmSource.isPlaying ? bgmSource.volume : 0f;

        // fade out to 0
        for (t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            bgmSource.volume = Mathf.Lerp(startVol, 0f, t / fadeTime);
            yield return null;
        }
        bgmSource.volume = 0f;

        // swap and play
        bgmSource.clip = newClip;
        bgmSource.loop = loop;
        bgmSource.Play();

        // fade in
        for (t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            bgmSource.volume = Mathf.Lerp(0f, targetVolume, t / fadeTime);
            yield return null;
        }
        bgmSource.volume = targetVolume;
    }

    /// <summary>双通道 crossfade：secondary 播 newClip，两个 source 同时做线性插值，完成后停止旧 source。</summary>
    private IEnumerator CrossfadeBgmCoroutine(AudioClip newClip, float targetVolume, bool loop, float fadeTime)
    {
        var from = bgmSource;
        var to = bgmSourceSecondary;

        // 准备 to
        to.clip = newClip;
        to.loop = loop;
        to.volume = 0f;
        to.Play();

        float t = 0f;
        float fromStartVol = from.isPlaying ? from.volume : 0f;

        for (t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            float p = t / fadeTime;
            from.volume = Mathf.Lerp(fromStartVol, 0f, p);
            to.volume = Mathf.Lerp(0f, targetVolume, p);
            yield return null;
        }

        // 确保最终状态
        from.volume = 0f;
        to.volume = targetVolume;

        // 停止旧 source
        from.Stop();
        from.clip = null;

        // swap refs: 保证主源始终为 bgmSource（方便后续单通道接口使用）
        // 简单实现：把 bgmSource 的引用内容替换为 secondary 的 clip/volume 等（不需要交换 GameObjects）
        bgmSource.clip = to.clip;
        bgmSource.loop = to.loop;
        bgmSource.volume = to.volume;
        bgmSource.Play();

        // 停止 secondary
        to.Stop();
        to.clip = null;
        to.volume = 0f;

        crossfadeCoroutine = null;
    }

    /// <summary>淡出并停止指定 source（渐隐结束 stop）。当 fadeTime <= 0 则直接 stop。</summary>
    private IEnumerator FadeOutAndStop(AudioSource src, float fadeTime)
    {
        if (src == null) yield break;

        if (fadeTime <= 0f)
        {
            src.Stop();
            src.clip = null;
            src.volume = 0f;
            yield break;
        }

        fadeTime = Mathf.Max(0.0001f, fadeTime);
        float startVol = src.volume;
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            float p = t / fadeTime;
            src.volume = Mathf.Lerp(startVol, 0f, p);
            yield return null;
        }
        src.volume = 0f;
        src.Stop();
        src.clip = null;
    }

    #endregion

    #region SFX Controls (one-shot pool)

    /// <summary>播放某个 key 的 SFX（非阻塞），按 entry.volume * sfxVolume * masterVolume 计算实际音量。</summary>
    public void PlaySFX(string key)
    {
        if (!sfxMap.TryGetValue(key, out var entry) || entry.clip == null) return;
        PlayOneShot(entry.clip, entry.volume * sfxVolume * masterVolume);
    }

    /// <summary>播放任意 clip（非阻塞）</summary>
    public void PlayOneShot(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        var src = GetFreeOneShotSource();
        if (src == null) return;
        src.volume = Mathf.Clamp01(volume);
        src.clip = clip;
        src.PlayOneShot(clip, src.volume);
    }

    private AudioSource GetFreeOneShotSource()
    {
        // try to find a non-playing source first
        foreach (var s in oneShotPool)
        {
            if (!s.isPlaying) return s;
        }
        // otherwise reuse the oldest one
        if (oneShotPool.Count > 0) return oneShotPool[0];
        return null;
    }

    #endregion

    #region Phase-specific API

    public void PlayPhaseSfx(PhaseSfxKey key)
    {
        switch (key)
        {
            case PhaseSfxKey.HeadTurningRed:
                PlayConfigured(headTurningRedSfx);
                break;
            case PhaseSfxKey.HeadOnFire:
                PlayConfigured(headOnFireSfx);
                break;
            case PhaseSfxKey.HeadBoom:
                PlayConfigured(headBoomSfx);
                break;
            default:
                break;
        }
    }

    private void PlayConfigured(SoundEntry entry)
    {
        if (entry == null || entry.clip == null) return;
        PlayOneShot(entry.clip, entry.volume * sfxVolume * masterVolume);
    }

    #endregion

    #region Volume / Persistence

    public void SetMasterVolume(float v)
    {
        masterVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat("audio.master", masterVolume);
        ApplyVolumeImmediate();
    }
    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat("audio.music", musicVolume);
        ApplyVolumeImmediate();
    }
    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat("audio.sfx", sfxVolume);
        ApplyVolumeImmediate();
    }

    private void ApplyVolumeImmediate()
    {
        // Apply to bgm sources
        if (bgmSource != null)
            bgmSource.volume = Mathf.Clamp01(musicVolume * masterVolume);
        if (bgmSourceSecondary != null)
            bgmSourceSecondary.volume = Mathf.Clamp01(musicVolume * masterVolume);
        // pool sources volume will be set per-play in PlayOneShot (entry.volume * sfxVolume * masterVolume)
    }

    #endregion

    #region Utilities

    /// <summary>在 Editor 或运行时可调用，刷新字典，方便热更新 Inspector</summary>
    public void RebuildMaps()
    {
        BuildMaps();
    }

    #endregion
}
