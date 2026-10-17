using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NpcGenerator : MonoBehaviour
{
    [Header("NPC预制体")]
    public GameObject npcPrefab;

    [Header("场景中最大NPC数量")]
    public int maxNPCs = 10; // 场景中最大NPC数量

    [Header("起始生成时刻")]
    public float spawnAwake = 0f;

    [Header("生成间隔时间")]
    public float spawnInterval = 2f;

    //现在的寻路还比较简单粗暴，复杂后可以再开一个Navigation Manager来管理npc的寻路
    //也可以让类似功能写在NpcController里面
    [Header("生成NPC要到达的目的地")]
    public GameObject navTarget;

    [Header("入场动画的时候是否生成Npc")]
    public bool spawnAfterIntro = true;

    private int currentNPCCount = 0;   // 当前NPC数量

    //暂时也不写回收npc的功能

    private void OnEnable()
    {
        //从Menu测试
        //入场动画之后才生成
        if (LevelManager.Instance != null && spawnAfterIntro == true)
        {
            LevelManager.Instance.OnLevelStarted += StartSpawnNPCs;
        }
        //否则立刻生成
        else
        {
            StartCoroutine(SpawnNpcCrt());
        }

        //暂停生成
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelFailed += StopSpawnNPCs;
            LevelManager.Instance.OnLevelSucceeded += StopSpawnNPCs;
        }
        
    }

    private void OnDisable()
    {
        //从Menu测试
        //入场动画之后才生成
        if (LevelManager.Instance != null && spawnAfterIntro == true)
        {
            LevelManager.Instance.OnLevelStarted -= StartSpawnNPCs;    
        }
        else
        {
            StopCoroutine(SpawnNpcCrt());
        }

        //暂停生成
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelFailed -= StopSpawnNPCs;
            LevelManager.Instance.OnLevelSucceeded -= StopSpawnNPCs;
        }
    }

    private GameObject instance;
    //生成npc的协程
    IEnumerator SpawnNpcCrt()
    {
        //先等几秒
        yield return new WaitForSeconds(spawnAwake);
        while (true)
        {
            if (currentNPCCount < maxNPCs)
            {
                instance = Instantiate(npcPrefab, transform.position, transform.rotation);
                instance.GetComponent<Npc>().navTarget = navTarget;
                currentNPCCount++;
            }
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    //把协程封装为可以加入到时间中触发
    void StartSpawnNPCs()
    {
        StartCoroutine(SpawnNpcCrt());
    }

    void StopSpawnNPCs()
    {
        StopCoroutine(SpawnNpcCrt());
    }


    //在实际游戏中不显示，在scene界面画出方块和生成朝向
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * 8);
        Gizmos.DrawCube(transform.position, Vector3.one * 2);
    }

}
