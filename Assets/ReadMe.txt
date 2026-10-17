-测试
Scenes/Menu开始或者Scenes/Splash开始

-主角

--预制体
在Resources/Prefabs目录下

--受击
碰到Layer为HitFallDown的Collider物体会摔倒，恢复时间为HitFallRecoveryTime
碰到Layer为HitFly的Collider物体会被击飞，恢复时间为HitFlyRecoveryTime



-Npc

--受击（NpcHit）分两种情况
摔倒后会起立————调用一次Npc的布娃娃系统，隔一段CD，让他关闭布娃娃系统，他就会自己站起来
摔倒后不会起立————将Npc的Balance Height 设置为0，摔倒后不会起立
