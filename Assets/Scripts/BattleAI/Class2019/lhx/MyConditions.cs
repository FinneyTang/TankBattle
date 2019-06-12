using Main;
using AI.Base;
using AI.RuleBased;
using UnityEngine;

namespace lhx
{
	class ConditionGoodGame : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			Tank t = (Tank)agent;
			Tank oppTank = Match.instance.GetOppositeTank(t.Team);
			return t.Score >= (oppTank.Score + 30);
		}
	}

	class ConditionCanAttackEnemy : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			RaycastHit hitInfo;
			Tank t = (Tank)agent;
			Tank oppTank = Match.instance.GetOppositeTank(t.Team);
			Vector3 targetPos;
			if (oppTank != null && oppTank.IsDead == false)
			{
				if (oppTank.Velocity.magnitude >= 3f)
				{
					targetPos = Functions.CalculatePredictivePosition(t.FirePos, oppTank.Position, oppTank.Velocity);
				}
				else
				{
					targetPos = oppTank.Position;
				}
			}
			else
			{
				targetPos = Match.instance.GetRebornPos(oppTank.Team);
			}
			if (Physics.Linecast(t.FirePos, targetPos, out hitInfo, PhysicsUtils.LayerMaskScene))
			{
				return false;
			}
			Vector3 ray = targetPos - t.FirePos;
			Vector3 delta = Vector3.ClampMagnitude(Quaternion.AngleAxis(90, Vector3.up) * ray, 0.5f);
			if (Physics.Linecast(t.FirePos, targetPos + delta, out hitInfo, PhysicsUtils.LayerMaskScene))
			{
				return false;
			}
			delta = Vector3.ClampMagnitude(Quaternion.AngleAxis(-90, Vector3.up) * ray, 0.5f);
			if (Physics.Linecast(t.FirePos, targetPos + delta, out hitInfo, PhysicsUtils.LayerMaskScene))
			{
				return false;
			}
			return true;
		}
	}

	class ConditionCanSeeEnemy : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			Tank t = (Tank)agent;
			Tank oppTank = Match.instance.GetOppositeTank(t.Team);
			if (oppTank != null)
			{
				return t.CanSeeOthers(oppTank);
			}
			return false;
		}
	}

	class ConditionHpFull : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			Tank t = (Tank)agent;
			return t.HP == Match.instance.GlobalSetting.MaxHP;
		}
	}

	class ConditionHpBelowHalf : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			Tank t = (Tank)agent;
			return t.HP <= (Match.instance.GlobalSetting.MaxHP / 2);
		}
	}

	class ConditionHpBelowOneHit : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			Tank t = (Tank)agent;
			return t.HP <= Match.instance.GlobalSetting.DamagePerHit;
		}
	}

	class ConditionHaveSuperStar : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			foreach (var pair in Match.instance.GetStars())
			{
				Star s = pair.Value;
				if (s.IsSuperStar)
				{
					return true;
				}
			}
			return false;
		}
	}

	class ConditionEnemyAlive : Condition
	{
		public override bool IsTrue(IAgent agent)
		{

			Tank t = (Tank)agent;
			Tank oppTank = Match.instance.GetOppositeTank(t.Team);
			return oppTank != null && oppTank.IsDead == false;
		}
	}

	class ConditionEnemyIsComingSoon : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			Tank t = (Tank)agent;
			Tank oppTank = Match.instance.GetOppositeTank(t.Team);
			return oppTank != null
				&& oppTank.IsDead
				&& oppTank.GetRebornCD(Time.time) < (Match.instance.GlobalSetting.RebonCD / 3);
		}
	}

	class ConditionMyHpIsLow : Condition
	{
		public override bool IsTrue(IAgent agent)
		{

			Tank t = (Tank)agent;
			Tank oppTank = Match.instance.GetOppositeTank(t.Team);
			return oppTank != null && ((t.HP / Match.instance.GlobalSetting.DamagePerHit) < (oppTank.HP / Match.instance.GlobalSetting.DamagePerHit));
		}
	}

	class ConditionEnemyHpIsLow : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			Tank t = (Tank)agent;
			Tank oppTank = Match.instance.GetOppositeTank(t.Team);
			return oppTank != null && oppTank.IsDead == false && ((t.HP / Match.instance.GlobalSetting.DamagePerHit) > (oppTank.HP / Match.instance.GlobalSetting.DamagePerHit));
		}
	}

	class ConditionEnemyGoHome : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			Tank t = (Tank)agent;
			Tank oppTank = Match.instance.GetOppositeTank(t.Team);
			if (Functions.Vector3EqualTo(oppTank.NextDestination, Match.instance.GetRebornPos(oppTank.Team)))
			{
				return true;
			}
			return false;
		}
	}

	class ConditionEnemyGetTheStarMove : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			Tank t = (Tank)agent;
			Tank oppTank = Match.instance.GetOppositeTank(t.Team);
			foreach (var pair in Match.instance.GetStars())
			{
				Star s = pair.Value;
				if (Functions.Vector3EqualTo(oppTank.NextDestination, s.Position))
				{
					return true;
				}
			}
			return false;
		}
	}

	class ConditionEnemyGetTheSameMove : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			Tank t = (Tank)agent;
			Tank oppTank = Match.instance.GetOppositeTank(t.Team);
			if (Functions.Vector3EqualTo(oppTank.NextDestination, t.NextDestination))
			{
				return true;
			}
			return false;
		}
	}

	class ConditionSuperStarIsComing : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			Tank t = (Tank)agent;
			float dist = Functions.CalculatePathLength(t.CaculatePath(new Vector3(0, 0, 0)), t.Position, new Vector3(0, 0, 0));
			float speed = 10f;
			float time = dist / speed + 2 + dist / 30;
			float delta = Match.instance.RemainingTime - Match.instance.GlobalSetting.MatchTime / 2;
			return (delta > 0) && (delta <= time);
		}
	}

	class ConditionStarIsClose : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			Tank t = (Tank)agent;
			foreach (var pair in Match.instance.GetStars())
			{
				Star s = pair.Value;
				float dist = Functions.CalculatePathLength(t.CaculatePath(s.Position), t.Position, s.Position);
				if (dist <= (PhysicsUtils.MaxFieldSize * 0.05 * PhysicsUtils.MaxFieldSize * 0.05))
				{
					return true;
				}
			}
			return false;
		}
	}

	class ConditionStarIsVeryClose : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			Tank t = (Tank)agent;
			foreach (var pair in Match.instance.GetStars())
			{
				Star s = pair.Value;
				float dist = Functions.CalculatePathLength(t.CaculatePath(s.Position), t.Position, s.Position);
				if (dist <= (PhysicsUtils.MaxFieldSize * 0.03 * PhysicsUtils.MaxFieldSize * 0.03))
				{
					return true;
				}
			}
			return false;
		}
	}

	class ConditionHomeIsClose : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			Tank t = (Tank)agent;
			float dist = (Match.instance.GetRebornPos(t.Team) - t.Position).sqrMagnitude;
			return dist <= (PhysicsUtils.MaxFieldSize * 0.07 * PhysicsUtils.MaxFieldSize * 0.07);
		}
	}

	class ConditionHomeIsVeryClose : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			Tank t = (Tank)agent;
			float dist = (Match.instance.GetRebornPos(t.Team) - t.Position).sqrMagnitude;
			return dist <= (PhysicsUtils.MaxFieldSize * 0.05 * PhysicsUtils.MaxFieldSize * 0.05);
		}
	}

	class ConditionNoEnmeyAndNoStar : Condition
	{
		public override bool IsTrue(IAgent agent)
		{
			Tank t = (Tank)agent;
			Tank oppTank = Match.instance.GetOppositeTank(t.Team);
			if (oppTank != null && oppTank.IsDead == false)
				return false;
			return Match.instance.GetStars().Count == 0;
		}
	}
}
