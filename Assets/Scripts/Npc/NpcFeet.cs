using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NpcFeet : MonoBehaviour
{
    public Npc npc;

    private void OnCollisionEnter(Collision col)
    {
        //时刻检查身体是否平衡，若不平衡且脚着地，则恢复平衡
        if (!npc.balanced)
        {
            if (col.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                npc.NpcFellDown();
            }
        }

        //Npc的标志，决定Npc倒地不起还是能够恢复
        if (col.gameObject.tag == "Pool")
        {
            if (npc.afterPoolFallCanRecover)
            {

                Debug.Log($"Npc睬到了水坑，将于 {npc.poolFallRecoveryTime} 恢复");
                npc.isFallDown = true;
                npc.balanceHeight = 0f;

                StartCoroutine(FallCanRecover());
            }
            else
            {
                Debug.Log("Npc睬水坑倒地不起");
                npc.isFallDown = true;
                npc.balanceHeight = 0f;

                StartCoroutine(FallNotRecover());
            }
        }
    }

    private IEnumerator FallCanRecover()
    {
        yield return new WaitForSeconds(npc.poolFallRecoveryTime);
        npc.balanceHeight = 2.5f;
    }

    private IEnumerator FallNotRecover()
    {
        yield return new WaitForSeconds(1f);
        npc.enabled = false;
    }
}
