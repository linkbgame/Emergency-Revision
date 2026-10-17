using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFeet : MonoBehaviour
{
    public PlayerController player;

    //写死检测地面
    private void OnCollisionEnter(Collision col)
    {
        if (!player.isJumping && player.inAir)
        {
            if (col.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                player.CharacterLanded();
            }
        }
    }
}
