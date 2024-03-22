using System.Collections;
using UnityEngine;
using Main;
using AI.BehaviourTree;
using AI.Base;
using AI.Blackboard;
using AI.RuleBased;

namespace WSX
{
    enum EBBKey
    {
        MovingTargetPos
    }
    public class MoveTo : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            return workingMemory.HasValue((int)EBBKey.MovingTargetPos);
        }
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank t = (Tank)agent;
            t.Move(workingMemory.GetValue<Vector3>((int)EBBKey.MovingTargetPos));
            return ERunningStatus.Finished;
        }
    }
    public class FindEnemy : ActionNode
    {
        private Tank enemy;
        public FindEnemy(Tank myTank)
        {
            enemy = Match.instance.GetOppositeTank(myTank.Team);
        }
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            if (enemy.IsDead)
            {
                return false;
            }
            workingMemory.SetValue((int)EBBKey.MovingTargetPos, enemy.Position);
            return true;
        }
    }
    public class GoToCenter : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            if (workingMemory.TryGetValue((int)EBBKey.MovingTargetPos, out Vector3 targetPos))
            {
                Tank tank = (Tank)agent;
                if (Vector3.Distance(targetPos, tank.Position) >= 1f)
                {
                    return false;
                }
            }
            workingMemory.SetValue((int)EBBKey.MovingTargetPos, Vector3.zero);
            return true;
        }
    }
    public class BackHome : ActionNode
    {
        private Condition m_Precondition = null;
        public new Node SetPrecondition(Condition precondition)
        {
            this.m_Precondition = precondition;
            return this;
        }
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            if (m_Precondition == null || m_Precondition.IsTrue(agent))
            {
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, Match.instance.GetRebornPos(tank.Team));
                return true;
            }
            return false;
        }
    }
    public class RandomMove : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            if (workingMemory.TryGetValue((int)EBBKey.MovingTargetPos, out Vector3 targetPos))
            {
                Tank tank = (Tank)agent;
                if (Vector3.Distance(targetPos, tank.Position) >= 1f)
                {
                    return false;
                }
            }
            workingMemory.SetValue((int)EBBKey.MovingTargetPos, GetNextDestination());
            return true;
        }
        private Vector3 GetNextDestination()
        {
            float halfSize = Match.instance.FieldSize * 0.5f;
            return new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize));
        }
    }
    public class CollectStar : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            Tank tank = (Tank)agent;
            bool hasStar = false;
            float nearestStarDistance = float.MaxValue;
            float ShortestPathLength = float.MaxValue;
            Vector3 nearestStarPos = Vector3.zero;
            foreach (var pair in Match.instance.GetStars())
            {
                Star star = pair.Value;
                if (star.IsSuperStar)
                {
                    hasStar = true;
                    nearestStarPos = star.Position;
                    break;
                }
                else
                {
                    var path = tank.CaculatePath(star.Position);
                    float pathLength = GetPathLength(path);
                    if (pathLength != 0 && pathLength < ShortestPathLength)
                    {
                        hasStar = true;
                        ShortestPathLength = pathLength;
                        nearestStarPos = star.Position;
                    }
                    //float distance = (star.Position - tank.Position).sqrMagnitude;
                    //if (distance < nearestStarDistance)
                    //{
                    //    hasStar = true;
                    //    nearestStarDistance = distance;
                    //    nearestStarPos = star.Position;
                    //}
                }
            }
            if (hasStar)
            {
                Debug.Log($"最近的星星位置 {nearestStarPos}");
                workingMemory.SetValue((int)EBBKey.MovingTargetPos, nearestStarPos);
            }
            return hasStar;
        }
        private float GetPathLength(UnityEngine.AI.NavMeshPath path)
        {
            if (path.corners.Length < 2 )
            {
                Debug.LogWarning("path.cornors.Length < 2 !");
                return 0f;
            }
            float LengthSoFar = 0;
            Vector3 priviousCornor = path.corners[0];
            for (int i = 1; i < path.corners.Length; i++)
            {
                Vector3 currentCornor = path.corners[i];
                LengthSoFar += Vector3.Distance(priviousCornor, currentCornor);
                priviousCornor = currentCornor;
            }
            return LengthSoFar;
        }
    }
    public class TurnTurret : ActionNode
    {
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemroy)
        {
            Tank tank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(tank.Team);
            if (oppTank != null && oppTank.IsDead == false)
            {
                tank.TurretTurnTo(oppTank.Position);
            }
            else
            {
                tank.TurretTurnTo(oppTank.Position + 7 * oppTank.Velocity);
            }
            return ERunningStatus.Executing;
        }
    }
    public class Fire : ActionNode
    {
        protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
        {
            return ((Tank)agent).CanFire();
        }
        protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
        {
            ((Tank)agent).Fire();
            return ERunningStatus.Executing;
        }
    }
}