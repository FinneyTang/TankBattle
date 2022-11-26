using System.Collections;
using System.Collections.Generic;
using Main;
using UnityEngine;

namespace KXF
{
    public abstract class AICondition
    {
        public abstract bool IsTrue(Tank tank);
    }
    
    public class IsStarExist : AICondition
    {
        public override bool IsTrue(Tank tank)
        {
            Match m = Match.instance;
            return m.GetStars().Count > 0;
        }
    }

    public class IsEnemyDead : AICondition
    {
        public override bool IsTrue(Tank tank)
        {
            Match m = Match.instance;
            return m.GetOppositeTank(tank.Team).HP == 0;
        }
    }

    public class IsHpGreater : AICondition
    {
        private int amount = 0;
        public IsHpGreater(int amount)
        {
            this.amount = amount;
        }
        public override bool IsTrue(Tank tank)
        {
            Match m = Match.instance;
            return tank.HP > (m.GetOppositeTank(tank.Team).HP + amount);
        }
    }

    public class IsSuperStarExistAfterXSec : AICondition
    {
        private float time = 0;

        public IsSuperStarExistAfterXSec(float time)
        {
            this.time = time;
        }

        public override bool IsTrue(Tank tank)
        {
            Match m = Match.instance;
            return Mathf.Abs(m.RemainingTime - ((float)m.GlobalSetting.MatchTime/2 + time)) <= 0.1f;
        }
    }
}