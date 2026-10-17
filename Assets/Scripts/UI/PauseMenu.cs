using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>暂停菜单 UI。</summary>
public class PauseMenu : MonoBehaviour
{
    public event Action OnReturnToMenu;

    public void Show()
    {
        gameObject.SetActive(true);
        // block other input via LevelManager.EnableInput(false)
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    //UI 按钮调用
    public void OnReturnToMenuButton()
    {
        OnReturnToMenu?.Invoke();
    }

    //UI 按钮调用
    public void OnResumeButton()
    {   
        // Typically LevelManager.Resume() will be invoked by binding in scene or via event
        LevelManager.Instance?.Resume();
    }
}
