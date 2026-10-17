
using UnityEngine;
using System;
using UnityEngine.SceneManagement;


public class GameEntry : MonoBehaviour
{
    private CanvasGroup group;
    private float fadeInDuration = 3.0f;
    private float startTime;
    private float elapsedTime;

    private void Awake()
    {
        group = gameObject.GetComponent<CanvasGroup>();
        group.alpha = 0;

        //获取开始时间
        startTime = Time.time;
    }

    private void Update()
    {
        elapsedTime = Time.time - startTime;
        
        PlaySplash();
        OnStartPressed();
    }

    /// <summary>淡入标题并显示“请按鼠标左键开始”。</summary>
    /// <remarks>
    /// 数学计算得到 elapsedTime / fadeInDuration，该值从 0 线性变化到 1，alpha值也从 0 线性变化到 1。
    /// </remarks>
    public void PlaySplash()
    {
        if (elapsedTime < fadeInDuration)
        {
            group.alpha = Mathf.Clamp01(elapsedTime / fadeInDuration);
        }
        else
        {
            group.alpha = 1;
        }
    }

    /// <summary>处理玩家按键，切换到 Menu Scene（同步或异步）。</summary>
    public void OnStartPressed()
    {
        //淡入未完成时不处理按键
        if (elapsedTime < fadeInDuration)
        {
            return;
        }
        //按下鼠标左键    
        if (Input.GetMouseButtonDown(0))
        {
            OnPlayerStart?.Invoke();
            SceneManager.LoadScene("Menu", LoadSceneMode.Single);
        }
    }

    /// <summary>事件：当玩家点击开始（只是用于 UI / 统计）</summary>
    public event Action OnPlayerStart;
}
