# TankBattle
一个用于游戏人工智能教学和练习的Demo

By Jiaqi Tang

[AI分享站 http://www.aisharing.com](http://www.aisharing.com)

有时候为了练习或者演示一些AI的技术，总是找不到一个平台，为此写了一个小的Demo，可以用来作为AI的教学和练习。这个Demo是一个经典的坦克大战，可以支持自定义坦克脚本，进行两方对抗，不管是吃星星还是击毁对方都可以获取一定的分数，当比赛结束后，分数高的一方获胜。项目中默认已经添加了一个简单AI脚本，如果大家有兴趣写一下自己的Tank AI脚本，可以发给我（finneytang@gmail.com），我会添加到项目中，让更多的人参考。

## Unity版本

2017.2.3p2

## 游戏启动方式

- 打开BattleField，
- 选中场景中的Match，在Inspector中进行Team和Global参数的设置，或者直接使用默认参数
- 运行即可

## 游戏规则

- 吃星星可以获取分数
- 击杀对手可以获取分数
- 当比赛时间用尽时，分数高的一方赢得比赛

## 参数

### Team参数
- Reborn：重生点
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

## 自定义Tank脚本说明

- 自定义Tank脚本必须继承自Tank类，建议使用独立文件夹，并且加上namespace以保证类名不会产生冲突
- 必须重载GetName方法，这个方法返回Tank的名字
- 自定义Tank脚本不要重写原来MonoBehaviour中的Awake，Start，Update，OnDrawGizmos函数，而是重载对应的OnAwake，OnStart，OnUpdate，OnOnDrawGizmos函数
- OnReborn函数会在每次坦克重生的时候调用

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
- GetName()：返回坦克的名字
- NavMeshPath CaculatePath(Vector3 targetPos)：获取到目标位置的路径
- TurretTurnTo(Vector3 targetPos)：把炮管转向目标位置
- Move(NavMeshPath path)：按照path移动，返回值表示是否成功
- Move(Vector3 targetPos)：移动到目标点，返回值表示是否成功
- Fire()：以当前炮塔朝向开火，返回值表示是否成功

### Missile类
- Team：返回导弹所属的坦克队伍
- ID：返回导弹唯一ID
- Velocity：返回导弹的速度
- Position：返回导弹的位置

### Star类
- ID：返回星星唯一ID
- Position：返回星星的位置

### Match类

Match类为单例，可以通过Match.instance访问

- GlobalSetting：获取比赛设置，其中参数见上面“参数”部分
- GetOppositeTank(ETeam myTeam)：获取敌方坦克实例，参数是自己的队伍
- GetStars()：获取当前的所有星星列表
- GetOppositeMissiles(ETeam myTeam)：获取当前所有的敌方射出的导弹列表
- IsMathEnd()：比赛是否结束
- RemainingTime：获取比赛剩余时间

### PhysicsUtils类

- MaxFieldSize：比赛场地的边长
- LayerMaskCollsion：所有带碰撞的Layer Mask
- LayerMaskScene：场景碰撞的Layer Mask
- LayerMaskTank：坦克碰撞的Layer Mask
- IsFireCollider(Collider col)：是否是Tank的碰撞体

