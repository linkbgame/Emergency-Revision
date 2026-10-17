
-关卡



--核心
配置好Menu场景里的Core对象，以及每个场景要配置好LevelSceneInitializer里面需要引用的对象



--管理
每一个关卡的Hierarchy里，划分了关卡初始化设置、动画、场景这几个部分，之后根据实际情况添加



--配置
3C：Player参数，MainCamera参数，Player上的PlayerEffect脚本，要引用SpeedLines预制体

动画：CutsceneManager空物体上的CutsceneManager脚本参数（需引用A空物体上的PlayerableDirector组件的A.playerable文件），空物体和playerable文件要同名；入场动画命名——Intro_Level01，胜利动画命名——Victory_Level01

关卡初始化：配置LevelSceneInitializer上的LevelTimer脚本，HUD预制体，PauseMenu预制体，ResultUI预制体；




--可选
Menu的Core关掉Show HUD，同时禁用关卡场景内的HUD预制体，就不会显示倒计时，反之两者都打开就会显示倒计时
