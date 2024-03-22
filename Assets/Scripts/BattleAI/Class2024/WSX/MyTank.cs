using Main;
using AI.RuleBased;
using AI.Blackboard;
using AI.BehaviourTree;
using System;
using System.Collections.Generic;
using UnityEngine;
using AI.Base;

namespace WSX
{
    public class MyTank : Tank
    {
        private Tank m_enemy;
        private BlackboardMemory m_WorkingMemory;
        private Node m_BTNode;
        private readonly float Middle_Match = (float)Match.instance.GlobalSetting.MatchTime / 2f;

        // 
        private Condition m_HPBelow_40;
        private Condition m_HPBelow_60;
        private Condition m_HPBelow_100;
        private Condition m_EnemyDead;
        private Condition m_ScoreGreater;
        private Condition m_HPGreater;
        private Condition m_HasStar;
        private ActionNode m_Move;
        private ActionNode m_TurnTurret;
        private ActionNode m_CollectStar;

        private void KnowledgeLayer()
        {
            
        }

        private void StrategyLayerAndBehaviourLayer()
        {
            BehaviourTreeRunner.Exec(m_BTNode, this, m_WorkingMemory);
        }

        protected override void OnStart()
        {
            m_HPBelow_40 = new HPLessThan(40);
            m_HPBelow_60 = new HPLessThan(60);
            m_HPBelow_100 = new HPLessThan(100);
            m_EnemyDead = new IsEnemyDead();
            m_ScoreGreater = new IsScoreGreater(this);
            m_HPGreater = new IsHPGreater(this);
            m_HasStar = new HasStar();
            m_Move = new MoveTo();
            m_TurnTurret = new TurnTurret();
            m_CollectStar = new CollectStar();

            base.OnStart();
            m_enemy = Match.instance.GetOppositeTank(Team);
            m_WorkingMemory = new BlackboardMemory();
            
            m_BTNode = new ParallelNode(1).AddChild(
                            new TurnTurret(),
                            new Fire().SetPrecondition(new CanSeeEnemy(this)),
                            new SelectorNode().AddChild(
                                new SequenceNode().SetPrecondition(new NotCondition(new IsTimePass(Middle_Match + 10))).AddChild(
                                    new SelectorNode().AddChild(
                                        new BackHome().SetPrecondition(new AndCondition( m_EnemyDead, m_HPBelow_60 )),
                                        new BackHome().SetPrecondition(new AndCondition( new NotCondition( m_HasStar ), m_HPBelow_100 )),
                                        new BackHome().SetPrecondition( m_HPBelow_40 ),
                                        m_CollectStar,
                                        new RandomMove()
                                    ), 
                                    m_Move
                                    
                                ),  // 前期
                                new SequenceNode().SetPrecondition(new NotCondition(new IsTimePass(Middle_Match - 5))).AddChild(
                                    new SelectorNode().AddChild(
                                        new BackHome().SetPrecondition( m_HPBelow_60 ),
                                        new GoToCenter(),
                                        m_CollectStar
                                    ),
                                    m_Move
                                    
                                ),    // 中期
                                new SequenceNode().AddChild(
                                    new SelectorNode().AddChild(
                                        new SelectorNode().SetPrecondition( m_ScoreGreater ).AddChild(
                                            new BackHome().SetPrecondition( m_HPBelow_60 ),
                                            m_CollectStar
                                        ),
                                        new SelectorNode().AddChild(
                                            new FindEnemy(this).SetPrecondition( m_HPGreater ),
                                            new BackHome().SetPrecondition( m_HPBelow_60 ),
                                            m_CollectStar
                                        )
                                    ),
                                    m_Move
                                    
                                )    // 后期
                            )
                           
                        );
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            KnowledgeLayer();
            StrategyLayerAndBehaviourLayer();
            
        }

        public override string GetName()
        {
            return "WSX";
        }

    }
}

