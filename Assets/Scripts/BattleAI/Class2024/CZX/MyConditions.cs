using AI.Base;
using AI.RuleBased;
using Main;
using UnityEngine;

namespace CZX
{
    public class CanGetEnemy : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            var t       = (Tank)agent;
            var oppTank = Match.instance.GetOppositeTank(t.Team);
            Debug.Log("Get Enemy: " + t.CanSeeOthers(oppTank));
            if (oppTank != null)
            {
                Debug.Log("Get Enemy: " + t.CanSeeOthers(oppTank));
                return t.CanSeeOthers(oppTank);
            }

            return false;
        }
    }
}