using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public enum PlayerPhase
    {
        Normal = 0,
        HeadTurningRed = 1,
        HeadOnFire = 2,
        HeadBoom = 3
    }

    public PlayerPhase currentPhase { get; private set; } = PlayerPhase.Normal;

    public event Action<PlayerPhase> OnPhaseChanged;

    private Action<PlayerPhase> onPlayerPhaseRequestedHandler;


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

    [Header("手、脚、受击的脚本依赖，方便配置")]
    [SerializeField, Tooltip("右手脚本依赖")] private PlayerHand GrabRight;
    [SerializeField, Tooltip("左手脚本依赖")] private PlayerHand GrabLeft;
    [SerializeField, Tooltip("右脚脚本依赖")] private PlayerFeet FeetRight;
    [SerializeField, Tooltip("左脚脚本依赖")] private PlayerFeet FeetLeft;
    [SerializeField, Tooltip("上半身脚本依赖")] private PlayerHit HitDetect;

    [Header("滑动条影响人物的操控感")]
    [Header("移动属性")]
    [Range(0f, 10.0f), Tooltip("越大 → 角色在空中姿态更稳定（默认2.5）")] public float balanceHeight = 2.5f;//地面检测的射线长度，越大则角色在空中姿态更稳定
    [SerializeField, Range(0.1f, 0.5f), Tooltip("越小 → 角色步伐越快（默认0.2）")] private float StepDuration = 0.2f;
    [SerializeField, Range(0.5f, 4f), Tooltip("越大 → 角色抬腿越明显（默认1.7）")] private float StepHeight = 1.7f;
    [SerializeField, Tooltip("太小 → 脚容易悬空；太大 → 可能把角色压得过度")] private float FeetMountForce = 25f;
    [SerializeField, Range(0f, 50f), Tooltip("越大 → 角色移动响应速度更快")] private float moveResponse = 50f;
    [SerializeField, Tooltip("角色移动速度")] private float moveSpeed = 10f;
    [SerializeField, Tooltip("角色奔跑速度")] private float runSpeed = 20f;
    [SerializeField, Tooltip("奔跑时相机的FOV")] private float runFOV = 80f;
    [SerializeField, Range(0f, 50f), Tooltip("越大 → 角色朝向相机方向的响应更快")] private float turnSpeed = 6f;
    [SerializeField, Tooltip("角色弹跳力")] private float jumpForce = 18f;

    [Header("Player受击")]
    [Tooltip("摔倒恢复时间")] public float hitFallRecoveryTime = 2f;
    [Tooltip("击飞恢复时间")] public float hitFlyRecoveryTime = 2f;
    [SerializeField, Range(20f, 80f), Tooltip("击飞的力")] private float hitFlyForce = 30f;
    #endregion



    #region HideInInspector

    private float
    timer, Step_R_timer, Step_L_timer,
    MouseYAxisArms, MouseXAxisArms, MouseYAxisBody;

    private bool
    WalkForward, WalkBackward,
    StepRight, StepLeft, Alert_Leg_Right,
    Alert_Leg_Left, balanced = true, GettingUp,
    ResetPose, isRagdoll, isKeyDown, moveAxisUsed,
    jumpAxisUsed, reachLeftAxisUsed, reachRightAxisUsed;

    [HideInInspector]
    public bool
    jumping, isJumping, inAir, isFallDown, isHitFly, isRunning;

    //平衡
    private bool autoGetUpWhenPossible = true;
    private bool useStepPrediction = true;
    private float balanceStrength = 5000f;//平衡时root关节的刚度（驱动力）
    private float coreStrength = 1500f;//伸手时body关节的刚度
    private float limbStrength = 500f;//不伸手平衡时四肢关节的刚度

    //伸手
    private float reachSensitivity = 25f;//人物手臂摆动幅度
    private float armReachStiffness = 2000f;

    //移动控制开关和输入轴名称
    [HideInInspector] public bool useControls = true;//其他脚本可以通过 public 访问
    private string forwardBackward = "Vertical";
    private string leftRight = "Horizontal";
    private string jump = "Jump";
    [HideInInspector] public string reachLeft = "Fire1";
    [HideInInspector] public string reachRight = "Fire2";

    private Camera cam;//主摄像机的引用
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




    //-------------------------------------------------------------
    //--方法
    //-------------------------------------------------------------


    #region Unity生命周期

    private void OnEnable()
    {
        // 准备 handler 引用并尝试自动订阅（若 LevelManager 已存在）
        onPlayerPhaseRequestedHandler = HandleRequestedPhase;
        TrySubscribeToLevelManager();
    }

    private void Awake()
    {
        CharacterSetup();
    }

    private void Start()
    {
        // 再尝试一次（有时单例是在 Start 之后创建）
        TrySubscribeToLevelManager();
    }

    private void OnDisable()
    {
        // 确保解绑（防止在场景切换 / disable 时残留）
        UnsubscribeFromLevelManager();
    }

    private void OnDestroy()
    {
        // 保险性解绑（实例销毁）
        UnsubscribeFromLevelManager();
    }

    

    /// <summary>
    /// 每帧更新逻辑（主循环）。LateUpdate 之外的常规帧更新点——可驱动移动、触手、平衡预测等。
    /// </summary>
    /// <remarks>
    /// - Update 中按功能模块划分：移动、触手/伸手、步态预测、质心计算、地面检测等；
    /// - 注意：某些功能受 useControls、balanced、useStepPrediction 等标志控制；
    /// - CenterOfMass 在多处被调用（步态预测与常规更新），以确保质心信息及时更新用于平衡判断与步态预测。
    /// </remarks>
    private void Update()
    {
        // 当允许玩家输入控制且不在空中时，执行移动（走/跑）逻辑
        if (useControls && !inAir)
        {
            ReadMovementInput();
            //PlayerMovement();
        }

        // 触手 / 伸手输入（左右手）始终响应，前提是允许控制
        if (useControls)
        {
            PlayerReach();
        }

        // 平衡且启用步态预测时，执行步态预测并更新质心
        if (balanced && useStepPrediction)
        {
            StepPrediction();
            CenterOfMass();
        }

        // 如果未使用步态预测，则重置步态循环相关变量
        if (!useStepPrediction)
        {
            ResetWalkCycle();
        }

        // 始终进行地面检测并更新质心（地面检测可能会触发 ragdoll 切换）
        GroundCheck();
        CenterOfMass();
    }

    private void FixedUpdate()
    {
        if (useControls && !inAir && APR_Rbs[0] != null)
        {
            ApplyMovementPhysics();
        }

        Walking();

        if (useControls)
        {
            PlayerRotation();
            ResetPlayerPose();

            PlayerGetUpJumping();

            //PlayerHit改变标志状态，从而将人物击飞
            HitFly();
        }
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
        cam = Camera.main;

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
    /// 重置玩家姿势：在落地时恢复到预设姿态。
    /// </summary>
    /// <remarks>
    /// - 前提条件：ResetPose 标志为 true 且不处于跳跃状态；
    /// - 恢复的关节包括：身体、双臂（上/下臂），并将其 targetRotation 重置为预存的目标四元数；
    /// - 同时清零 MouseYAxisArms（相机控制的手臂偏移量），防止残余输入影响姿态；
    /// - ResetPose 重置为 false，避免重复触发。
    /// </remarks>
    private void ResetPlayerPose()
    {
        if (ResetPose && !jumping)
        {
            APR_Joints[1].targetRotation = BodyTarget;
            APR_Joints[3].targetRotation = UpperRightArmTarget;
            APR_Joints[4].targetRotation = LowerRightArmTarget;
            APR_Joints[5].targetRotation = UpperLeftArmTarget;
            APR_Joints[6].targetRotation = LowerLeftArmTarget;

            MouseYAxisArms = 0;

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

    #region 移动伸手跳跃转向
    /// <summary>
    /// 在 Update() 中读取玩家的移动输入并计算期望方向（desiredDirection），同时维护移动状态标志（WalkForward / moveAxisUsed / isKeyDown）。
    /// </summary>
    /// <remarks>
    /// - 仅负责“读取输入与状态判断”，**不直接修改刚体（避免与物理系统冲突）**；实际速度应用由 FixedUpdate()/ApplyMovementPhysics 负责；
    /// - 使用缓存的 rootT（Transform）将局部输入转换为世界方向，避免每帧访问 APR_Parts[0].transform 带来的额外开销；
    /// - 把原来 PlayerMovement 的输入判定与状态切换部分保留，但将速度计算与刚体写入拆分出去，便于物理一致性与更容易做插值/滤波；
    /// - 注意：代码中使用 rawInput（Input.GetAxisRaw）来判断是否有按键输入，以便即时响应；而对旋转/平滑等可以使用 GetAxis（带灵敏度）视需求调整。
    /// </remarks>
    private void ReadMovementInput()
    {
        // 读取原始输入（左右、前后），使用 GetAxisRaw 以便获得更即时的按键状态
        Vector3 rawInput = new Vector3(Input.GetAxisRaw(leftRight), 0f, Input.GetAxisRaw(forwardBackward));

        //正常状态变红状态按Shift有反应，着火和爆炸锁定移动相关属性
        switch (currentPhase)
        {
            case PlayerPhase.HeadOnFire:
                isRunning = true;
                moveSpeed = runSpeed;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, runFOV, Time.deltaTime * 5f);
                break;
            case PlayerPhase.HeadBoom:
                isRunning = false;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60f, Time.deltaTime * 5f);
                break;
            default:
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    isRunning = true;
                    moveSpeed = runSpeed;
                    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, runFOV, Time.deltaTime * 5f);
                }
                else
                {
                    isRunning = false;
                    moveSpeed = 10f;
                    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60f, Time.deltaTime * 5f);
                }
                break;
        }

        /*if (Input.GetKey(KeyCode.LeftShift))
        {
            isRunning = true;
            moveSpeed = runSpeed;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, runFOV, Time.deltaTime * 5f);
        }
        else
        {
            isRunning = false;
            moveSpeed = 10f;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60f, Time.deltaTime * 5f);
        }*/

        // 使用缓存的 rootT（Transform）将本地输入旋转成世界方向，减少每帧访问 transform 的开销
        // 保证竖直分量为 0，避免意外修改竖直速度（例如跳跃/重力）
        desiredDirection = rootT.rotation * rawInput;
        desiredDirection.y = 0f;

        // 状态切换：当有方向输入且处于平衡时，设为行走状态
        // 注意把 rawInput 的 x/z 与 0 比较以判断是否存在输入（比长时间读取 GetAxis 更稳定）
        if ((rawInput.x != 0f || rawInput.z != 0f) && balanced)
        {
            if (!WalkForward && !moveAxisUsed)
            {
                WalkForward = true;
                moveAxisUsed = true;
                isKeyDown = true;
            }
        }
        // 无输入时恢复静止状态
        else if (rawInput.x == 0f && rawInput.z == 0f)
        {
            if (WalkForward && moveAxisUsed)
            {
                WalkForward = false;
                moveAxisUsed = false;
                isKeyDown = false;
            }
        }

        // 备注：不要在这里改变刚体（rootRb）的 velocity；将物理写入放到 FixedUpdate/ApplyMovementPhysics，
        // 能让物理表现与 Unity 的 Fixed Timestep 保持一致，避免抖动与物理不稳定。
    }

    /// <summary>
    /// 在 FixedUpdate() 中调用：将由 ReadMovementInput 计算得到的期望方向转换为目标速度，并应用到 rootRb（刚体）。
    /// </summary>
    /// <remarks>
    /// - 该方法负责把输入转换为真正的物理速度修改，因此必须在 FixedUpdate（或等同的物理更新节奏）中调用；
    /// - 通过一次性读取并重用 rootRb.velocity，减少重复获取刚体数据与多次写入；
    /// - 使用插值（Vector3.Lerp）对速度做平滑过渡，避免瞬时速度跳变导致的抖动；
    /// - 将竖直速度（y 分量）保持为当前刚体的 y 值，从而不破坏重力或跳跃逻辑；
    /// - 建议将插值系数（例如 lerpT）作为可配置字段（moveSmoothing / moveLerpFactor）以便调参。
    /// </remarks>
    private void ApplyMovementPhysics()
    {
        // 保护性检查：确保缓存的 APR_Rbs[0] 不为空（防止在编辑器或初始化顺序异常时抛空）
        if (APR_Rbs[0] == null)
            return;

        // 读取当前刚体速度一次到局部变量，避免多次访问 APR_Rbs[0].velocity
        Vector3 currentVel = APR_Rbs[0].velocity;

        // 计算目标水平速度（不修改 y 分量），把 desiredDirection 映射到 moveSpeed
        Vector3 targetVel = desiredDirection * moveSpeed;
        targetVel.y = currentVel.y; // 保持当前竖直速度以尊重重力/跳跃

        // 指数平滑系数（基于固定时间步长）：lerpT ∈ [0,1)
        // 公式：lerpT = 1 - exp(-k * dt)，k = moveResponse（越大越快），dt = Time.fixedDeltaTime
        float lerpT = 1f - Mathf.Exp(-moveResponse * Time.fixedDeltaTime);

        // 对速度进行平滑插值
        Vector3 newVel = Vector3.Lerp(currentVel, targetVel, lerpT);

        // 写回刚体速度
        APR_Rbs[0].velocity = newVel;
    }

    /// <summary>
    /// 处理玩家伸手（Reach）相关输入：身体弯曲、左右手伸展、关节刚度切换与姿势重置标志设置。
    /// </summary>
    /// <remarks>
    /// - 包含三部分：Body Bending（躯干弯曲）、Reach Left（左手伸出）、Reach Right（右手伸出）；
    /// - 在伸手开始时，会把对应手臂关节的 angular drives 切换为较高的 ReachStiffness 以获得精准控制；
    /// - 在伸手结束时，根据 balanced 状态把关节驱动恢复为 PoseOn（平衡时）或 DriveOff（非平衡时）；
    /// - MouseYAxisArms / MouseYAxisBody 的范围限制用于避免输入导致异常的四元数值；
    /// - targetRotation 使用 Quaternion 构造（注意：直接用四元数字面量赋值不是严格的旋转构造方法，但在此脚本中作为近似可用）。
    /// </remarks>
    private void PlayerReach()
    {
        // Body Bending（躯干弯曲）——用 Mouse Y 驱动 Body 的 targetRotation（俯仰）
        if (1 == 1) // 原代码保留的占位条件（等同于始终执行）
        {
            if (MouseYAxisBody <= 0.9f && MouseYAxisBody >= -0.9f)
            {
                MouseYAxisBody = MouseYAxisBody + (Input.GetAxis("Mouse Y") / reachSensitivity);
            }
            else if (MouseYAxisBody > 0.9f)
            {
                MouseYAxisBody = 0.9f;
            }
            else if (MouseYAxisBody < -0.9f)
            {
                MouseYAxisBody = -0.9f;
            }

            // 直接修改 Body 的 targetRotation（注意：这里使用四元数的分量赋值，可能不是严格单位四元数）
            APR_Joints[1].targetRotation = new Quaternion(MouseYAxisBody, 0, 0, 1);
        }

        // Reach Left（左手伸手）
        if (Input.GetAxisRaw(reachLeft) != 0)
        {
            if (!reachLeftAxisUsed)
            {
                // 第一次启用左手伸手时，把左臂的驱动切换为 ReachStiffness，且加固躯干 CoreStiffness
                APR_Joints[5].angularXDrive = ReachStiffness;
                APR_Joints[5].angularYZDrive = ReachStiffness;
                APR_Joints[6].angularXDrive = ReachStiffness;
                APR_Joints[6].angularYZDrive = ReachStiffness;

                APR_Joints[1].angularXDrive = CoreStiffness;
                APR_Joints[1].angularYZDrive = CoreStiffness;

                reachLeftAxisUsed = true;
            }

            // 控制手臂上下角度，限制在 [-1.2, 1.2] 范围内
            if (MouseYAxisArms <= 1.2f && MouseYAxisArms >= -1.2f)
            {
                MouseYAxisArms = MouseYAxisArms + (Input.GetAxis("Mouse Y") / reachSensitivity);
            }
            else if (MouseYAxisArms > 1.2f)
            {
                MouseYAxisArms = 1.2f;
            }
            else if (MouseYAxisArms < -1.2f)
            {
                MouseYAxisArms = -1.2f;
            }

            // 设置上臂的目标姿态（upper left arm pose）
            APR_Joints[5].targetRotation = new Quaternion(-0.58f - (MouseYAxisArms), -0.88f - (MouseYAxisArms), -0.8f, 1);
        }

        // 左手不抬起，则恢复对应刚度 / 标志
        if (Input.GetAxisRaw(reachLeft) == 0)
        {
            if (reachLeftAxisUsed)
            {
                if (balanced)
                {
                    // 如果处于平衡状态，恢复为 PoseOn（保留姿势控制）
                    APR_Joints[5].angularXDrive = PoseOn;
                    APR_Joints[5].angularYZDrive = PoseOn;
                    APR_Joints[6].angularXDrive = PoseOn;
                    APR_Joints[6].angularYZDrive = PoseOn;

                    APR_Joints[1].angularXDrive = PoseOn;
                    APR_Joints[1].angularYZDrive = PoseOn;
                }
                else if (!balanced)
                {
                    // 非平衡时把手臂驱动关闭（DriveOff）以避免强制姿势导致不自然物理效果
                    APR_Joints[5].angularXDrive = DriveOff;
                    APR_Joints[5].angularYZDrive = DriveOff;
                    APR_Joints[6].angularXDrive = DriveOff;
                    APR_Joints[6].angularYZDrive = DriveOff;
                }

                ResetPose = true; // 标记需要重置姿势（下一帧或特定流程中会处理）
                reachLeftAxisUsed = false;
            }
        }

        // Reach Right（右手伸手）——逻辑与左手对称
        if (Input.GetAxisRaw(reachRight) != 0)
        {
            if (!reachRightAxisUsed)
            {
                APR_Joints[3].angularXDrive = ReachStiffness;
                APR_Joints[3].angularYZDrive = ReachStiffness;
                APR_Joints[4].angularXDrive = ReachStiffness;
                APR_Joints[4].angularYZDrive = ReachStiffness;

                APR_Joints[1].angularXDrive = CoreStiffness;
                APR_Joints[1].angularYZDrive = CoreStiffness;

                reachRightAxisUsed = true;
            }

            if (MouseYAxisArms <= 1.2f && MouseYAxisArms >= -1.2f)
            {
                MouseYAxisArms = MouseYAxisArms + (Input.GetAxis("Mouse Y") / reachSensitivity);
            }
            else if (MouseYAxisArms > 1.2f)
            {
                MouseYAxisArms = 1.2f;
            }
            else if (MouseYAxisArms < -1.2f)
            {
                MouseYAxisArms = -1.2f;
            }

            // upper right arm pose（上臂目标姿态）
            APR_Joints[3].targetRotation = new Quaternion(0.58f + (MouseYAxisArms), -0.88f - (MouseYAxisArms), 0.8f, 1);
        }

        if (Input.GetAxisRaw(reachRight) == 0)
        {
            if (reachRightAxisUsed)
            {
                if (balanced)
                {
                    APR_Joints[3].angularXDrive = PoseOn;
                    APR_Joints[3].angularYZDrive = PoseOn;
                    APR_Joints[4].angularXDrive = PoseOn;
                    APR_Joints[4].angularYZDrive = PoseOn;

                    APR_Joints[1].angularXDrive = PoseOn;
                    APR_Joints[1].angularYZDrive = PoseOn;
                }
                else if (!balanced)
                {
                    APR_Joints[3].angularXDrive = DriveOff;
                    APR_Joints[3].angularYZDrive = DriveOff;
                    APR_Joints[4].angularXDrive = DriveOff;
                    APR_Joints[4].angularYZDrive = DriveOff;
                }

                ResetPose = true;
                reachRightAxisUsed = false;
            }
        }
    }

    /// <summary>
    /// 玩家旋转逻辑：根据摄像机的朝向调整角色整体方向（仅水平平面旋转）。
    /// </summary>
    /// <remarks>
    /// - 通过摄像机的 forward 向量作为参考，只取 XZ 平面的投影，保证角色不因摄像机俯仰产生倾斜；
    /// - 使用 Quaternion.LookRotation 构造目标朝向，随后取其 Inverse 与关节的 targetRotation 插值，保证平滑转身；
    /// - 旋转插值使用 Quaternion.Slerp，平滑度由 turnSpeed 控制，基于 Time.fixedDeltaTime 保持物理时间步稳定性。
    /// </remarks>
    private void PlayerRotation()
    {
        Vector3 lookPos = cam.transform.forward;
        lookPos.y = 0;
        Quaternion rotation = Quaternion.LookRotation(lookPos);
        APR_Joints[0].targetRotation = Quaternion.Slerp(APR_Joints[0].targetRotation, Quaternion.Inverse(rotation), Time.fixedDeltaTime * turnSpeed);
    }

    /// <summary>
    /// 玩家跳跃/起身逻辑：根据输入触发跳跃或在失衡时切换到 Ragdoll。
    /// </summary>
    /// <remarks>
    /// - 输入处理：当检测到 jump 轴有值时，若 jumpAxisUsed 为 false（即第一次按下），根据角色平衡状态决定：
    ///   - 平衡且在地面：触发 jumping 标志；
    ///   - 不平衡：调用 DeactivateRagdoll()，角色进入全物理状态；
    /// - 当 jumping 为 true：
    ///   - 施加向上的速度（v3 = up * jumpForce），同时保留原有水平速度分量（x/z 不变）；
    ///   - 设置 isJumping = true；
    /// - 定时器逻辑：通过 timer 控制跳跃持续时间，超过 0.2 秒后重置 jumping/isJumping，并将角色标记为 inAir；
    /// - jumpAxisUsed 防抖：避免持续按下时重复触发。
    /// </remarks>
    private void PlayerGetUpJumping()
    {
        if (Input.GetAxis(jump) > 0)
        {
            if (!jumpAxisUsed)
            {
                if (balanced && !inAir)
                {
                    jumping = true;
                }
                else if (!balanced)
                {
                    DeactivateRagdoll();
                }
            }
            jumpAxisUsed = true;
        }
        else
        {
            jumpAxisUsed = false;
        }

        if (jumping)
        {
            //播放音效
            AudioManager.Instance.PlaySFX("jump");
            isJumping = true;

            Vector3 v3 = APR_Rbs[0].transform.up * jumpForce;
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
    /// PlayerHit 中触发 HitFly，实现玩家被击飞的效果（用途大，有待开发）
    /// </summary>
    /// <remarks>
    /// - 总体逻辑与跳跃一致；
    /// - PlayerHit改变标志，FixedUpdate中该方法得以被调用；
    /// - 开启 jumping 的标志，然后使用跳跃的逻辑；
    /// - 跳跃高度由 hitFlyForce 控制；
    /// - 其他脚本可以引用PlayerController，然后通过设置 isHitFly 来触发击飞效果。
    /// </remarks>
    private void HitFly()
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

    #region 订阅绑定助手
    /// <summary>
    /// 尝试订阅 LevelManager（若 Instance 已就绪）。此方法可安全频繁调用（内部会检查）。
    /// </summary>
    private void TrySubscribeToLevelManager()
    {
        if (LevelManager.Instance != null)
        {
            // 防止重复订阅
            LevelManager.Instance.OnPlayerPhaseRequested -= onPlayerPhaseRequestedHandler;
            LevelManager.Instance.OnPlayerPhaseRequested += onPlayerPhaseRequestedHandler;
        }
    }

    /// <summary>
    /// 显式绑定到 LevelManager 的入口（供 LevelSceneInitializer 在注册 playerController 时调用）。
    /// 使用此方法可以保证在 LevelManager 常驻但 PlayerController 在场景后创建时及时绑定。
    /// </summary>
    public void BindToLevelManager()
    {
        TrySubscribeToLevelManager();
    }

    /// <summary>
    /// 取消订阅（在 OnDisable / OnDestroy / 替换绑定时调用）
    /// </summary>
    private void UnsubscribeFromLevelManager()
    {
        if (LevelManager.Instance != null && onPlayerPhaseRequestedHandler != null)
        {
            LevelManager.Instance.OnPlayerPhaseRequested -= onPlayerPhaseRequestedHandler;
        }
    }

    #endregion

    #region LevelManager事件处理器

    /// <summary>
    /// LevelManager 发来切换相位的请求时被调用（通过事件）。仅更新 state 并广播 OnPhaseChanged。
    /// 具体表现（材质、粒子）由 PlayerEffect 等订阅者实现。
    /// </summary>
    /// <param name="phase"></param>
    private void HandleRequestedPhase(PlayerPhase phase)
    {
        if (phase == currentPhase) return;

        // 更新当前相位
        currentPhase = phase;

        // 广播变化（表现层订阅者会执行渐变、粒子等）
        OnPhaseChanged?.Invoke(currentPhase);

        Debug.Log($"[PlayerController] Phase changed to {currentPhase}");
    }

    #endregion

    #region 公共API

    public void EnableInput(bool beEnable)
    {
        useControls = beEnable;
    }

    #endregion
}
