using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Npc : MonoBehaviour
{
    //-------------------------------------------------------------
    //--变量字段
    //-------------------------------------------------------------

    #region Serialization
    [SerializeField, Tooltip("布娃娃身体各个部位")]
    private GameObject
    //
    Root, Body, Head,
    UpperRightArm, LowerRightArm,
    UpperLeftArm, LowerLeftArm,
    UpperRightLeg, LowerRightLeg,
    UpperLeftLeg, LowerLeftLeg,
    RightFoot, LeftFoot;

    [SerializeField, Tooltip("布娃娃身体的质心")] private Transform COMP;

    [Header("Npc的脚和受击的脚本依赖，方便配置")]
    [SerializeField, Tooltip("右脚脚本依赖")] private NpcFeet FeetRight;
    [SerializeField, Tooltip("左脚脚本依赖")] private NpcFeet FeetLeft;
    [SerializeField, Tooltip("上半身脚本依赖")] private NpcHit HitDetect;

    [Header("移动属性")]
    [SerializeField, Range(0.1f, 0.5f), Tooltip("越小 → Npc步伐越快（默认0.2）")] private float StepDuration = 0.2f;
    [SerializeField, Range(0.5f, 4f), Tooltip("越大 → Npc抬腿越明显（默认1.7）")] private float StepHeight = 1.7f;
    [SerializeField, Tooltip("太小 → 脚容易悬空；太大 → 可能把Npc压得过度")] private float FeetMountForce = 25f;
    [SerializeField, Range(0f, 1f), Tooltip("越大 → Npc移动响应速度更快")] private float moveResponse = 0.5f; 
    [SerializeField, Range(0f, 50f), Tooltip("越大 → Npc转向更快")] private float turnSpeed = 6f;
    [SerializeField, Tooltip("Npc移动速度")] private float moveSpeed = 10f;
    private float defaultSpeed = 0f;
    [HideInInspector] public float balanceHeight = 2.5f;

    [Header("Npc水坑摔倒和被车撞飞")]
    [Tooltip("水坑摔倒后能否恢复")] public bool afterPoolFallCanRecover = true;
    [Tooltip("水坑摔倒后恢复时间")] public float poolFallRecoveryTime = 2f;
    [Tooltip("被车击飞后恢复时间")] public float hitFlyRecoveryTime = 2f;
    [SerializeField, Range(20f, 80f), Tooltip("被车击飞的力")] private float hitFlyForce = 30f;

    [Header("Npc被霸体影响")]
    [Tooltip("受玩家霸体影响的CD")] public float hitCD = 3f;
    [Tooltip("被霸体摔倒后恢复时间")] public float hitFallRecoveryTime = 2f;
    [SerializeField, Range(0f, 80f), Tooltip("霸体击飞的力")] private float hitBTFlyForce = 30f;

    private bool canResetPose = true;

    [Header("AI测试和能否动")]
    [Tooltip("启用会寻路功能，不用只会直走")] public bool useAI = true;
    [Tooltip("直接测试场景AI的时候需要开启，如果从Menu开始自动将其设置为false，然后再开启；角色移动之后再关闭ableMove，角色已经进入移动的状态")] 
    public bool ableMove = false;
    private NavMeshAgent agent;
    public GameObject navTarget;


    [Header("寻路进阶：是否到达目标一定范围内就停止")]
    public bool useStopDist = true;    //是否开启在目标物体一定距离范围下就会停下
    //private bool targetReached = false;    //标记是否到达物体的一定范围内
    public float StopDist = 0.2f;     //这个停止范围，不考虑竖轴分量
    private Vector3 curDir;

    #endregion



    #region HideInInspector

    private float
    timer, Step_R_timer, Step_L_timer;

    private bool
    WalkForward, WalkBackward,
    StepRight, StepLeft, Alert_Leg_Right,
    Alert_Leg_Left,  GettingUp,
    ResetPose, isRagdoll, isKeyDown, moveAxisUsed,
    jumpAxisUsed, reachLeftAxisUsed, reachRightAxisUsed;

    [HideInInspector]
    public bool
    jumping, isJumping, inAir, isFallDown, isHitFly, isRunning;

    //平衡
    [HideInInspector] public bool balanced = true;
    private bool autoGetUpWhenPossible = true;
    private bool useStepPrediction = true;
    private float balanceStrength = 5000f;//平衡时root关节的刚度（驱动力）
    private float coreStrength = 1500f;//伸手时body关节的刚度
    private float limbStrength = 500f;//不伸手平衡时四肢关节的刚度
    private float armReachStiffness = 2000f;//伸手

    private Vector3 CenterOfMassPoint;//质心位置    

    //布娃娃身体各个部位、关节、刚体的数组，缓存
    private GameObject[] APR_Parts;
    private ConfigurableJoint[] APR_Joints;
    private Rigidbody[] APR_Rbs;
    private Transform rootT;//CharacterSetup中缓存的root的Transform的引用
    private Vector3 desiredDirection;//玩家的移动期望方向 desiredDirection 

    //关节驱动
    private JointDrive
    //
    BalanceOn, PoseOn, CoreStiffness, ReachStiffness, DriveOff;

    //初始状态下各个部件的旋转值
    private Quaternion
    //
    HeadTarget, BodyTarget,
    UpperRightArmTarget, LowerRightArmTarget,
    UpperLeftArmTarget, LowerLeftArmTarget,
    UpperRightLegTarget, LowerRightLegTarget,
    UpperLeftLegTarget, LowerLeftLegTarget;
    #endregion






    //订阅LevelManager关卡结束时的事件，让Npc停下来
    //Npc会 移动 旋转 初始化- 地面检测 步态预测- 重置走路循环- Npc脚着地 Walking- 启用布娃娃- 关闭布娃娃- 重置姿势- 质心- 



    #region Npc的生命周期

    //订阅LevelManager开始关卡这个事件，用于启用Npc的行动
    private void OnEnable()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelStarted += EnableMove;
        }
    }

    private void OnDisable()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelStarted -= EnableMove;
        }
    }

    private void Awake()
    {
        CharacterSetup();

        //开启时有寻路功能，不开启就只往前走
        if (useAI)
        {
            agent = GetComponent<NavMeshAgent>();

            //禁止agent联动角色，最终只使用寻路功能
            agent.updatePosition = false;
            agent.updateRotation = false;

            //即便开启上面两个，agent还是会隐性按照自己的参数自己动，也得关闭agent的移动
            agent.isStopped = true;
        }
    }

    private void Update()
    {
        if (useAI)
        {
            FindNavDir();
        }

        if (!inAir && ableMove)
        {
            NpcMovement();
        }

        // 维持步态预测与重心计算
        if (balanced && useStepPrediction)
        {
            StepPrediction();
            CenterOfMass();
        }
        else
            ResetWalkCycle();

        GroundCheck();
        CenterOfMass();
    }

    private void FixedUpdate()
    {
        Walking();

        if (useAI)
        {
            // 根据寻得最短路方向并转动角色
            NpcRotation();
        }

        HitFly();

        // 直接重置姿势
        ResetNpcPose();
    }

    #endregion

    #region 布娃娃底层设置
    /// <summary>
    /// 角色与关节驱动和部件的初始化设置（JointDrive、APR_Parts 数组、原始 targetRotation 缓存）。
    /// </summary>
    /// <remarks>
    /// - 创建并配置若干 JointDrive（BalanceOn、PoseOn、CoreStiffness、ReachStiffness、DriveOff），
    ///   这些驱动用于在不同状态下（平衡 / 非平衡 / 伸手）切换关节刚度；
    /// - 将所有 active ragdoll 的部件放入 APR_Parts 数组（索引有注释），便于通过数组索引统一访问；
    /// - 缓存各关节最初的 targetRotation，用于 Reset / 返回原始姿势时使用；
    /// - 注意：获取组件（GetComponent）在 Awake/Start 阶段执行是合理的，但在 Update 中频繁调用会有性能影响。
    /// </remarks>
    private void CharacterSetup()
    {
        BalanceOn = new JointDrive();
        BalanceOn.positionSpring = balanceStrength;
        BalanceOn.positionDamper = 0;
        BalanceOn.maximumForce = Mathf.Infinity;

        PoseOn = new JointDrive();
        PoseOn.positionSpring = limbStrength;
        PoseOn.positionDamper = 0;
        PoseOn.maximumForce = Mathf.Infinity;

        CoreStiffness = new JointDrive();
        CoreStiffness.positionSpring = coreStrength;
        CoreStiffness.positionDamper = 0;
        CoreStiffness.maximumForce = Mathf.Infinity;

        ReachStiffness = new JointDrive();
        ReachStiffness.positionSpring = armReachStiffness;
        ReachStiffness.positionDamper = 0;
        ReachStiffness.maximumForce = Mathf.Infinity;

        DriveOff = new JointDrive();
        DriveOff.positionSpring = 25;
        DriveOff.positionDamper = 0;
        DriveOff.maximumForce = Mathf.Infinity;

        // 将 Active Ragdoll 的各个 GameObject 部件组织成数组，便于统一处理
        APR_Parts = new GameObject[]
        {
        // array index numbers
        // 0
        Root,
        // 1
        Body,
        // 2
        Head,
        // 3
        UpperRightArm,
        // 4
        LowerRightArm,
        // 5
        UpperLeftArm,
        // 6
        LowerLeftArm,
        // 7
        UpperRightLeg,
        // 8
        LowerRightLeg,
        // 9
        UpperLeftLeg,
        // 10
        LowerLeftLeg,
        // 11
        RightFoot,
        // 12
        LeftFoot
        };

        APR_Joints = new ConfigurableJoint[APR_Parts.Length];
        APR_Rbs = new Rigidbody[APR_Parts.Length];

        for (int i = 0; i < APR_Parts.Length; i++)
        {
            if (APR_Parts[i] != null)
            {
                APR_Joints[i] = APR_Parts[i].GetComponent<ConfigurableJoint>();
                APR_Rbs[i] = APR_Parts[i].GetComponent<Rigidbody>();
            }
            else
            {
                APR_Joints[i] = null;
                APR_Rbs[i] = null;
            }
        }

        // 缓存 root 的 Transform 
        rootT = APR_Parts[0].transform;

        // 缓存各部件的原始 ConfigurableJoint.targetRotation，用于后续姿势还原
        BodyTarget = APR_Joints[1].targetRotation;
        HeadTarget = APR_Joints[2].targetRotation;
        UpperRightArmTarget = APR_Joints[3].targetRotation;
        LowerRightArmTarget = APR_Joints[4].targetRotation;
        UpperLeftArmTarget = APR_Joints[5].targetRotation;
        LowerLeftArmTarget = APR_Joints[6].targetRotation;
        UpperRightLegTarget = APR_Joints[7].targetRotation;
        LowerRightLegTarget = APR_Joints[8].targetRotation;
        UpperLeftLegTarget = APR_Joints[9].targetRotation;
        LowerLeftLegTarget = APR_Joints[10].targetRotation;

        //获取当前移动的方向：root的z轴正方向
        curDir = APR_Parts[0].transform.forward;

        //获取默认的初始速度
        defaultSpeed = moveSpeed; 
    }

    /// <summary>
    /// 步态预测：在非平衡或平衡状态下判断前后移动方向，并维护步态相关的定时器与警报标志（用于抬脚、转移重心等）。
    /// </summary>
    /// <remarks>
    /// - 方法会在不移动时重置步态相关状态；
    /// - 通过比较质心 COMP 与双脚（RightFoot、LeftFoot）的 z 轴位置判断向前或向后倾向，
    ///   这是一种简单的基于位置的步态启发式判定；
    /// - 若无输入（isKeyDown 为 false），会把对应的 WalkForward/WalkBackward 恢复为 false；
    /// - 该方法依赖于 COMP（质心辅助 Transform）位置和 APR_Parts 的脚部索引正确设置。
    /// </remarks>
    private void StepPrediction()
    {
        // Reset variables when balanced and not moving（若不移动则清理步态状态）
        if (!WalkForward && !WalkBackward)
        {
            StepRight = false;
            StepLeft = false;
            Step_R_timer = 0;
            Step_L_timer = 0;
            Alert_Leg_Right = false;
            Alert_Leg_Left = false;
        }

        // Check direction to walk when off balance（判断前后移动趋势）
        // Backwards：如果质心在两脚之`后`，则视为向后倾向
        if (COMP.position.z < APR_Parts[11].transform.position.z && COMP.position.z < APR_Parts[12].transform.position.z)
        {
            WalkBackward = true;
        }
        else
        {
            if (!isKeyDown)
            {
                WalkBackward = false;
            }
        }

        // Forward：如果质心在两脚之`前`，则视为向前倾向
        if (COMP.position.z > APR_Parts[11].transform.position.z && COMP.position.z > APR_Parts[12].transform.position.z)
        {
            WalkForward = true;
        }
        else
        {
            if (!isKeyDown)
            {
                WalkForward = false;
            }
        }
    }

    /// <summary>
    /// 重置步态循环相关变量（当没有移动输入时保持静止状态的一致性）
    /// </summary>
    /// <remarks>
    /// - 与 StepPrediction 中的重置逻辑类似，用于在禁用步态预测或其它条件下保证步态状态复位；
    /// - 将步态触发器、定时器与告警标志清零/重置。
    /// </remarks>
    private void ResetWalkCycle()
    {
        if (!WalkForward && !WalkBackward)
        {
            StepRight = false;
            StepLeft = false;
            Step_R_timer = 0;
            Step_L_timer = 0;
            Alert_Leg_Right = false;
            Alert_Leg_Left = false;
        }
    }

    /// <summary>
    /// 步行动作逻辑：根据左右脚位置与步态标志触发抬脚/落脚，并通过修改关节 targetRotation 与对脚施力来模拟行走。
    /// </summary>
    /// <remarks>
    /// - 本实现使用事先缓存的 APR_Joints（ConfigurableJoint[]）和 APR_Rbs（Rigidbody[]），避免每帧 GetComponent 调用，提升性能；
    /// - 计时器使用 Time.fixedDeltaTime（步态相关通常与物理步长同步）；脚部施力也使用 FixedDeltaTime 以保证物理稳定性；
    /// - 关节 targetRotation 的修改仍沿用原脚本的分量修改方式（直接 new Quaternion(...)），如果需要更物理/严格的姿态控制，建议改用 Quaternion.Euler 或基于初始目标旋转的增量旋转。
    /// </remarks>
    private void Walking()
    {
        // 仅在不在空中时处理步态
        if (!inAir)
        {
            // WalkForward 时判断哪只脚先抬（基于脚的 z 轴位置比较）
            if (WalkForward)
            {
                // right leg 条件：右脚在左脚后方且尚未触发左脚步与右脚警报时
                if (APR_Rbs[11].position.z < APR_Rbs[12].position.z && !StepLeft && !Alert_Leg_Right)
                {
                    StepRight = true;
                    Alert_Leg_Right = true;
                    Alert_Leg_Left = true;
                }

                // left leg 条件：右脚在左脚前方且尚未触发右脚步与左脚警报时
                if (APR_Rbs[11].position.z > APR_Rbs[12].position.z && !StepRight && !Alert_Leg_Left)
                {
                    StepLeft = true;
                    Alert_Leg_Left = true;
                    Alert_Leg_Right = true;
                }
            }

            // WalkBackward 时判断（方向相反）
            if (WalkBackward)
            {
                if (APR_Rbs[11].position.z > APR_Rbs[12].position.z && !StepLeft && !Alert_Leg_Right)
                {
                    StepRight = true;
                    Alert_Leg_Right = true;
                    Alert_Leg_Left = true;
                }

                if (APR_Rbs[11].position.z < APR_Rbs[12].position.z && !StepRight && !Alert_Leg_Left)
                {
                    StepLeft = true;
                    Alert_Leg_Left = true;
                    Alert_Leg_Right = true;
                }
            }

            // StepRight 流程
            if (StepRight)
            {
                Step_R_timer += Time.fixedDeltaTime;

                // 右脚向下施力以保持接地感（使用缓存的刚体）
                APR_Rbs[11].AddForce(-Vector3.up * FeetMountForce * Time.fixedDeltaTime, ForceMode.Impulse);

                // 行走姿态模拟：修改对应关节的 targetRotation（基于缓存的 APR_Joints）
                if (WalkForward)
                {
                    // 读取当前 targetRotation，按原有比例调整 x 分量
                    Quaternion t7 = APR_Joints[7].targetRotation;
                    Quaternion t8 = APR_Joints[8].targetRotation;
                    Quaternion t9 = APR_Joints[9].targetRotation;

                    APR_Joints[7].targetRotation = new Quaternion(t7.x + 0.09f * StepHeight, t7.y, t7.z, t7.w);
                    APR_Joints[8].targetRotation = new Quaternion(t8.x - 0.09f * StepHeight * 2, t8.y, t8.z, t8.w);
                    APR_Joints[9].targetRotation = new Quaternion(t9.x - 0.12f * StepHeight / 2, t9.y, t9.z, t9.w);
                }

                if (WalkBackward)
                {
                    Quaternion t7 = APR_Joints[7].targetRotation;
                    Quaternion t8 = APR_Joints[8].targetRotation;
                    Quaternion t9 = APR_Joints[9].targetRotation;

                    APR_Joints[7].targetRotation = new Quaternion(t7.x - 0.00f * StepHeight, t7.y, t7.z, t7.w);
                    APR_Joints[8].targetRotation = new Quaternion(t8.x - 0.07f * StepHeight * 2, t8.y, t8.z, t8.w);
                    APR_Joints[9].targetRotation = new Quaternion(t9.x + 0.02f * StepHeight / 2, t9.y, t9.z, t9.w);
                }

                // 步时长结束处理
                if (Step_R_timer > StepDuration)
                {
                    Step_R_timer = 0f;
                    StepRight = false;

                    if (WalkForward || WalkBackward)
                    {
                        StepLeft = true;
                    }
                }
            }
            else
            {
                // 右腿回到 idle（插值到缓存的目标姿势 UpperRightLegTarget / LowerRightLegTarget）
                APR_Joints[7].targetRotation = Quaternion.Lerp(APR_Joints[7].targetRotation, UpperRightLegTarget, (8f) * Time.fixedDeltaTime);
                APR_Joints[8].targetRotation = Quaternion.Lerp(APR_Joints[8].targetRotation, LowerRightLegTarget, (17f) * Time.fixedDeltaTime);

                // 双脚持续向下施力以保持触地压力（使用缓存的刚体）
                APR_Rbs[11].AddForce(-Vector3.up * FeetMountForce * Time.fixedDeltaTime, ForceMode.Impulse);
                APR_Rbs[12].AddForce(-Vector3.up * FeetMountForce * Time.fixedDeltaTime, ForceMode.Impulse);
            }

            // StepLeft 流程（对称于 StepRight）
            if (StepLeft)
            {
                Step_L_timer += Time.fixedDeltaTime;

                // 左脚向下施力
                APR_Rbs[12].AddForce(-Vector3.up * FeetMountForce * Time.fixedDeltaTime, ForceMode.Impulse);

                if (WalkForward)
                {
                    Quaternion t9 = APR_Joints[9].targetRotation;
                    Quaternion t10 = APR_Joints[10].targetRotation;
                    Quaternion t7 = APR_Joints[7].targetRotation;

                    APR_Joints[9].targetRotation = new Quaternion(t9.x + 0.09f * StepHeight, t9.y, t9.z, t9.w);
                    APR_Joints[10].targetRotation = new Quaternion(t10.x - 0.09f * StepHeight * 2, t10.y, t10.z, t10.w);
                    APR_Joints[7].targetRotation = new Quaternion(t7.x - 0.12f * StepHeight / 2, t7.y, t7.z, t7.w);
                }

                if (WalkBackward)
                {
                    Quaternion t9 = APR_Joints[9].targetRotation;
                    Quaternion t10 = APR_Joints[10].targetRotation;
                    Quaternion t7 = APR_Joints[7].targetRotation;

                    APR_Joints[9].targetRotation = new Quaternion(t9.x - 0.00f * StepHeight, t9.y, t9.z, t9.w);
                    APR_Joints[10].targetRotation = new Quaternion(t10.x - 0.07f * StepHeight * 2, t10.y, t10.z, t10.w);
                    APR_Joints[7].targetRotation = new Quaternion(t7.x + 0.02f * StepHeight / 2, t7.y, t7.z, t7.w);
                }

                // 步时长结束处理
                if (Step_L_timer > StepDuration)
                {
                    Step_L_timer = 0f;
                    StepLeft = false;

                    if (WalkForward || WalkBackward)
                    {
                        StepRight = true;
                    }
                }
            }
            else
            {
                // 左腿回到 idle（插值到缓存的目标姿势 UpperLeftLegTarget / LowerLeftLegTarget）
                APR_Joints[9].targetRotation = Quaternion.Lerp(APR_Joints[9].targetRotation, UpperLeftLegTarget, (7f) * Time.fixedDeltaTime);
                APR_Joints[10].targetRotation = Quaternion.Lerp(APR_Joints[10].targetRotation, LowerLeftLegTarget, (18f) * Time.fixedDeltaTime);

                // 双脚持续向下施力
                APR_Rbs[11].AddForce(-Vector3.up * FeetMountForce * Time.fixedDeltaTime, ForceMode.Impulse);
                APR_Rbs[12].AddForce(-Vector3.up * FeetMountForce * Time.fixedDeltaTime, ForceMode.Impulse);
            }
        }
    }

    /// <summary>
    /// 重置Npc姿势：每个物理帧调用。
    /// </summary>
    /// <remarks>
    /// - 前提条件：ResetPose 标志为 true 且不处于跳跃状态；
    /// - 恢复的关节包括：身体、双臂（上/下臂），并将其 targetRotation 重置为预存的目标四元数；
    /// - ResetPose 重置为 false，避免重复触发。
    /// </remarks>
    private void ResetNpcPose()
    {
        if (ResetPose && canResetPose)
        {
            APR_Joints[1].targetRotation = BodyTarget;
            APR_Joints[3].targetRotation = UpperRightArmTarget;
            APR_Joints[4].targetRotation = LowerRightArmTarget;
            APR_Joints[5].targetRotation = UpperLeftArmTarget;
            APR_Joints[6].targetRotation = LowerLeftArmTarget;

            ResetPose = false;
        }
    }

    /// <summary>
    /// 计算整个角色的质心（Center of Mass），并把结果赋值到 COMP（用于可视化或后续平衡计算）。
    /// </summary>
    /// <remarks>
    /// - 采用标准的质量加权平均公式：sum(m_i * r_i) / sum(m_i)；
    /// - 代码通过对 APR_Parts 中每个部件的刚体质量与位置进行线性组合得到 CenterOfMassPoint；
    /// - 计算完毕后把 COMP.position 设为该质心位置，便于后续步态预测与平衡逻辑使用；
    /// - 注意：频繁调用 GetComponent<Rigidbody>() 每帧可能有性能开销，建议缓存刚体引用以优化性能。
    /// </remarks>
    private void CenterOfMass()
    {
        CenterOfMassPoint =
            (APR_Rbs[0].mass * APR_Parts[0].transform.position +
             APR_Rbs[1].mass * APR_Parts[1].transform.position +
             APR_Rbs[2].mass * APR_Parts[2].transform.position +
             APR_Rbs[3].mass * APR_Parts[3].transform.position +
             APR_Rbs[4].mass * APR_Parts[4].transform.position +
             APR_Rbs[5].mass * APR_Parts[5].transform.position +
             APR_Rbs[6].mass * APR_Parts[6].transform.position +
             APR_Rbs[7].mass * APR_Parts[7].transform.position +
             APR_Rbs[8].mass * APR_Parts[8].transform.position +
             APR_Rbs[9].mass * APR_Parts[9].transform.position +
             APR_Rbs[10].mass * APR_Parts[10].transform.position +
             APR_Rbs[11].mass * APR_Parts[11].transform.position +
             APR_Rbs[12].mass * APR_Parts[12].transform.position)
            /
            (APR_Rbs[0].mass + APR_Rbs[1].mass +
             APR_Rbs[2].mass + APR_Rbs[3].mass +
             APR_Rbs[4].mass + APR_Rbs[5].mass +
             APR_Rbs[6].mass + APR_Rbs[7].mass +
             APR_Rbs[8].mass + APR_Rbs[9].mass +
             APR_Rbs[10].mass + APR_Rbs[11].mass +
             APR_Rbs[12].mass);

        COMP.position = CenterOfMassPoint; // 把计算结果写回 COMP（用于调试/步态逻辑/平衡判定）
    }

    /// <summary>
    /// 激活 ragdoll（物理驱动）模式：把大部分关节驱动切换为 DriveOff（接近失能），以便骨骼由物理自然倒下/摆动。
    /// </summary>
    /// <remarks>
    /// - 将 isRagdoll 设为 true，balanced 设为 false；
    /// - 针对不同部位（Root、Head、Arms、Legs）把对应的 ConfigurableJoint 的 angular drives 切换为 DriveOff；
    /// - 对于当前正在伸手的手臂（reachRightAxisUsed / reachLeftAxisUsed），保留其原来驱动状态，避免伸手时被强制关闭；
    /// - 该函数应尽量在地面检测判定、受击或强制倒地时调用。
    /// </remarks>
    public void ActivateRagdoll()
    {
        isRagdoll = true;
        balanced = false;

        // Root
        APR_Joints[0].angularXDrive = DriveOff;
        APR_Joints[0].angularYZDrive = DriveOff;
        // head
        APR_Joints[2].angularXDrive = DriveOff;
        APR_Joints[2].angularYZDrive = DriveOff;
        // arms
        if (!reachRightAxisUsed)
        {
            APR_Joints[3].angularXDrive = DriveOff;
            APR_Joints[3].angularYZDrive = DriveOff;
            APR_Joints[4].angularXDrive = DriveOff;
            APR_Joints[4].angularYZDrive = DriveOff;
        }

        if (!reachLeftAxisUsed)
        {
            APR_Joints[5].angularXDrive = DriveOff;
            APR_Joints[5].angularYZDrive = DriveOff;
            APR_Joints[6].angularXDrive = DriveOff;
            APR_Joints[6].angularYZDrive = DriveOff;
        }
        // legs
        APR_Joints[7].angularXDrive = DriveOff;
        APR_Joints[7].angularYZDrive = DriveOff;
        APR_Joints[8].angularXDrive = DriveOff;
        APR_Joints[8].angularYZDrive = DriveOff;
        APR_Joints[9].angularXDrive = DriveOff;
        APR_Joints[9].angularYZDrive = DriveOff;
        APR_Joints[10].angularXDrive = DriveOff;
        APR_Joints[10].angularYZDrive = DriveOff;
        APR_Joints[11].angularXDrive = DriveOff;
        APR_Joints[11].angularYZDrive = DriveOff;
        APR_Joints[12].angularXDrive = DriveOff;
        APR_Joints[12].angularYZDrive = DriveOff;
    }

    /// <summary>
    /// 取消 ragdoll（恢复姿势控制）模式：把关节驱动恢复为 BalanceOn / PoseOn 等以重获得姿态约束与站立能力。
    /// </summary>
    /// <remarks>
    /// - 将 isRagdoll 设为 false，balanced 设为 true；
    /// - 恢复 Root 的 BalanceOn（用于维持整体平衡），头/躯干/四肢恢复为 PoseOn（或保留伸手时的状态）；
    /// - 设置 ResetPose 标志，提示上层逻辑进行目标姿势的插值还原（此处只做驱动恢复，不直接立刻重设 targetRotation）。
    /// </remarks>
    public void DeactivateRagdoll()
    {
        isRagdoll = false;
        balanced = true;

        // Root
        APR_Joints[0].angularXDrive = BalanceOn;
        APR_Joints[0].angularYZDrive = BalanceOn;
        // head
        APR_Joints[2].angularXDrive = PoseOn;
        APR_Joints[2].angularYZDrive = PoseOn;
        // arms
        if (!reachRightAxisUsed)
        {
            APR_Joints[3].angularXDrive = PoseOn;
            APR_Joints[3].angularYZDrive = PoseOn;
            APR_Joints[4].angularXDrive = PoseOn;
            APR_Joints[4].angularYZDrive = PoseOn;
        }

        if (!reachLeftAxisUsed)
        {
            APR_Joints[5].angularXDrive = PoseOn;
            APR_Joints[5].angularYZDrive = PoseOn;
            APR_Joints[6].angularXDrive = PoseOn;
            APR_Joints[6].angularYZDrive = PoseOn;
        }
        // legs
        APR_Joints[7].angularXDrive = PoseOn;
        APR_Joints[7].angularYZDrive = PoseOn;
        APR_Joints[8].angularXDrive = PoseOn;
        APR_Joints[8].angularYZDrive = PoseOn;
        APR_Joints[9].angularXDrive = PoseOn;
        APR_Joints[9].angularYZDrive = PoseOn;
        APR_Joints[10].angularXDrive = PoseOn;
        APR_Joints[10].angularYZDrive = PoseOn;
        APR_Joints[11].angularXDrive = PoseOn;
        APR_Joints[11].angularYZDrive = PoseOn;
        APR_Joints[12].angularXDrive = PoseOn;
        APR_Joints[12].angularYZDrive = PoseOn;

        ResetPose = true; // 标记需要重置姿势（例如把 targetRotation 恢复为缓存值）
    }
    #endregion

    #region AI寻路和移动

    /// <summary>
    /// 开启update当中调用角色移动方法的标志
    /// </summary>
    private void EnableMove()
    {
        ableMove = true;
    }

    private void FindNavDir()
    {
        //Debug.Log(pathTarget.transform.position);
        //todo: 若每帧调用，对性能消耗大，需要进行优化
        agent.SetDestination(navTarget.transform.position);
        if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathComplete)
        {
            if (agent.path.corners.Length > 1)
            {
                curDir = (agent.path.corners[1] - agent.path.corners[0]).normalized;
            }
            else
            {
                // 如果路径只有一个点，直接指向目标，起始时目标就不可达会存在这种情况
                // 此时的唯一点其实就是agent目前所在点
                curDir = APR_Parts[0].transform.forward;
            }
            //其实还有一些进阶的不可达处理，后面再说
        }
    }

    private void NpcMovement()
    {
        if (useAI)
        {
            agent.nextPosition = rootT.position;
        }

        if (useStopDist)
        {
            if (!agent.pathPending && agent.remainingDistance < StopDist)
            {
                moveSpeed = 0;
            }
            else
            {
                moveSpeed = defaultSpeed;
            }
        }

        // 设置“行走意图”，驱动抬步
        // 这样比较暴力，怎么腿就动起来了
        WalkBackward = false;

        if (moveSpeed != 0)
        {
            WalkForward = true;
            isKeyDown = true;
            moveAxisUsed = true;
        }
        else
        {
            WalkForward = false;
            isKeyDown = false;
            moveAxisUsed = false;
        }

        //设置速度
        //未完全理解，智能说现在的方向已经朝向了寻路的目的地
        Vector3 targetVel = APR_Rbs[0].transform.forward * moveSpeed;
        targetVel.y = APR_Rbs[0].velocity.y;
        APR_Rbs[0].velocity = Vector3.Lerp(APR_Rbs[0].velocity, targetVel, moveResponse);
    }

    private void NpcRotation()
    {
        var lookPos = Quaternion.Inverse(transform.rotation) * curDir;   //修正
        lookPos.y = 0;
        var rotation = Quaternion.LookRotation(lookPos);
        APR_Joints[0].targetRotation = Quaternion.Slerp(APR_Joints[0].targetRotation, Quaternion.Inverse(rotation), Time.deltaTime * turnSpeed);
    }

    #endregion

    #region 物理相关
    /// <summary>
    /// 地面检测：通过从 Root 发射向下射线判断是否接触 Ground 图层，从而决定是否切换平衡 / ragdoll 状态。
    /// </summary>
    /// <remarks>
    /// - 使用 Physics.Raycast 检测到 ground 时并且角色速度较小，可进入平衡状态（balanced = true）；
    /// - 若射线没有检测到地面，则认为处于空中或倒地（balanced = false）；
    /// - 根据 balanced 与 isRagdoll 状态决定是否调用 ActivateRagdoll/DeactivateRagdoll 切换驱动配置；
    /// - 需要注意对 LayerMask 的使用，这里使用 LayerMask.NameToLayer("Ground") 构造位掩码。
    /// </remarks>
    private void GroundCheck()
    {
        Ray ray = new Ray(APR_Parts[0].transform.position, -APR_Parts[0].transform.up);
        RaycastHit hit;

        // 当检测到地面且满足若干前置条件时尝试进入平衡状态
        // balanceHeight为射线长度
        if (Physics.Raycast(ray, out hit, balanceHeight, 1 << LayerMask.NameToLayer("Ground")) && !inAir && !isJumping && !reachRightAxisUsed && !reachLeftAxisUsed)
        {
            // 仅在根刚体速度较小时，才自动站起（防止跑动中误判）
            if (!balanced && APR_Rbs[0].velocity.magnitude < 1f)
            {
                if (autoGetUpWhenPossible)
                {
                    balanced = true;
                }
            }
        }
        // Fall over when ground is not detected（射线未命中地面时认为不平衡）
        else if (!Physics.Raycast(ray, out hit, balanceHeight, 1 << LayerMask.NameToLayer("Ground")))
        {
            if (balanced)
            {
                balanced = false;
            }
        }

        // Balance on/off：根据 balanced 与 isRagdoll 切换 ragdoll 状态（保证二者一致）
        if (balanced && isRagdoll)
        {
            DeactivateRagdoll();
        }
        else if (!balanced && !isRagdoll)
        {
            ActivateRagdoll();
        }
    }

    /// <summary>
    /// 落地检测：当角色从空中回到地面时恢复 ResetPose。
    /// </summary>
    /// <remarks>
    /// - 前提条件：当前 inAir 为 true，且没有处于 jumping 或 isJumping 状态；
    /// - 执行逻辑：将 inAir 设为 false，并设置 ResetPose = true（下一个 FixedUpdate 会自动触发 ResetPlayerPose()）；
    /// - 调用时机：通常由脚部触地触发（FeetConnection 调用）。
    /// </remarks>
    public void CharacterLanded()
    {
        if (inAir && !isJumping && !jumping)
        {
            inAir = false;
            ResetPose = true;
        }
    }
    #endregion

    #region Npc摔倒和击飞

    public void NpcFellDown()
    {
        ResetPose = true;
    }

    /// <summary>
    /// NpcHit 中触发 HitFly，实现Npc被击飞的效果（用途大，有待开发）
    /// </summary>
    /// <remarks>
    /// - 总体逻辑与跳跃一致；
    /// - NpcHit改变标志，FixedUpdate中该方法得以被调用；
    /// - 开启 jumping 的标志，然后使用跳跃的逻辑；
    /// - 跳跃高度由 hitFlyForce 控制；
    /// - 其他脚本可以引用Npc，然后通过设置 isHitFly 来触发击飞效果。
    /// </remarks>
    public void HitFly()
    {
        if (isHitFly)
        {
            jumping = true;

            if (jumping)
            {
                isJumping = true;

                Vector3 v3 = APR_Rbs[0].transform.up * hitFlyForce;
                v3.x = APR_Rbs[0].velocity.x;
                v3.z = APR_Rbs[0].velocity.z;
                APR_Rbs[0].velocity = v3;
            }

            if (isJumping)
            {
                timer = timer + Time.fixedDeltaTime;

                if (timer > 0.2f)
                {
                    timer = 0.0f;
                    jumping = false;
                    isJumping = false;
                    inAir = true;
                }
            }
        }
    }

    public void HitBTFly()
    {
        //斜45度后上方
        Vector3 v3 = (APR_Rbs[0].transform.up + APR_Rbs[0].transform.forward * -1).normalized
            * hitBTFlyForce;
        v3.x = APR_Rbs[0].velocity.x;
        v3.z = APR_Rbs[0].velocity.z;
        APR_Rbs[0].velocity = v3;
        /*if (isHitFly)
        {
            jumping = true;

            if (jumping)
            {
                isJumping = true;

                Vector3 v3 = APR_Rbs[0].transform.up * hitFlyForce;
                v3.x = APR_Rbs[0].velocity.x;
                v3.z = APR_Rbs[0].velocity.z;
                APR_Rbs[0].velocity = v3;
            }

            if (isJumping)
            {
                timer = timer + Time.fixedDeltaTime;

                if (timer > 0.2f)
                {
                    timer = 0.0f;
                    jumping = false;
                    isJumping = false;
                    inAir = true;
                }
            }
        }*/
    }
    #endregion

    #region 公共API

    public void EnableMove(bool beEnable)
    {
        ableMove = beEnable;
    }

    #endregion
}
