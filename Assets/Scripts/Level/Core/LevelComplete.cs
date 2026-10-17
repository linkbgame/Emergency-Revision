using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelComplete : MonoBehaviour
{
    [Tooltip("默认层级为WinCondition，可调整")]
    public string winConditionLayerName = "WinCondition"; // 通关条件层的名称

    private void OnTriggerEnter(Collider other)
    {
        int winConditionMask = 1 << LayerMask.NameToLayer(winConditionLayerName);
        Debug.Log("触发检测到物体 " + other.gameObject.name + " ——识别是否为通关条件...");
        if ((winConditionMask & (1 << other.gameObject.layer)) != 0)
        {
            Debug.Log("该物体为通关条件");
            LevelManager.Instance.RunVictoryFlow();
            
        }
        else
        {
            Debug.Log("该物体不是通关条件");
        }
    }
}
