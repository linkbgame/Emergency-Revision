using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>结果界面（失败界面）。</summary>
public class ResultUI : MonoBehaviour
{
    public event Action OnRetry;
    public event Action OnReturnToMenu;

    public void ShowFailure()
    {
        gameObject.SetActive(true);
        // setup failure text/buttons
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void OnRetryButton()
    {
        OnRetry?.Invoke();
    }

    public void OnReturnToMenuButton()
    {
        OnReturnToMenu?.Invoke();
    }
}
