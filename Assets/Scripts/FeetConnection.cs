using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FeetConnection : MonoBehaviour
{
    public Character Player;

    //通知Player当脚部碰撞器进入地面对象层时
    void OnCollisionEnter(Collision col)
    {
        if (!Player.isJumping && Player.inAir)
        {
            if (col.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                Player.CharacterLanded();
            }
        }
    }
}
