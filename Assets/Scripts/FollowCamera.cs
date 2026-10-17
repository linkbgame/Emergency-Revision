using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//优化：相机可以陷进Player里，然后将Player透明度降低
//优化：相机碰撞检测可以使用SphereCast，效果会更好
[RequireComponent(typeof(Camera))]
public class FollowCamera : MonoBehaviour
{
    [Header("跟随")]
    [SerializeField, Tooltip("跟随目标")] private Transform m_focus;//跟随目标
    [SerializeField, Range(1f, 20f), Tooltip("相机与目标的距离")] private float m_distance = 10f;//相机与目标的距离
    [SerializeField, Min(0), Tooltip("相机球形的拍摄区域包裹目标，目标走出范围半径相机才跟随")] private float m_freeMoveRadius = 1.0f;//自由移动区域半径
    private Vector3 m_focusPoint;//焦点
    private Vector3 m_previousFocusPoint;//前一帧的焦点
    private float m_focusCentering = 0.5f;//每秒钟拉进到focus和target距离总长的一半

    [Header("旋转")]
    [SerializeField, Tooltip("相机初始旋转角度")] private Vector2 m_orbitAngles = new Vector2(45, 0);//初始角度
    [SerializeField, Range(1f, 360f), Tooltip("相机旋转速度（灵敏度）")] private float m_rotationSpeed = 180.0f;
    [SerializeField, Range(1f, 90f), Tooltip("多少度开始对齐速度减慢")] private float m_alignSmoothRange = 45f;
    [SerializeField, Range(-89f, 89f), Tooltip("最小俯仰角")] private float m_minVerticalAngle = 10f;
    [SerializeField, Range(-89f, 89f), Tooltip("最大俯仰角")] private float m_maxVerticalAngle = 45f;
    [SerializeField, Min(0), Tooltip("结束手动旋转，多少秒之后开始自动对齐")] private float m_alignDelay = 3f;   //自动对齐的延迟
    private float m_lastManualRotationTime;//记录上次发生手动旋转的时间     

    //碰撞
    private Camera m_regularCamera;
    private Vector3 CameraHalfExtents//相机的近裁剪平面
    {
        get
        {
            Vector3 halfExtents;
            halfExtents.y = m_regularCamera.nearClipPlane * Mathf.Tan(0.5f * Mathf.Deg2Rad * m_regularCamera.fieldOfView);
            halfExtents.x = halfExtents.y * m_regularCamera.aspect;
            halfExtents.z = 0f;
            return halfExtents;
        }
    }

    [Header("偏移")]
    [SerializeField, Tooltip("相机偏移向量")] private Vector3 m_offset = Vector3.up;
    [SerializeField, Tooltip("是否使用相机位置的偏移")] private bool useOffset = false;


    //回调函数，便于序列化使用
    private void OnValidate()
    {
        if (m_maxVerticalAngle < m_minVerticalAngle)
            m_maxVerticalAngle = m_minVerticalAngle;
    }

    private void Awake()
    {
        //锁定并隐藏鼠标
        /*Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;*/

        m_regularCamera = GetComponent<Camera>();
        m_focusPoint = m_focus.position;
        transform.localRotation = Quaternion.Euler(m_orbitAngles);
    }

    /// <summary>
    /// 更新相机的位置和旋转，以保持对目标的焦点，同时避免障碍物。
    /// </summary>
    private void LateUpdate()
    {
        //焦点的更新
        UpdateFocusPoint();

        //旋转角度的更新
        Quaternion lookRotation = UpdateRotation();
        Vector3 lookDirection = lookRotation * Vector3.forward;
        Vector3 lookPosition = m_focusPoint - lookDirection * m_distance;

        if (useOffset)
        {
            lookPosition += m_offset;
        }

        //位置的合法性检测，优化方向起点距离
        lookPosition = ValidateCameraPosition(lookPosition, lookRotation);

        transform.SetPositionAndRotation(lookPosition, lookRotation);
    }

    /// <summary>
    /// 更新相机的焦点位置（m_focusPoint），并保存上一帧的焦点（m_previousFocusPoint）。
    /// </summary>
    /// <remarks>
    /// - 当 m_freeMoveRadius &gt; 0 时，使用插值使相机焦点滞后跟随目标（m_focus），以获得平滑跟随效果；
    ///   插值因子 t 的计算使用了 Mathf.Pow(1 - m_focusCentering, Time.deltaTime) 来实现帧率无关的指数衰减平滑；
    /// - 若目标距离超过 m_freeMoveRadius，则限制最大移动量，避免焦点瞬移过远使相机跟不上主角；
    /// - 当 m_freeMoveRadius == 0 时，直接把焦点设为目标位置（无滞后）。
    /// </remarks>
    private void UpdateFocusPoint()
    {
        m_previousFocusPoint = m_focusPoint;//记录当前帧开始时焦点的位置，这一帧结束后视作前一帧的焦点
        Vector3 targetPoint = m_focus.position;

        if (m_freeMoveRadius > 0)
        {
            float distance = Vector3.Distance(targetPoint, m_focusPoint);
            float t = 1f; // 关键插值因子

            //玩家画面中心时刻对准主角
            if (distance > 0.01f)
            {
                // 返回 f 的 y 次幂，使用 Time.deltaTime 使得平滑在不同帧率下表现一致
                t = Mathf.Pow(1 - m_focusCentering, Time.deltaTime);
            }
            //玩家走出相机自由移动区域时，t更小相机才跟得上主角
            if (distance > m_freeMoveRadius)
            {
                // 取较小的插值因子，使插值靠近主角，让相机能够跟得住主角
                t = Mathf.Min(t, m_freeMoveRadius / distance);
            }

            // 线性插值：t 越接近 0 越靠近 targetPoint，越接近 1 越靠近原来 m_focusPoint
            m_focusPoint = Vector3.Lerp(targetPoint, m_focusPoint, t);
        }
        else
        {
            m_focusPoint = targetPoint; // 最原始方法（无滞后）
        }
    }

    /// <summary>
    /// 更新相机的旋转角度（手动输入）
    /// </summary>
    /// <remarks>
    /// 如果检测到手动输入，
    /// 则会重新计算并约束角度范围，
    /// 否则保持当前旋转角度不变。
    /// </remarks>
    /// <returns>代表相机旋转的新 Quaternion</returns>
    private Quaternion UpdateRotation()
    {
        if (ManualRotation() || AutoRotation())
        {
            ConstrainAngles();
            return Quaternion.Euler(m_orbitAngles);
        }
        else
        {
            return transform.localRotation;
        }
    }

    /// <summary>
    /// 验证并返回一个合法的相机位置，避免近裁剪面穿模（使用 BoxCast 检测）。
    /// </summary>
    /// <param name="lookPosition">计算得到的理想相机位置（基于焦点与距离）。</param>
    /// <param name="lookRotation">相机朝向（用于计算近裁剪面朝向与 BoxCast 方向）。</param>
    /// <returns>经碰撞检测与修正后的相机位置（确保近裁剪面不穿透场景物体）。</returns>
    /// <remarks>
    /// - 计算相机近裁剪面中心 rectPosition（lookPosition + lookDirection * nearClipPlane），
    ///   然后从角色位置 castFrom 对 rectPosition 做 BoxCast（体积检测），以判断路径上是否被场景阻挡；
    /// - 若检测到命中，则将 rectPosition 移动到命中点，并把 lookPosition 再减去 rectOffset，
    ///   从而确保近裁剪面不会穿入物体内部；
    /// </remarks>
    private Vector3 ValidateCameraPosition(Vector3 lookPosition, Quaternion lookRotation)
    {
        Vector3 lookDirection = lookRotation * Vector3.forward;

        //保证相机近裁剪平面（near clip plane）不穿透墙体。
        Vector3 rectOffset = lookDirection * m_regularCamera.nearClipPlane;
        Vector3 rectPosition = lookPosition + rectOffset;

        //从角色发射一条射线，检查到相机位置这一段路上会不会撞到墙
        Vector3 castFrom = m_focus.position;
        Vector3 castLine = rectPosition - castFrom;
        float castDistance = castLine.magnitude;
        Vector3 castDirection = castLine / castDistance;

        //1<<0，LayerMask.NameToLayer("Default")，检测默认层，二进制的1左移0位
        if (Physics.BoxCast(castFrom, CameraHalfExtents, castDirection,
                            out RaycastHit hit, lookRotation, castDistance, 1 << 0 | 1 << 14))
        {
            rectPosition = castFrom + castDirection * hit.distance;
            lookPosition = rectPosition - rectOffset;
        }

        return lookPosition;
    }

    /// <summary>
    /// 读取玩家输入并将其转换为相机绕目标旋转的角度（m_orbitAngles）。
    /// </summary>
    /// <returns>如果检测到手动输入并已应用到 m_orbitAngles，则返回 true；否则返回 false。</returns>
    /// <remarks>
    /// - 使用 Input.GetAxis 获取“Camera Vertical”和“Camera Horizontal”输入；水平轴取负值（习惯上将左右输入映射到绕 Y 轴的旋转方向）；
    /// - 为了避免浮点数比较问题，使用 float.Epsilon 判定非零输入；
    /// - 一旦接受到手动输入，更新 m_orbitAngles，并记录 m_lastManualRotationTime（用于自动对齐的冷却判断）。
    /// </remarks>
    private bool ManualRotation()
    {
        Vector2 input = new Vector2(-Input.GetAxis("Camera Vertical"), Input.GetAxis("Camera Horizontal"));

        // 判断浮点数非0用 Epsilon
        if (input.x < -float.Epsilon || input.x > float.Epsilon || input.y < -float.Epsilon || input.y > float.Epsilon)
        {
            m_orbitAngles += input * m_rotationSpeed * Time.deltaTime;
            m_lastManualRotationTime = Time.unscaledTime;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 对 m_orbitAngles 做必要的约束（例如竖直角度范围限制），防止相机翻转或超出期望视角区间。
    /// </summary>
    /// <remarks>
    /// - 对 X 分量（垂直视角）使用 Mathf.Clamp 限制在 [m_minVerticalAngle, m_maxVerticalAngle] 范围内；
    /// - Y 分量（水平）归一化。错误的：Unity 的 Quaternion.Euler 会正确处理超过 360 的角度；
    /// </remarks>
    private void ConstrainAngles()
    {
        m_orbitAngles.x = Mathf.Clamp(m_orbitAngles.x, m_minVerticalAngle, m_maxVerticalAngle);

        //rotation.y 的约束
        if (m_orbitAngles.y < 0f)
            m_orbitAngles.y += 360f;
        else if (m_orbitAngles.y > 360f)
            m_orbitAngles.y -= 360f;
    }

    /// <summary>
    /// 在没有手动旋转一段时间后，根据目标的运动方向自动平滑地对齐相机朝向（朝向目标移动方向）。
    /// </summary>
    /// <returns>如果执行了自动旋转并修改了 m_orbitAngles，则返回 true；否则返回 false。</returns>
    /// <remarks>
    /// - 使用 Time.unscaledTime 与 m_lastManualRotationTime 判定是否处于“自动对齐冷却期”内（m_alignDelay）；
    /// - 通过比较当前帧焦点与上一帧焦点（m_focusPoint 与 m_previousFocusPoint）的 XZ 平面位移，计算运动方向；
    /// - 将运动方向转换成航向角（headingAngle），并使用 Mathf.MoveTowardsAngle 将 m_orbitAngles.y 平滑移动到目标角度；
    /// - 若与目标角度偏差在 m_alignSmoothRange 内，则按比例降低旋转速度实现更平滑的收敛。
    /// </remarks>
    private bool AutoRotation()
    {
        // 暂停后相机是否会自动对齐，使用 unscaledTime（不受 Time.timeScale 影响）
        if (Time.unscaledTime - m_lastManualRotationTime < m_alignDelay)
            return false;

        Vector2 movement = new Vector2(m_focusPoint.x - m_previousFocusPoint.x, m_focusPoint.z - m_previousFocusPoint.z);
        float movementDeltaSqr = movement.sqrMagnitude;
        if (movementDeltaSqr < 0.00001f)
        {
            return false;
        }
        float headingAngle = GetAngle(movement.normalized);

        // 旋转平滑
        float deltaAbs = Mathf.Abs(Mathf.DeltaAngle(m_orbitAngles.y, headingAngle));
        float rotationChange = m_rotationSpeed * Time.unscaledDeltaTime;//rotationChange 本帧允许旋转的最大角度
        if (deltaAbs < m_alignSmoothRange)//越接近目标角度（headingAngle），deltaAbs越小，旋转越慢
            rotationChange *= deltaAbs / m_alignSmoothRange;
        m_orbitAngles.y = Mathf.MoveTowardsAngle(m_orbitAngles.y, headingAngle, rotationChange);
        return true;
    }

    /// <summary>
    /// 将二维方向向量（x, y）转换为以度为单位的角度（0-360），用于航向计算。
    /// </summary>
    /// <param name="direction">归一化后的方向向量，通常为 (x, y) = (右向分量, 前向分量)。</param>
    /// <returns>对应的角度（以度为单位，范围 [0, 360)），0 度表示 +Y（forward），顺时针递增。</returns>
    /// <remarks>
    /// - 使用 Mathf.Acos(direction.y) 得到与 +Y 轴夹角（弧度），再转为角度；
    /// - 由于 acos 返回的是 0..180 范围，需要根据 direction.x 的正负来决定角度在 0..360 中的映射（direction.x &lt; 0 则位于 180..360 区间）。
    /// - 该方法不依赖对象状态，因此声明为 static，便于在无需实例的场景下复用。
    /// </remarks>
    private static float GetAngle(Vector2 direction)
    {
        float angle = Mathf.Acos(direction.y) * Mathf.Rad2Deg;
        return direction.x < 0 ? 360f - angle : angle;
    }
}
