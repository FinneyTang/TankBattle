using Main;
using AI.Blackboard;
using AI.BehaviourTree;
using AI.Base;
using UnityEngine;

namespace lhx
{
	class TurnTurretPredictive : ActionNode
	{
		protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
		{
			Tank t = (Tank)agent;
			Tank oppTank = Match.instance.GetOppositeTank(t.Team);
			if (oppTank != null && oppTank.IsDead == false)
			{
				if (oppTank.Velocity.magnitude >= 3f)
				{
					t.TurretTurnTo(Functions.CalculatePredictivePosition(t.FirePos, oppTank.Position, oppTank.Velocity));
				}
				else
				{
					t.TurretTurnTo(oppTank.Position);
				}
			}
			else
			{
				Vector3 oppHomeZonePos = Match.instance.GetRebornPos(oppTank.Team);
				t.TurretTurnTo(oppHomeZonePos);
			}
			return ERunningStatus.Executing;
		}
	}

	class TurnTurret : ActionNode
	{
		protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
		{
			Tank t = (Tank)agent;
			Tank oppTank = Match.instance.GetOppositeTank(t.Team);
			if (oppTank != null && oppTank.IsDead == false)
			{
				t.TurretTurnTo(oppTank.Position);
			}
			else
			{
				Vector3 oppHomeZonePos = Match.instance.GetRebornPos(oppTank.Team);
				t.TurretTurnTo(oppHomeZonePos);
			}
			return ERunningStatus.Executing;
		}
	}

	class Prefire : ActionNode
	{
		protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
		{
			Tank t = (Tank)agent;
			return t.CanFire();
		}
		protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
		{
			Tank t = (Tank)agent;
			t.Fire();
			return ERunningStatus.Executing;
		}
	}

	class Fire : ActionNode
	{
		protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
		{
			Tank t = (Tank)agent;
			return t.CanFire();
		}
		protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
		{
			Tank t = (Tank)agent;
			t.Fire();
			return ERunningStatus.Executing;
		}
	}

	class BackToHome : ActionNode
	{
		protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
		{
			Tank t = (Tank)agent;
			workingMemory.SetValue((int)EBBKey.MovingTargetPos, Match.instance.GetRebornPos(t.Team));
			return true;
		}
	}

	class GetStarMoveSafe : ActionNode
	{
		protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
		{
			Tank t = (Tank)agent;
			bool hasStar = false;
			float nearestDist = float.MaxValue;
			Vector3 nearestStarPos = Vector3.zero;
			foreach (var pair in Match.instance.GetStars())
			{
				Star s = pair.Value;
				if (s.IsSuperStar)
				{
					hasStar = true;
					nearestStarPos = s.Position;
					break;
				}
				else if(!Functions.Vector3EqualTo(t.NextDestination, s.Position))
				{
					float dist = Functions.CalculatePathLength(t.CaculatePath(s.Position), t.Position, s.Position);
					if (dist < nearestDist)
					{
						hasStar = true;
						nearestDist = dist;
						nearestStarPos = s.Position;
					}
				}
			}
			if (hasStar)
			{
				workingMemory.SetValue((int)EBBKey.MovingTargetPos, nearestStarPos);
			}
			return hasStar;
		}
	}

	class GetStarMove : ActionNode
	{
		protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
		{
			Tank t = (Tank)agent;
			bool hasStar = false;
			float nearestDist = float.MaxValue;
			Vector3 nearestStarPos = Vector3.zero;
			foreach (var pair in Match.instance.GetStars())
			{
				Star s = pair.Value;
				if (s.IsSuperStar)
				{
					hasStar = true;
					nearestStarPos = s.Position;
					break;
				}
				else
				{
					float dist = Functions.CalculatePathLength(t.CaculatePath(s.Position), t.Position, s.Position);
					if (dist < nearestDist)
					{
						hasStar = true;
						nearestDist = dist;
						nearestStarPos = s.Position;
					}
				}
			}
			if (hasStar)
			{
				workingMemory.SetValue((int)EBBKey.MovingTargetPos, nearestStarPos);
			}
			return hasStar;
		}
	}

	class GetSuperStarPremove : ActionNode
	{
		protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
		{
			workingMemory.SetValue((int)EBBKey.MovingTargetPos, new Vector3(0, 0, 0));
			return true;
		}
	}

	// Deprecated
	class RandomMove : ActionNode
	{
		protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
		{
			Vector3 targetPos;
			if (workingMemory.TryGetValue((int)EBBKey.MovingTargetPos, out targetPos))
			{
				Tank t = (Tank)agent;
				if (Vector3.Distance(targetPos, t.Position) >= 1f)
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

	class GetMidMove : ActionNode
	{
		protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
		{
			workingMemory.SetValue((int)EBBKey.MovingTargetPos, new Vector3(0, 0, 0));
			return ERunningStatus.Finished;
		}
	}

	class MoveTo : ActionNode
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

	class Hide : ActionNode
	{
		protected override bool OnEvaluate(IAgent agent, BlackboardMemory workingMemory)
		{
			Tank t = (Tank)agent;
			var missiles = Match.instance.GetOppositeMissiles(t.Team);
			if (missiles.Count == 0)
			{
				return false;
			}
			Missile missile = Functions.GetLatestMissile(missiles, t.Team);
			float dist = (missile.Position - t.Position).magnitude;
			if (dist < 8)
			{
				return false;
			}
			float cos = Vector3.Dot(missile.Velocity, t.Velocity) / missile.Velocity.magnitude / t.Velocity.magnitude;
			if (Mathf.Abs(cos) < 0.5)
			{
				return false;
			}
			return true;
		}
		protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
		{
			Tank t = (Tank)agent;
			var missiles = Match.instance.GetOppositeMissiles(t.Team);
			Missile missile = Functions.GetLatestMissile(missiles, t.Team);
			Vector3 normal = Vector3.Cross(Vector3.up, missile.Velocity).normalized;
			RaycastHit hitInfo1, hitInfo2;
			float hitDistance1 = 0, hitDistance2 = 0;
			if (Physics.Linecast(t.Position, t.Position + normal * 100, out hitInfo1, PhysicsUtils.LayerMaskScene))
			{
				hitDistance1 = hitInfo1.distance;
			}
			if (Physics.Linecast(t.Position, t.Position - normal * 100, out hitInfo2, PhysicsUtils.LayerMaskScene))
			{
				hitDistance2 = hitInfo2.distance;
			}
			if (Mathf.Max(hitDistance1, hitDistance2) < 3)
			{
				return ERunningStatus.Failed;
			}
			if (hitDistance1 > hitDistance2)
			{
				workingMemory.SetValue((int)EBBKey.MovingTargetPos, t.Position + Vector3.ClampMagnitude(normal, hitDistance1 - 1));
				return ERunningStatus.Finished;
			}
			workingMemory.SetValue((int)EBBKey.MovingTargetPos, t.Position - Vector3.ClampMagnitude(normal, hitDistance2 - 1));
			return ERunningStatus.Finished;
		}
	}

	class DoNothing : ActionNode
	{
		protected override ERunningStatus OnExecute(IAgent agent, BlackboardMemory workingMemory)
		{
			return ERunningStatus.Failed;
		}
	}
}
