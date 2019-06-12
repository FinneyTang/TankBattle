using Main;
using AI.Blackboard;
using AI.BehaviourTree;
using AI.RuleBased;

namespace lhx
{
    public enum EBBKey
    {
        MovingTargetPos,
		// TODO Modify in function Missile.Update, which is read-only
		PrefireSuccessRate,
		HideRate,
    }
    class MyTank : Tank
    {
        private BlackboardMemory m_WorkingMemory;
        private Node m_BTNode;
		protected override void OnStart()
        {
            base.OnStart();
            m_WorkingMemory = new BlackboardMemory();
			m_BTNode = // Behavior Tree
				new ParallelNode(1).AddChild(
					new WeightedSelectorNode(EBBKey.PrefireSuccessRate).AddChild(
						new TurnTurretPredictive(),
						new TurnTurret()),
					new WeightedSelectorNode(EBBKey.PrefireSuccessRate).AddChild(
						new Prefire().SetPrecondition(
							new OrCondition(
								new ConditionEnemyIsComingSoon(),
								new OrCondition(
									new ConditionCanSeeEnemy(),
									new ConditionCanAttackEnemy()))),
						new Fire().SetPrecondition(new ConditionCanSeeEnemy())),
					new SequenceNode().AddChild(
						new SelectorNode().AddChild(
							new GetSuperStarPremove().SetPrecondition(new ConditionSuperStarIsComing()),
							new GetStarMove().SetPrecondition(new ConditionStarIsVeryClose()),
							new BackToHome().SetPrecondition(
								new OrCondition(
									new AndCondition(
										new ConditionMyHpIsLow(),
										new ConditionEnemyGetTheSameMove()),
									new OrCondition(
										new AndCondition(
											new NotCondition(new ConditionHpFull()),
											new ConditionHomeIsVeryClose()),
										new AndCondition(
											new ConditionHpBelowHalf(),
											new ConditionHomeIsClose())))),
							new GetStarMove().SetPrecondition(
								new OrCondition(
									new AndCondition(
										new NotCondition(new ConditionHpBelowHalf()),
										new ConditionStarIsClose()),
									new OrCondition(
										new AndCondition(
											new NotCondition(new ConditionEnemyAlive()),
											new NotCondition(new ConditionEnemyIsComingSoon())),
										new OrCondition(
											new ConditionEnemyHpIsLow(),
											new ConditionEnemyGoHome())))),
							new BackToHome().SetPrecondition(
								new OrCondition(
									new AndCondition(
										new ConditionGoodGame(),
										new ConditionHpBelowHalf()),
									new OrCondition(
										new AndCondition(
											new NotCondition(new ConditionHpFull()),
											new ConditionNoEnmeyAndNoStar()),
										new ConditionHpBelowOneHit()))),
							new SelectorNode().AddChild(
								new GetStarMoveSafe().SetPrecondition(
									new AndCondition(
										new NotCondition(new ConditionEnemyGetTheSameMove()),
										new ConditionGoodGame())),
								new GetStarMove()),
							new GetMidMove()),
						new SelectorNode().AddChild(
							new WeightedSelectorNode(EBBKey.HideRate).AddChild(
								// Ineffective
								new Hide(),//.SetPrecondition(new FalseCondition()),
								new DoNothing()),
							new MoveTo())));
        }
        protected override void OnUpdate()
        {
            BehaviourTreeRunner.Exec(m_BTNode, this, m_WorkingMemory);
        }
		public override string GetName()
        {
            return "lhx";
        }
    }
}
