using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AirWall : MonoBehaviour
{
    [Header("对机制控制器的依赖")]
    public MachineController machineController;

    // 内部状态
    private Coroutine runningCoroutine = null;

    private void OnEnable()
    {
        if (machineController != null)
        {
            machineController.OnMEventPublish += OnMEventReceive;
        }
    }
    private void OnDisable()
    {
        // 解绑事件
        if (machineController != null)
        {
            machineController.OnMEventPublish -= OnMEventReceive;
        }

        // 停止任何正在运行的协程
        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }
    }

    //接收广播，执行协程
    private void OnMEventReceive(MachineController.MachineEvent receiveEvent)
    {
        //Debug.Log("接受到广播，判断中");
        if (receiveEvent == MachineController.MachineEvent.AirWall)
        {
            runningCoroutine = StartCoroutine(DestoryItself());
        }
    }

    private IEnumerator DestoryItself()
    {
        Debug.Log("成功销毁空气墙");
        Destroy(gameObject);
        yield return null;
    }
}
