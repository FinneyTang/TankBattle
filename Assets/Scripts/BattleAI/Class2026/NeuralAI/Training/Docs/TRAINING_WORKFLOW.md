# NeuralAI 采集与训练流程

本文按项目根目录 `D:\Develop\TankBattle-master` 作为工作目录编写命令。`TrainingData/`、`results/`、`build/` 仍放在项目根目录，便于 ML-Agents 和独立构建读写。

当前训练工程使用 `com.unity.ml-agents` release_21（Unity 包 3.0.0-exp.1），对应 Python 侧 `mlagents==1.0.0`。只做分发兼容包时，可以把 Unity 包切回 `2.0.2`；模型字段兼容 2.x 的 `NNModel` 和 3.x 的 `ModelAsset`。

## 相关文件

- `Assets/NeuralAI/Training/Configs/tankbattle_ppo_curriculum.yaml`：ML-Agents PPO + 行为克隆 + 课程学习配置。网络为 `hidden_units: 512`、`num_layers: 4`。
- `Assets/NeuralAI/Training/Configs/training_config.json`：Unity 运行时课程对手池和奖励参数配置。
- `Assets/NeuralAI/Training/Tools/RecordChampionDemos.ps1`：批量采集冠军/专家 AI `.demo` 和 `.jsonl`。
- `Assets/NeuralAI/Training/Docs/NEURAL_AI_SPACE_AND_NETWORK.md`：观测空间、动作空间和网络结构说明。

`TankBattleConfigLoader` 读取 `training_config.json` 的优先级是：`build/config/training_config.json`、`Assets/NeuralAI/Training/Configs/training_config.json`、旧路径 `config/training_config.json`、`StreamingAssets/training_config.json`。发布构建时可继续用 `build/config/training_config.json` 覆盖参数。

## 1. 配置训练场景

训练和采集需要 `TrainingController`，普通推理对局不需要它。推荐使用 `Assets/Scenes/BattleFieldTraining.unity`：

1. 在场景中新建空物体 `TrainingController`。
2. 挂载 `NeuralAI.TrainingMatchController`。
3. `Training Mode` 开启。
4. `Neural Tank Script` 填 `NeuralAI.TankBattleAgent`。
5. `Use Curriculum` 开启时，课程阶段由 `tankbattle_ppo_curriculum.yaml` 的 `lesson_stage` 控制，对手池和奖励权重由 `training_config.json` 控制。
6. `Time Scale` 建议先用 `2` 到 `8`；稳定后再提高。`Target Frame Rate` 建议 `-1`。
7. `Disable LLM` 开启，避免训练或采集时访问本地 LLM。
8. `Reload Scene On Match End` 开启，让每局结束后自动重开。
9. 训练时 `Neural Model` 留空，由 `mlagents-learn` 接管策略；本地评测推理时才指定 ONNX 模型。

采集专家 demo 时额外开启：

- `Attach Demo Recorder To Opponent`
- `Record Ml Agents Demo`
- 可选 `Record Jsonl Demo`
- `Recording Match Limit`，用于录满指定局数后自动退出

这些字段也可以通过 `RecordChampionDemos.ps1` 和 `--tankbattle-*` 启动参数覆盖。

## 2. 采集专家 Demo

先构建 `build/TankBattle.exe`，然后在项目根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\Assets\NeuralAI\Training\Tools\RecordChampionDemos.ps1 `
  -ExecutablePath .\build\TankBattle.exe `
  -OutputDirectory .\TrainingData\ChampionDemos `
  -MatchesPerChampion 100 `
  -MatchTimeSeconds 180 `
  -DemoOnly `
  -Parallelism 2 `
  -TimeScale 1
```

脚本会启动独立构建，给专家坦克挂 `ExpertDemoRecorder`，输出 `.demo` 到 `TrainingData/ChampionDemos`。如果只想人工检查轨迹，可去掉 `-DemoOnly`，同时输出 `.jsonl`。

采集完成后，把要用于行为克隆的 `.demo` 放到或整理到：

```text
TrainingData/BcDemos
```

这和 `tankbattle_ppo_curriculum.yaml` 中的 `behavioral_cloning.demo_path` 保持一致。

## 3. 连通性 Smoke Test

正式长训前先跑短测试，确认行为名、观测维度和动作空间无误：

```powershell
mlagents-learn config\tankbattle_ppo_smoke.yaml `
  --run-id tankbattle_smoke_test `
  --force `
  --env build\TankBattle.exe `
  --no-graphics `
  --timeout-wait 300 `
  --env-args --tankbattle-opponent UtilityBasedAI.MyTank
```

看到 Python 端识别 `TankBattlePPO`，并开始写 reward / loss 统计，即可停止 smoke test。

## 4. 课程训练

正式训练使用新的配置路径：

```powershell
mlagents-learn Assets\NeuralAI\Training\Configs\tankbattle_ppo_curriculum.yaml `
  --run-id tankbattle_curriculum `
  --force `
  --env build\TankBattle.exe `
  --no-graphics `
  --num-envs 4 `
  --time-scale 2 `
  --timeout-wait 300 `
  --env-args --tankbattle-use-curriculum true --tankbattle-time-scale 2
```

训练配置中的 `lesson_stage` 会驱动 Unity 侧 `TankBattleCurriculum`，再由 `training_config.json` 决定各阶段对手池和奖励权重。

## 5. 继续训练与导出

中断后继续：

```powershell
mlagents-learn Assets\NeuralAI\Training\Configs\tankbattle_ppo_curriculum.yaml `
  --run-id tankbattle_curriculum `
  --resume `
  --env build\TankBattle.exe `
  --no-graphics `
  --num-envs 4 `
  --time-scale 2 `
  --timeout-wait 300 `
  --env-args --tankbattle-use-curriculum true --tankbattle-time-scale 2
```

导出的 ONNX 通常在 `results/<run-id>/TankBattlePPO.onnx`。用于分发时，把模型复制到：

```text
Assets/NeuralAI/TrainedModels/TankBattlePPO.onnx
```

然后在 `BattleField.unity` 的 `Match -> NeuralMatchConfigurator -> Override Model` 指定该模型。
