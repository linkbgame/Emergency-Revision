using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//疑惑：三个机制脚本都使用协程的意义所在
//待测，还没放音效
public class TrafficLight : MonoBehaviour
{
    [Header("对机制控制器的依赖")]
    public MachineController machineController;

    [Header("灯泡子对象")]
    public GameObject RedLight;
    public GameObject GreenLight;

    private Renderer redRenderer;
    private Renderer greenRenderer;

    [Header("频闪时间（总时长）")]
    public float flashingDuration = 6f;

    [Header("频闪半周期（秒）")]
    public float halfT = 1f;

    // 内部状态
    private Coroutine runningCoroutine = null;
    private MaterialPropertyBlock mpb;

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        redRenderer = RedLight.GetComponent<Renderer>();
        greenRenderer = GreenLight.GetComponent<Renderer>();

        // 初始颜色
        SetRendererColor(redRenderer, Color.black);
        SetRendererColor(greenRenderer, Color.green);

        if (machineController != null)
            machineController.OnMEventPublish += OnMEventReceive;
    }

    private void OnDisable()
    {
        if (machineController != null)
            machineController.OnMEventPublish -= OnMEventReceive;

        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }
    }

    private void OnMEventReceive(MachineController.MachineEvent receiveEvent)
    {
        if (receiveEvent != MachineController.MachineEvent.Light) return;

        // 如果正在运行则先停止（采用覆盖策略）
        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }

        runningCoroutine = StartCoroutine(TrafficLightChange());
    }

    private IEnumerator TrafficLightChange()
    {
        // 重置计时
        float elapsed = 0f;

        // 让绿灯按 halfT 周期闪烁，使用 WaitForSecondsRealtime 保证不受 timeScale 影响（可改为 WaitForSeconds）
        bool isGreenOn = true;
        SetRendererColor(redRenderer, Color.black);
        SetRendererColor(greenRenderer, Color.green);
        isGreenOn = true;

        while (elapsed < flashingDuration)
        {
            // 切换绿灯显示状态
            isGreenOn = !isGreenOn;
            SetRendererColor(greenRenderer, isGreenOn ? Color.green : Color.black);

            // 等待半个周期（不受 timeScale 的影响）
            yield return new WaitForSecondsRealtime(halfT);

            elapsed += halfT;
        }

        // 结束闪烁：红灯亮，绿灯灭
        SetRendererColor(redRenderer, Color.red);
        SetRendererColor(greenRenderer, Color.black);

        // 协程结束，清理引用
        runningCoroutine = null;
    }

    // 使用 MaterialPropertyBlock 来修改颜色，避免创建材质实例
    private void SetRendererColor(Renderer rend, Color color)
    {
        if (rend == null) return;
        mpb.Clear();
        rend.GetPropertyBlock(mpb);
        mpb.SetColor("_Color", color); // 注意：若用 HDRP/URP/不同 Shader，属性名可能是 "_BaseColor"
        rend.SetPropertyBlock(mpb);
    }
}

