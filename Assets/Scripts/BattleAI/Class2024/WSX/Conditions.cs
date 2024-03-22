using System.Collections;
using UnityEngine;
using Main;
using AI.Base;
using AI.RuleBased;


namespace WSX
{

    public class HPLessThan : Condition
    {
        private int m_HPLessThan;
        public HPLessThan(int vlaue)
        {
            m_HPLessThan = vlaue;
        }
        public override bool IsTrue(IAgent agent)
        {
            return ((Tank)agent).HP < m_HPLessThan;
        }
    }
    public class HasStar : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            var stars = Match.instance.GetStars().Values;
            
            return stars.Count != 0;
        }
    }
    public class IsTimePass : Condition
    {
        private float m_time;
        public IsTimePass(float value)
        {
            m_time = value;
        }
        public override bool IsTrue(IAgent agent)
        {
            Match match = Match.instance;
            return match.RemainingTime - m_time <= 0f;
        }
    }
    public class IsScoreGreater : Condition
    {
        private Tank enemy;
        public IsScoreGreater(Tank myTank)
        {
            enemy = Match.instance.GetOppositeTank(myTank.Team);
        }
        public override bool IsTrue(IAgent agent)
        {
            return ((Tank)agent).Score > enemy.Score;
        }
    }
    public class IsHPGreater : Condition
    {
        private Tank enemy;
        public IsHPGreater(Tank myTank)
        {
            enemy = Match.instance.GetOppositeTank(myTank.Team);
        }
        public override bool IsTrue(IAgent agent)
        {
            return ((Tank)agent).HP > enemy.HP;
        }
    }
    public class IsEnemyDead : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Match match = Match.instance;
            return match.GetOppositeTank(((Tank)agent).Team).IsDead;
        }
    }
    public class CanSeeEnemy : Condition
    {
        private Tank enemy;
        public CanSeeEnemy(Tank myTank)
        {
            enemy = Match.instance.GetOppositeTank(myTank.Team);
        }
        public override bool IsTrue(IAgent agent)
        {
            return ((Tank)agent).CanSeeOthers(enemy);
        }
    }
}