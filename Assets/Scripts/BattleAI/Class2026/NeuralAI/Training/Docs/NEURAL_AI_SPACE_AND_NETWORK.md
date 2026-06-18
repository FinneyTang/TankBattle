# NeuralAI 观察空间、行为空间与网络结构

本文说明 `Assets/NeuralAI/Inference` 中神经网络坦克看到什么、输出什么动作、它和传统规则 AI 的区别，以及训练配置里的网络大致是什么结构。

## 1. 和其他算法一样吗？

不完全一样。

项目里的传统算法通常继承 `Main.Tank`，在 `Update()` 中直接读取游戏对象和 API，然后用手写规则实时调用 `Move()`、`TurretTurnTo()`、`Fire()`。它们可以访问更丰富的游戏状态，也能写复杂的搜索、状态机、行为树或启发式逻辑。

`NeuralAI.TankBattleAgent` 也是一个 `Main.Tank`，所以可以像其他算法一样填进 `Match.TeamSettings` 并实时参赛。但它不会直接把整套游戏对象交给网络，而是先把战场压缩成固定长度的 128 维向量，再让 ML-Agents 策略输出动作。网络只能基于这组固定形状的数字决策。

因此：

- 运行入口和比赛接入方式与其他算法一样：脚本名仍是 `NeuralAI.TankBattleAgent`。
- 决策方式不同：规则 AI 是手写逻辑，NeuralAI 是向量观测 + 神经网络推理。
- 可见信息不同：NeuralAI 只看到编码后的摘要信息，不直接看到完整对象列表。
- 实时性可以做到：推理在 Unity 内通过 ML-Agents 执行，不需要 Python trainer 在线。当前训练工程使用 release_21 / Sentis，分发兼容包也可以切回 2.0.2 / Barracuda。

## 2. 实时运行方式

实时比赛时不需要启动 `mlagents-learn`。训练完成后，把导出的模型资源导入 Unity，并在 `Behavior Parameters` 上指定模型即可。

运行流程：

1. `Match` 根据队伍配置创建 `NeuralAI.TankBattleAgent`。
2. `TankBattleAgent` 自动挂载并绑定 `TankBattleMlAgent`。
3. `TankBattleMlAgent` 配置行为名、观察维度和动作空间。
4. 每隔 `DecisionInterval` 帧请求一次新决策。
5. 非决策帧调用 `RequestAction()`，重复上一帧动作，保持移动和瞄准连续。
6. `TankBattleActionMapper` 把网络动作转换成 `Move()`、`TurretTurnTo()`、`Fire()`。

默认 `DecisionInterval = 3`，也就是约每 3 帧产生一次新网络决策。这个频率比每帧推理更省，但仍然可以实时运行。若希望反应更快，可以降低 `DecisionInterval`；若希望性能更稳，可以提高它。

## 3. 行为名和空间大小

当前训练和推理必须保持这些配置一致：

```text
Behavior Name: TankBattlePPO
Vector Observation Size: 128
Continuous Actions: 4
Discrete Branches: [2]
```

对应代码：

- `TankBattleTrainingSettings.BehaviorName`
- `TankBattleTrainingSettings.ObservationSize`
- `TankBattleTrainingSettings.ContinuousActionSize`
- `TankBattleTrainingSettings.DiscreteBranches`

## 4. 观察空间

观察空间是 128 个 `float`，由 `BattleObservationEncoder` 生成。编码器当前写入 117 个有效特征，最后补 11 个 `0` 以保持模型输入尺寸稳定。大多数距离和位置会按地图尺寸归一化到 `[-1, 1]`，生命值、时间、数量等会归一化到 `[0, 1]`，布尔值用 `0` 或 `1` 表示。

当前结构：

- 自身状态：14 维。
- 最近 3 个敌人：`3 x 11 = 33` 维，不足时补零。
- 最近队友：9 维，没有队友时补零。
- 比赛全局状态：5 维。
- 星星状态：24 维，包括 3 个普通星槽位和 1 个超级星槽位。
- 最近 3 枚敌方导弹：`3 x 8 = 24` 维，不足时补零。
- LiDAR：8 维，表示 8 个方向射线到最近障碍物的距离。
- 保留填充：11 维，值为 0。

## 5. 行为空间

行为空间由 4 个连续动作和 1 个离散动作分支组成。

### 连续动作，4 维

| 动作索引 | 含义 | 范围 | 映射 |
| --- | --- | --- | --- |
| 0 | 移动方向 X | 通常 `[-1, 1]` | 和动作 1 组成移动向量 |
| 1 | 移动方向 Z | 通常 `[-1, 1]` | 移动向量会被限制到长度 1 |
| 2 | 瞄准方向 X | 通常 `[-1, 1]` | 和动作 3 组成炮塔瞄准方向 |
| 3 | 瞄准方向 Z | 通常 `[-1, 1]` | 瞄准向量会被限制到长度 1 |

移动向量超过 `ActionDeadZone` 时，会执行：

```text
目标点 = 当前坐标 + 移动方向 * MoveStepDistance
```

然后调用 `tank.Move(target)`。

瞄准向量超过 `ActionDeadZone` 时，会执行：

```text
瞄准点 = 当前坐标 + 瞄准方向 * AimDistance
```

然后调用 `tank.TurretTurnTo(aimTarget)`。如果瞄准向量太小，则默认转向最近敌人。

### 离散动作，1 个分支

| 分支 | 可选值 | 含义 |
| --- | --- | --- |
| 0 | `0` | 不开火 |
| 0 | `1` | 请求开火 |

当离散动作 `discrete[0] == 1` 时，会调用 `tank.Fire()`。是否真的发射成功仍取决于游戏规则，例如冷却、死亡状态、弹药或可开火条件。

## 6. 网络结构

训练配置在 `Assets/NeuralAI/Training/Configs/tankbattle_ppo_curriculum.yaml`：

```yaml
trainer_type: ppo
hyperparameters:
  batch_size: 2048
  buffer_size: 40960
  learning_rate: 0.0005
  beta: 0.01
  epsilon: 0.15
  lambd: 0.95
  num_epoch: 6
network_settings:
  normalize: true
  hidden_units: 512
  num_layers: 4
behavioral_cloning:
  demo_path: ./TrainingData/BcDemos
  strength: 0.05
  steps: 2000000
max_steps: 40000000
time_horizon: 1024
```

可以把它理解成一个多层感知机策略网络：

```text
128 维观察（×3 帧堆叠 = 384 维输入）
  -> 归一化
  -> 全连接层 512
  -> 全连接层 512
  -> 全连接层 512
  -> 全连接层 512
  -> PPO Actor 输出动作分布
      -> 4 个连续动作
      -> 1 个二分类离散开火动作
  -> PPO Critic 估计状态价值
```

ML-Agents 的 PPO 会训练 actor 和 critic。Actor 负责输出动作，critic 负责估计当前局面的价值，用来降低策略梯度训练的方差。行为克隆开启时，训练过程中还会额外加入专家动作监督，让 actor 先靠近 demo 中的专家行为。

当前奖励信号：

```yaml
reward_signals:
  extrinsic:
    gamma: 0.995
    strength: 1.0
```

这表示主要依赖环境奖励训练，折扣因子较高，鼓励模型关注较长时间跨度的收益，例如抢星、击杀、存活和最终胜负。

## 7. 为什么不直接把其他算法的代码塞给网络？

神经网络不执行手写规则代码，它只接收固定形状的数字张量。传统算法里的大量逻辑，例如路径选择、攻击条件、避弹规则，需要通过两种方式转移给网络：

1. 观察空间中提供足够相关的信息。
2. 通过专家 demo 和奖励信号让网络学出类似行为。

所以 NeuralAI 的关键不是“代码和其他算法一样”，而是“给网络的观察是否足够表达战局，动作是否足够控制坦克，奖励是否能区分好坏策略”。

## 8. 设计取舍

当前 128 维观察空间仍然保持固定长度，适合先跑通训练并稳定接入推理：

- 优点：网络小、训练和推理快、实时运行压力低。
- 优点：观察固定长度，适合 PPO 和行为克隆。
- 缺点：只记录最近敌人和最近 3 枚敌方导弹，不能完整表达多人、多弹幕复杂局面。
- 缺点：没有显式地图障碍、路径网格、历史轨迹等信息，部分策略需要靠奖励和短期记忆间接学习。

如果后续模型表现受限，可以考虑增加：

- 更多敌人或队友槽位。
- 更多导弹槽位。
- 障碍物或可通行区域特征。
- 最近几次动作或短时历史。
- 更细的星星分布信息。
