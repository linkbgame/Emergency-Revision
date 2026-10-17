-Npc
请区分不同的Npc预制体，区别在于以下设置：
标签（CanBeGrabed）、层级（HitFallDown）、移动速度（为0动不了）、
使用AI（useAI）、直接测试当前场景的AI（ableMove)

--尤其注意：
同时勾选 useAI ableMove，从Menu玩关卡，Npc开始倒计时之前就会动。
所以，如果想Npc比玩家提前动，同时勾选两个选项。
反之，Npc若与玩家一起动，仅仅勾选 useAI，关闭 ableMove

--useAI
不勾选，Npc只会直走

