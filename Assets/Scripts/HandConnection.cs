using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandConnection : MonoBehaviour
{
    public Character Player;

    [SerializeField] private bool isLeftHand;

    private bool hasJoint;

    
    private void Update()
    {
        if (Player.useControls)
        {
            if (isLeftHand)
            {
                if (hasJoint && Input.GetAxisRaw(Player.reachLeft) == 0)
                {
                    this.gameObject.GetComponent<FixedJoint>().breakForce = 0;
                    hasJoint = false;
                }
                if (hasJoint && this.gameObject.GetComponent<FixedJoint>() == null)
                {
                    hasJoint = false;
                }
            }
            else
            {
                if (hasJoint && Input.GetAxisRaw(Player.reachRight) == 0)
                {
                    this.gameObject.GetComponent<FixedJoint>().breakForce = 0;
                    hasJoint = false;
                }
                if (hasJoint && this.gameObject.GetComponent<FixedJoint>() == null)
                {
                    hasJoint = false;
                }
            }
        }
    }

    private void OnCollisionEnter(Collision col)
    {
        if (Player.useControls)
        {
            if (isLeftHand)
            {
                //写死了抓取的判定是"CanBeGrabbed"标签，并且不能是玩家自己
                if (col.gameObject.tag == "CanBeGrabbed" && col.gameObject.layer != LayerMask.NameToLayer("Player") && !hasJoint)
                {
                    if (Input.GetAxisRaw(Player.reachLeft) != 0 && !hasJoint)
                    {
                        hasJoint = true;
                        this.gameObject.AddComponent<FixedJoint>();
                        this.gameObject.GetComponent<FixedJoint>().breakForce = Mathf.Infinity;
                        this.gameObject.GetComponent<FixedJoint>().connectedBody = col.gameObject.GetComponent<Rigidbody>();
                    }
                }
            }

            if (!isLeftHand)
            {
                if (col.gameObject.tag == "CanBeGrabbed" && col.gameObject.layer != LayerMask.NameToLayer("Player") && !hasJoint)
                {
                    if (Input.GetAxisRaw(Player.reachRight) != 0 && !hasJoint)
                    {
                        hasJoint = true;
                        this.gameObject.AddComponent<FixedJoint>();
                        this.gameObject.GetComponent<FixedJoint>().breakForce = Mathf.Infinity;
                        this.gameObject.GetComponent<FixedJoint>().connectedBody = col.gameObject.GetComponent<Rigidbody>();
                    }
                }
            }
        }
    }
}
