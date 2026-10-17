using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD : Head Up Display 抬头显示（显示剩余时间，倒计时UI等）。</summary>
public class HUD : MonoBehaviour
{
    //塞尔达老假面的时间UI
    private Text timeText;

    private void Awake()
    {
        timeText = GetComponentInChildren<Text>();
    }

    public void SetTime(float seconds)
    {
        // Update time text/visuals
        timeText.text = Mathf.CeilToInt(seconds).ToString();
    }
}
