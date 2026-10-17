using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// trigger时间即为鸣笛时间，统一鸣笛，启动和刹车时间自定义
/// </summary>
public class Car : MonoBehaviour
{
    [Header("对机制控制器的依赖")]
    public MachineController machineController;

    [Tooltip("鸣笛之后多少秒开始移动")]public float afterHonkToMove = 3f;
    [Tooltip("移动之后多少秒开始停止")] public float afterMoveToStop = 4f;

    private Rigidbody rb;
    private bool hasStarted = false;//用于标记汽车是否已经启动
    private float engineForce = 400f;

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

        hasStarted = false;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    //默认汽车往自身x轴方向移动
    private void FixedUpdate()
    {
        if (hasStarted)
        {
            rb.AddForce(transform.right * engineForce , ForceMode.Impulse);
        }
    }

    /// <summary>
    /// 接收到 MachineController 的事件回调。
    /// 我们只在收到 MachineEvent.Car 时启动一次序列协程（若已有协程在运行则先停止它，避免重复）。
    /// </summary>
    private void OnMEventReceive(MachineController.MachineEvent receiveEvent)
    {
        if (receiveEvent != MachineController.MachineEvent.Car) return;

        // 如果已有在运行的序列，先停止它（取决于你想覆盖旧行为还是忽略新事件）为了覆盖旧行为
        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
            hasStarted = false;
        }

        // 启动新的序列协程（一次串联的流程）
        runningCoroutine = StartCoroutine(HonkThenMoveThenDestroySequence());
    }

    /// <summary>
    /// 串联协程：鸣笛 -> 等待 -> 开始行驶 -> 等待 -> 销毁
    /// 使用 WaitForSecondsRealtime 如果希望不受 Time.timeScale 影响（例如暂停时仍计时）
    /// </summary>
    private IEnumerator HonkThenMoveThenDestroySequence()
    {
        // 1. 播放鸣笛音效（容错检查）
        AudioManager.Instance?.PlaySFX("carHonk");

        // 2. 等待鸣笛后到移动（你可以选择 WaitForSecondsRealtime）
        yield return new WaitForSeconds(afterHonkToMove);

        // 3. 启动移动状态
        hasStarted = true;

        // 4. 持续移动 afterMoveToStop 秒
        yield return new WaitForSeconds(afterMoveToStop);

        // 5. 销毁自己（或者优雅停靠/播放毁灭特效后销毁）
        Destroy(gameObject);

        // 清理协程引用（防止 OnDisable 重复 Stop）
        runningCoroutine = null;
    }
}
