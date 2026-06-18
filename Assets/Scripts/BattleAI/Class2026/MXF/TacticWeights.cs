namespace MXF
{
    /// <summary>
    ///     战术权重配置: 集中管理所有策略 (Action) 中所有效用组件 (Utility) 的加权参数.
    ///     每个常量直接乘以对应的效用原始值, 形成最终得分; 通过调整这些倍率即可全局调参.
    ///     覆盖的策略: 吃星、接近敌人、追击、撤退、占中、躲避、回家共七种战术行为.
    /// </summary>
    public static class TacticWeights
    {
        // ==================== GetOneStar ====================

        public const float W_Star_BasicStar = 0.5f; // 星星基础价值倍率
        public const float W_Star_Distance = 2f; // 距星距离效用倍率 (平方后缩放)
        public const float W_Star_Contest = 2f; // 争夺效用倍率 (谁更近)
        public const float W_Star_Density = 3f; // 密集度奖励倍率
        public const float W_Star_SuperStar = 10f; // 超级星总分倍率
        public const float W_Star_EnemyHome = -0.3f; // 敌方距家距离惩罚 (敌方远离家→加分, 负权重→敌方近家减分)
        public const float W_Star_EnemyProximity = -0.3f; // 敌方距星距离惩罚

        // ==================== ApproachEnemy ====================

        public const float W_Appr_Distance = 0.75f; // 距离效用倍率
        public const float W_Appr_AdvantageBoost = 1f; // HP优势时交战意愿平方放大系数

        // ==================== RetreatFromEnemy ====================

        public const float W_Retreat_Score = 0.4f; // 撤退基础效用倍率
        public const float W_Retreat_BaseMul = 0.4f; // 基础乘数 (无劣势时的倍率)
        public const float W_Retreat_DisadvBoost = 1f; // HP劣势时撤退意愿放大系数
        public const float W_Retreat_CenterScale = 1f; // 超级星临近时压制撤退的系数

        // ==================== ControlCenter ====================

        public const float W_Ctrl_Center = 1.5f; // 中心控制效用倍率

        // ==================== FinishEnemy ====================

        public const float W_Finish_Advantage = 1f; // HP优势→追击紧迫度倍率
        public const float W_Finish_EnemyHome = 3f; // 敌人离家近→压低追击的惩罚系数
        public const float W_Finish_Pursuit = 1f; // 我方距敌近→追击便利奖励系数

        // ==================== AvoidAction ====================

        public const float W_Avoid_CenterScale = 0.25f; // 超级星临近时压低躲避的系数
        public const float W_Avoid_EnemyProx = 200f; // 敌人太近时额外惩罚系数 (平方后放大)

        // ==================== BackHome ====================

        public const float W_Home_Recover = 1.2f; // HP恢复需求倍率
        public const float W_Home_Distance = 1f; // 距家距离效用倍率
        public const float W_Home_DistDiv = 200f; // 距离因子归一化除数
        public const float W_Home_DistBase = 0.5f; // 距离因子基础偏移 [0.5, 1.0]
        public const float W_Home_DisadvScore = 60f; // HP劣势→回家紧迫度倍率
        public const float W_Home_CenterScale = 1f; // 超级星临近时劣势方紧迫额外加算
    }
}
