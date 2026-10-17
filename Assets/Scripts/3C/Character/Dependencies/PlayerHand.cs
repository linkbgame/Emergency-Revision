using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHand : MonoBehaviour
{
    public PlayerController player;
    public AudioClip grabClip;

    [SerializeField] private bool isLeftHand;

    private bool hasJoint;


    private void Update()
    {
        if (player.useControls)
        {
            if (isLeftHand)
            {
                if (hasJoint && Input.GetAxisRaw(player.reachLeft) == 0)
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
                if (hasJoint && Input.GetAxisRaw(player.reachRight) == 0)
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
        if (player.useControls)
        {
            if (isLeftHand)
            {
                //写死了抓取的判定是"CanBeGrabbed"标签，并且不能是玩家自己
                if (col.gameObject.tag == "CanBeGrabbed" && col.gameObject.layer != LayerMask.NameToLayer("Player") && !hasJoint)
                {
                    if (Input.GetAxisRaw(player.reachLeft) != 0 && !hasJoint)
                    {
                        hasJoint = true;
                        this.gameObject.AddComponent<FixedJoint>();
                        this.gameObject.GetComponent<FixedJoint>().breakForce = Mathf.Infinity;
                        this.gameObject.GetComponent<FixedJoint>().connectedBody = col.gameObject.GetComponent<Rigidbody>();

                        AudioManager.Instance.PlaySFX("handGrab");
                    }
                }
            }

            if (!isLeftHand)
            {
                if (col.gameObject.tag == "CanBeGrabbed" && col.gameObject.layer != LayerMask.NameToLayer("Player") && !hasJoint)
                {
                    if (Input.GetAxisRaw(player.reachRight) != 0 && !hasJoint)
                    {
                        hasJoint = true;
                        this.gameObject.AddComponent<FixedJoint>();
                        this.gameObject.GetComponent<FixedJoint>().breakForce = Mathf.Infinity;
                        this.gameObject.GetComponent<FixedJoint>().connectedBody = col.gameObject.GetComponent<Rigidbody>();

                        AudioManager.Instance.PlaySFX("handGrab");
                    }
                }
            }
        }
    }
}
