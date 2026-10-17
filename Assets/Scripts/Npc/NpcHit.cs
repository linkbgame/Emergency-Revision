using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NpcHit : MonoBehaviour
{
    public Npc npc;
    public bool useBTHitFly = false;

    private float hitCD;
    private bool isOnCooldown = false;
    private Coroutine cooldownCoroutine = null;

    private void Awake()
    {
        hitCD = npc.hitCD;
    }

    #region Unity回调
    private void OnCollisionEnter(Collision col)
    {
        //先判断是否被车撞，之后再来判断霸体的Contact
        if (col.gameObject.layer == LayerMask.NameToLayer("HitFly") && !npc.isHitFly)
        {
            npc.isHitFly = true;
            StartCoroutine(HitFlyThanRecover());
        }

        TryHandleContact(col.gameObject);
    }
    #endregion

    #region Contact 处理逻辑
    /// <summary>
    /// 公共入口：尝试对接触到的物体执行命中逻辑（如果不在冷却中其物体是 Player ）。
    /// </summary>
    /// <param name="obj">接触到的游戏对象（Collider.gameObject 或 Collision.gameObject）</param>
    private void TryHandleContact(GameObject obj)
    {
        //判断冷却
        if (isOnCooldown) return;

        //判断层级
        if (obj.gameObject.layer != LayerMask.NameToLayer("Player")) return;

        //找到组件
        //尝试找到 PlayerController（在父物体或自身）
        PlayerController pc = obj.GetComponent<PlayerController>();
        if (pc == null)
        {
            pc = obj.GetComponentInParent<PlayerController>();
        }

        // 检查玩家相位 —— 你需求中的“Fire”相位（根据你的 PlayerController 枚举名调整）
        if (pc.currentPhase == PlayerController.PlayerPhase.HeadOnFire)
        {
            // 执行命中效果（实际实现留给你）
            PerformBeHitAction();


            // 启动冷却
            StartCooldown();
        }
    }
    #endregion

    #region CD(Cooldown冷却)处理
    private void StartCooldown()
    {
        if (cooldownCoroutine != null)
        {
            StopCoroutine(cooldownCoroutine);
            cooldownCoroutine = null;
        }
        cooldownCoroutine = StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
        isOnCooldown = true;

        // 受 timeScale 影响：使用 WaitForSeconds（更简洁）
        yield return new WaitForSeconds(hitCD);

        isOnCooldown = false;
        cooldownCoroutine = null;
    }

    /// <summary>
    /// 立即取消冷却并允许再次触发（供外部调用，测试或特殊行为）。
    /// </summary>
    public void ResetCooldown()
    {
        if (cooldownCoroutine != null)
        {
            StopCoroutine(cooldownCoroutine);
            cooldownCoroutine = null;
        }
        isOnCooldown = false;
    }

    private IEnumerator HitFallThanRecover()
    {
        yield return new WaitForSeconds(npc.hitFallRecoveryTime);
        npc.balanceHeight = 2.5f;
    }

    private IEnumerator HitFlyThanRecover()
    {
        yield return new WaitForSeconds(npc.hitFlyRecoveryTime);
        npc.isHitFly = false;
    }
    #endregion


    private void PerformBeHitAction()
    {
        Debug.Log("Npc被霸体影响");
        npc.isFallDown = true;
        npc.balanceHeight = 0f;

        if (useBTHitFly)
            npc.isHitFly = true;

        StartCoroutine(HitFallThanRecover());
    }

    //碰到了Player某个身体部位，判断是否为Player层，然后找到父对象中的PC脚本

    //若Player的状态是Fire，则执行一次方法，本方法执行过后，等待 3 秒之后，才能执行下一次方法，CD为3

    //具体方法：Npc瘫倒在地，2 秒之后恢复；或者先给予一个力，然后让Npc瘫倒，做出振飞的效果
}
