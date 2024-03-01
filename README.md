# TankBattle
用于游戏人工智能练习的项目

By Jiaqi Tang

[AI分享站 http://www.aisharing.com](http://www.aisharing.com)

有时候为了练习或者演示一些AI的技术，总是找不到一个平台，为此写了一个小项目，可以用来作为AI的教学和练习。这个项目是一个经典的坦克大战，可以支持自定义坦克脚本，进行两方对抗，不管是吃星星还是击毁对方都可以获取一定的分数，当比赛结束后，分数高的一方获胜。项目中默认已经添加了一个简单AI脚本，如果大家有兴趣写一下自己的Tank AI脚本，可以发给我（finneytang@gmail.com），我会添加到项目中，让更多的人参考。

## Unity版本

2021.3.27f1

## 游戏启动方式

- 打开BattleField（2人），打开BattleField2X（4人）
- 选中场景中的Match，在Inspector中进行Team和Global参数的设置，或者直接使用默认参数
- 运行即可

## 游戏规则

- 吃星星可以获取分数
- 击杀对手可以获取分数
- 在时间还剩一半的时候在地图中央会产生一个分数更高的超级星星
- 回到自己的重生点可以以一定速度回血
- 当比赛时间用尽时，分数高的一方赢得比赛

## 参数

### Team参数

- Team：坦克所属队伍
- Tank Script：用户自定义Tank脚本的名字，namespace.classname的格式

### Global参数
- Math Time：比赛时间
- Fire Interval：开火间隔
- Missile Speed：导弹速度
- Max HP：坦克最大血量
- Reborn CD：重生需要的时间
- Damage Per Hit：导弹伤害
- Score For Star：获取每颗星星所得分数
- Score For Kill：每次摧毁敌方坦克所得分数
- Star Add Interval：生成星星的间隔
- Max Star Count：同时存在的最大星星数量
- HP Recovery Speed：每秒回血的速度
- Home Zone Radius：以重生点为圆心，回血区域的半径

## 自定义Tank脚本说明

- 自定义Tank脚本必须继承自Tank类，建议使用独立文件夹，并且加上namespace以保证类名不会产生冲突
- 必须重载GetName方法，这个方法返回Tank的名字
- 自定义Tank脚本不要重写原来MonoBehaviour中的Awake，Start，Update，OnDrawGizmos函数，而是重载对应的OnAwake，OnStart，OnUpdate，OnOnDrawGizmos函数
- OnReborn函数会在每次坦克重生的时候调用
- OnHandleSendTeamStrategy函数会在队友发送团队策略的时候调用，目前通过ETeamStrategy预定义了一些枚举Help（求助）, FocusFire（集火）, StaySafe（苟住）等，也可以自定义策略类型。如果需要传递参数，可以通过坦克上的SetTeamStrategyParam和GetTeamStrategyParam来设置和获取，具体用法，可以参考BattleAI\TJQ\MyTank.cs脚本

## API

由于很难做到把所有不允许访问的接口屏蔽（比如获取了Tank后，直接修改Tank.transform.position），建议仅仅使用namespace Main下所有类的public方法

### Tank类
- Team：返回坦克的队伍
- HP：返回坦克的血量
- IsDead：返回坦克是否死亡
- TurretAiming：返回坦克炮塔的朝向
- NextDestination：返回坦克当前移动的目标点
- Velocity：返回坦克的速度
- Position：返回坦克的位置
- FirePos：返回坦克炮口的位置
- Forword：返回坦克车身的朝向
- CanFire()：返回是否可以开火
- CanSeeOthers(Tank t)：是否可以看到目标坦克，返回值表示是否成功
- CanSeeOthers(Vector3 pos)：是否可以看到目标位置，返回值表示是否成功
- GetName()：返回坦克的名字
- NavMeshPath CaculatePath(Vector3 targetPos)：获取到目标位置的路径
- TurretTurnTo(Vector3 targetPos)：把炮管转向目标位置
- Move(NavMeshPath path)：按照path移动，返回值表示是否成功
- Move(Vector3 targetPos)：移动到目标点，返回值表示是否成功
- Fire()：以当前炮塔朝向开火，返回值表示是否成功
- SendTeamStratedgy(int teamStrategy)：发送团队指令

### Missile类

- Team：返回导弹所属的坦克队伍
- ID：返回导弹唯一ID
- Velocity：返回导弹的速度
- Position：返回导弹的位置

### Star类

- ID：返回星星唯一ID
- Position：返回星星的位置
- IsSuperStar：是否是超级星星

### Match类

Match类为单例，可以通过Match.instance访问

- GlobalSetting：获取比赛设置，其中参数见上面“参数”部分
- GetOppositeTank(ETeam myTeam)：获取敌方坦克实例，参数是自己的队伍，_**为兼容以前坦克脚本，仅能拿到A队或者B队的第一个坦克**_
- GetOppositeTanks(ETeam myTeam, List<Tank> outputs)：获取敌方所有坦克
- GetTank(ETeam t)：获取己方坦克，_**为兼容以前坦克脚本，仅能拿到本队的第一个坦克**_
- GetTanks(ETeam myTeam, List<Tank> outputs)：获取己方所有坦克
- GetStars()：获取当前的所有星星列表
- GetOppositeMissiles(ETeam myTeam)：获取当前所有敌方射出的导弹列表，_**为兼容以前坦克脚本，仅能拿到A队或者B队的导弹列表**_
- GetOppositeMissilesEx(ETeam myTeam, Dictionary<int, Missile> outputs = null)：获取当前所有敌方射出的导弹列表
- IsMathEnd()：比赛是否结束
- RemainingTime：获取比赛剩余时间
- GetRebornPos(ETeam t)：获取队伍的重生位置
- GetTeamColor(ETeam t)：获取队伍的颜色
- GetStarByID(int id)：根据星星的id获取星星

### PhysicsUtils类

- MaxFieldSize：比赛场地的边长
- LayerMaskCollsion：所有带碰撞的Layer Mask
- LayerMaskScene：场景碰撞的Layer Mask
- LayerMaskTank：坦克碰撞的Layer Mask
- IsFireCollider(Collider col)：是否是Tank的碰撞体

### 下一步

- (已完成)~~增加4人模式的对抗，可以是2v2，或者4人混战，这样AI的策略就更有趣~~
- (已完成)~~4人模式需要重新设计地图尺寸和一些比赛的参数~~
- (已完成)~~组队AI的接口计划采用命令式的方式，比如Tank可以进行命令呼叫，队友对于这些命令注册响应函数，这样不同的人写的AI也能进行组队策略，命令的例子比如：求助，集火某个目标坦克，包抄，掩护等等~~

