using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>加载界面（进度条）。</summary>
public class LoadingUI : MonoBehaviour
{
    public void Show(float progress)
    {
        // 显示 loading panel 并设置进度条 value = progress (0..1)
        //文本输出“Loading... {progress*100}%”
        gameObject.SetActive(true);
        // TODO: set UI elements
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
