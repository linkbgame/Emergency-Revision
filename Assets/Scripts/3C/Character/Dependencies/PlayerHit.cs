using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHit : MonoBehaviour
{
    //考虑无敌
    //摔倒：角色标志、碰撞物的层级（HitFallDown)、角色是否摔倒->满足条件：启用布娃娃
    //撞飞：碰撞物的层级（HitFly），角色是否撞飞->满足条件：Player跳起来（类似逻辑实现）
    //播放音乐
    [SerializeField] private PlayerController player;

    private void Awake()
    {
        if (player == null)
        {
            Debug.LogError("PlayerHit 的 player 引用没有设置好");
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        //判断：玩家非着火状态、碰撞物的层级是HitFallDown、角色没有摔倒，满足条件
        if (collision.gameObject.layer == LayerMask.NameToLayer("HitFallDown") && !player.isFallDown
            && player.currentPhase != PlayerController.PlayerPhase.HeadOnFire)
        {
            Debug.Log("Player满足条件，摔倒了");
            //摔倒之后标志取true，hitRecoveryTime秒后恢复false
            player.isFallDown = true;
            player.balanceHeight = 0f;

            //启用布娃娃，并且开始协程
            //player.ActivateRagdoll();
            StartCoroutine("RecoverFromFall");

            //播放音乐
            AudioManager.Instance.PlaySFX("fallDown");
        }

        //判断：碰撞物的层级是HitFallDown、角色没有击飞，满足条件
        //什么状态都会被击飞
        if (collision.gameObject.layer == LayerMask.NameToLayer("HitFly") && !player.isHitFly)
        {
            player.isHitFly = true;

            StartCoroutine("RecoverFromHitFly");

        }
    }

    //从摔倒中恢复过来
    private IEnumerator RecoverFromFall()
    {
        yield return new WaitForSeconds(player.hitFallRecoveryTime);
        player.isFallDown = false;
        //原来的摔倒逻辑
        //player.DeactivateRagdoll();
        player.balanceHeight = 6.4f;
    }

    private IEnumerator RecoverFromHitFly()
    {
        yield return new WaitForSeconds(player.hitFlyRecoveryTime);
        player.isHitFly = false;
    }
}
