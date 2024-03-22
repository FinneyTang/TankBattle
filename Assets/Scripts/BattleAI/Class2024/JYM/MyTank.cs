using Main;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace JYM
{
	public class MyTank : Tank
	{
		KnowledgePool knowledgePool;

		Request curruntRequest;
		Match match => Match.instance;

		#region Parameters

		#region HP
		const int returnHP = 20;
		const int enemyDeadReturnHP = 40;
		const int enemyDeatAndNoStarReturnHP = 60;
		const int nearHomeReturnHomeHP = 60;
		const int stayHomeHP = 75;
		#endregion
		#region Distance
		const float returningFindStarDis = 17;
		const float returnDis = 50;
		const float returnIgnoreDis = 15;
		const float avoidDetectDis = 45;
		const float ignoreAvoidDis = 23;
		const float nearEnemyRebornPosDis = 20;
		const float tankDamageRadius = 4;
		const float areaRadius = 6;
		#endregion
		#region Time
		const float superstarCompeteTime = 8;
		const float superstarReturnTime = 22;
		#endregion
		#region Offset
		const float rangeDetectOffset = 0.25f;
		#endregion
		#region Avoid
		const float avoidDetectTime = 1.5f;
		const float avoidDetectInterval = 0.05f;
		const float ignoreAvoidAngle = 25;
		const int avoidMinStep = 8;
		const int avoidMaxStep = 14;
		const int avoidStopSpeed = 6;
		#endregion
		#region FirePredict
		const int predictMaxDis = 20;
		const float predictTime = 7;
		const float predictInterval = 0.1f;
		const float predictdeviantDis = 2f;
		#endregion

		#endregion

		protected override void OnAwake()
		{
			base.OnAwake();
			knowledgePool = new KnowledgePool();
		}

		protected override void OnStart()
		{
			base.OnStart();
			OnKnowledgeStart();
		}

		protected override void OnUpdate()
		{
			base.OnUpdate();
			if (!IsDead)
			{
				OnKnowledgeUpdate();
				OnStratedgyUpdate();
				OnBehaviorUpdate();
			}
			//print(curruntRequest?.requestType);
		}

		public override string GetName()
		{
			return "MICCMIDC";
		}


		#region KnowledgeLayer
		private void OnKnowledgeStart()
		{
			knowledgePool.enemyTank = match.GetOppositeTank(Team);
			knowledgePool.rebornPos = match.GetRebornPos(Team);
			knowledgePool.enemyRebornPos = match.GetRebornPos(knowledgePool.enemyTank.Team);
			knowledgePool.middlePos = Vector3.zero;
			knowledgePool.idlePos = knowledgePool.rebornPos + ((knowledgePool.middlePos - knowledgePool.rebornPos) / 2) + Vector3.Project((knowledgePool.middlePos - knowledgePool.rebornPos), Vector3.forward).normalized * 30;
			knowledgePool.stars = match.GetStars();
			knowledgePool.superstarAppearTime = match.GlobalSetting.MatchTime / 2;
			knowledgePool.homeZoneRadius = match.GlobalSetting.HomeZoneRadius;
			knowledgePool.damagePerHit = match.GlobalSetting.DamagePerHit;
			knowledgePool.missileSpeed = match.GlobalSetting.MissileSpeed;
		}

		private void OnKnowledgeUpdate()
		{
			knowledgePool.remainingTime = match.RemainingTime;
			knowledgePool.turretAiming = TurretAiming;
			if (knowledgePool.missiles == null) knowledgePool.missiles = match.GetOppositeMissiles(Team);

			bool isSuperStarDetected = false;
			foreach (KeyValuePair<int, Star> kvp in knowledgePool.stars)
			{
				if (kvp.Value.IsSuperStar)
				{
					isSuperStarDetected = true;
					knowledgePool.isSuperstarExist = true;
					break;
				}
			}
			if (!isSuperStarDetected) knowledgePool.isSuperstarExist = false;
		}
		#endregion
		#region StratedgyLayer

		private void OnStratedgyUpdate()
		{
			//发射部分与炮口转向部分
			Vector3 simulatedEnemyPos = GetAimOffset(knowledgePool.enemyTank);
			if (knowledgePool.enemyTank.IsDead)
			{
				SendRequest(new TurnFireRequest(knowledgePool.enemyRebornPos));
			}
			else
			{
				SendRequest(new TurnFireRequest(simulatedEnemyPos));
			}

			if (CanFire()
				&& CanSeePos(simulatedEnemyPos, true)
				&& !knowledgePool.enemyTank.IsDead
				&& Vector3.Angle(knowledgePool.turretAiming, simulatedEnemyPos - Position) < GetAimOffsetAngle(knowledgePool.enemyTank))
			{
				SendRequest(new FireRequest());
			}


			//其余指令的逻辑判断
			Missile dangerMissile;
			Vector3 dangerPosition;
			if (MissleAvoidDetect(knowledgePool.missiles, out dangerMissile, out dangerPosition) && !(CaculateHitCnt(this) >= 2 && CaculateRoadDistance(knowledgePool.rebornPos, this) < returnIgnoreDis))
			{
				Vector3 avoidPos = GetAvoid(dangerMissile, dangerPosition);
				SendRequest(new AvoidRequest(avoidPos, dangerMissile));
			}
			else if (HP <= returnHP)
			{
				if (knowledgePool.isSuperstarExist || TimeJudge(knowledgePool.superstarAppearTime + superstarCompeteTime, knowledgePool.superstarAppearTime))
				{
					SendRequest(new CompeteRequest(knowledgePool.middlePos));
				}
				else
				{
					SendRequest(new ReturnRequest(knowledgePool.rebornPos));
				}
			}
			else if (HP < stayHomeHP && Vector3.Distance(Position, knowledgePool.rebornPos) <= knowledgePool.homeZoneRadius)
			{
				SendRequest(new IdleRequest());
			}
			else if (TimeJudge(knowledgePool.superstarAppearTime + superstarCompeteTime, knowledgePool.superstarAppearTime))
			{
				SendRequest(new CompeteRequest(knowledgePool.middlePos));
			}
			else if (TimeJudge(knowledgePool.superstarAppearTime + superstarReturnTime, knowledgePool.superstarAppearTime + superstarCompeteTime) && CaculateHitCnt(this) != 5)
			{
				SendRequest(new ReturnRequest(knowledgePool.rebornPos));
			}
			else if (knowledgePool.enemyTank.IsDead && (HP <= enemyDeadReturnHP || (knowledgePool.stars.Count == 0 && HP <= enemyDeatAndNoStarReturnHP)))
			{
				float roadDis;
				Star nearestStar = GetNearestStar(knowledgePool.stars, out roadDis);
				if (roadDis < returningFindStarDis)
				{
					SendRequest(new ReturnRequest(nearestStar.Position));
				}
				else
				{
					SendRequest(new ReturnRequest(knowledgePool.rebornPos));
				}

			}
			else if ((CaculateRoadDistance(knowledgePool.rebornPos, this) < returnDis || Vector3.Distance(knowledgePool.idlePos, Position) < areaRadius) && Vector3.Distance(knowledgePool.rebornPos, Position) > knowledgePool.homeZoneRadius && (HP <= nearHomeReturnHomeHP || CaculateHitCnt(this) < CaculateHitCnt(knowledgePool.enemyTank)))
			{
				float roadDis;
				Star nearestStar = GetNearestStar(knowledgePool.stars, out roadDis);
				if (roadDis < returningFindStarDis)
				{
					SendRequest(new ReturnRequest(nearestStar.Position));
				}
				else
				{
					SendRequest(new ReturnRequest(knowledgePool.rebornPos));
				}
			}
			else if (knowledgePool.stars.Count != 0)
			{

				Star aimStar = GetNearestStar(knowledgePool.stars, knowledgePool.enemyTank);
				if (aimStar == null)
				{
					SendRequest(new MoveRequest(knowledgePool.idlePos));
				}
				else
				{
					SendRequest(new MoveRequest(aimStar.Position));
				}
			}
			else
			{
				SendRequest(new MoveRequest(knowledgePool.idlePos));
			}
		}

		/// <summary>
		/// 事件判断函数，以知识池的比赛剩余时间进行判断。
		/// </summary>
		/// <param name="startTime">开始时间，由于倒计时制值较大</param>
		/// <param name="endTime">结束时间，由于倒计时制值较小。</param>
		/// <returns></returns>
		private bool TimeJudge(float startTime, float endTime)
		{
			float curruntTime = knowledgePool.remainingTime;
			if (curruntTime < endTime || curruntTime > startTime) return false;
			return true;
		}

		private int CaculateHitCnt(Tank tank)
		{
			int hitCnt = 0;
			for (int i = 0; i < 5; i++)
			{
				if (tank.HP > i * knowledgePool.damagePerHit) hitCnt++;
			}
			return hitCnt;
		}

		private float CaculateRoadDistance(Vector3 aimPos, Tank tank)
		{
			NavMeshPath path;
			path = tank.CaculatePath(aimPos);
			float pathLength = 0;

			pathLength += Vector3.Distance(tank.Position, path.corners[0]);

			for (int i = 0; i < path.corners.Length - 1; i++)
			{
				pathLength += Vector3.Distance(path.corners[i], path.corners[i + 1]);
			}
			return pathLength;
		}

		private Star GetNearestStar(Dictionary<int, Star> stars, Tank enemyTank)
		{

			int nearestStarIndex = -1;
			float nearestPathLength = Mathf.Infinity;
			int enemyNearestStarIndex = -1;
			float enemyNearestPathLength = Mathf.Infinity;
			float pathLength;

			if (stars.Count <= 0) return null;

			//计算离敌人最近的星星的下标与距离
			if (!enemyTank.IsDead)
			{
				foreach (KeyValuePair<int, Star> kvp in stars)
				{
					pathLength = CaculateRoadDistance(kvp.Value.Position, enemyTank);

					if (pathLength < enemyNearestPathLength)
					{
						enemyNearestStarIndex = kvp.Key;
						enemyNearestPathLength = pathLength;
					}
				}
			}

			//计算离我最近的星星下标与距离
			foreach (KeyValuePair<int, Star> kvp in stars)
			{
				if (kvp.Value.IsSuperStar)
				{
					nearestStarIndex = kvp.Key;
					return stars[nearestStarIndex];
				}

				pathLength = CaculateRoadDistance(kvp.Value.Position, this);

				if (pathLength < nearestPathLength)
				{
					nearestStarIndex = kvp.Key;
					nearestPathLength = pathLength;
				}
			}

			//如果离我和敌人最近的星星是同一个并且敌人距离星星更近
			if (nearestStarIndex == enemyNearestStarIndex && nearestPathLength > enemyNearestPathLength)
			{
				if (stars.Count == 1)
				{
					if (Vector3.Distance(stars[nearestStarIndex].Position, knowledgePool.enemyRebornPos) < nearEnemyRebornPosDis
						|| HP < knowledgePool.enemyTank.HP + knowledgePool.damagePerHit)
					{
						return null;
					}
					else
					{
						return stars[nearestStarIndex];
					}
				}
				nearestPathLength = Mathf.Infinity;

				//寻找另一个最近的星星
				foreach (KeyValuePair<int, Star> kvp in stars)
				{
					if (kvp.Key == enemyNearestStarIndex) continue;

					pathLength = CaculateRoadDistance(kvp.Value.Position, this);

					if (pathLength < nearestPathLength)
					{
						nearestStarIndex = kvp.Key;
						nearestPathLength = pathLength;
					}
				}
			}
			return stars[nearestStarIndex];
		}

		private Star GetNearestStar(Dictionary<int, Star> stars, out float nearestPathLength)
		{
			int nearestStarIndex = -1;
			nearestPathLength = Mathf.Infinity;
			float pathLength;

			if (stars.Count <= 0) return null;

			//计算离我最近的星星下标与距离
			foreach (KeyValuePair<int, Star> kvp in stars)
			{
				if (kvp.Value.IsSuperStar)
				{
					nearestStarIndex = kvp.Key;
					return stars[nearestStarIndex];
				}

				pathLength = CaculateRoadDistance(kvp.Value.Position, this);

				if (pathLength < nearestPathLength)
				{
					nearestStarIndex = kvp.Key;
					nearestPathLength = pathLength;
				}
			}
			return stars[nearestStarIndex];
		}

		/// <summary>
		/// 使用循环的离散检测接下来是否会被击中。
		/// </summary>
		/// <param name="missiles">导弹映射，包含当前场上所有导弹</param>
		/// <param name="dangerMissile">如果将被某个导弹击中，返回此导弹</param>
		/// <returns>返回是否会被击中</returns>
		private bool MissleAvoidDetect(Dictionary<int, Missile> missiles, out Missile dangerMissile, out Vector3 dangerPos)
		{
			if (missiles == null || missiles.Count == 0)
			{
				dangerMissile = null;
				dangerPos = Vector3.zero;
				return false;
			}
			Missile missile;
			float distance;
			foreach (KeyValuePair<int, Missile> kvp in missiles)
			{
				missile = kvp.Value;
				distance = Vector3.Distance(Position, missile.Position);
				if (distance >= avoidDetectDis) continue;
				else
				{
					int loopCnt = (int)(avoidDetectTime / avoidDetectInterval);
					for (int step = 0; step <= loopCnt; step++)
					{
						if (Vector3.Distance(missile.Position + (missile.Velocity * avoidDetectInterval * step), Position + (Velocity * avoidDetectInterval * step)) < tankDamageRadius
							&& CanSeePos(missile.Position, Position + (Velocity * avoidDetectInterval * step), false))
						{
							dangerPos = Position + (Velocity * avoidDetectInterval * step);
							dangerMissile = missile;
							return true;
						}
					}
				}
			}
			dangerMissile = null;
			dangerPos = Vector3.zero;
			return false;
		}

		private Vector3 GetAvoid(Missile dangerMissile, Vector3 dangerPosition)
		{
			if (Vector3.Distance(knowledgePool.rebornPos, Position) < match.GlobalSetting.HomeZoneRadius || Vector3.Distance(Position, dangerMissile.Position) <= ignoreAvoidDis)
			{
				return NextDestination;
			}

			if (Velocity.magnitude <= avoidStopSpeed)
			{
				bool isStopAvoid = true;
				int loopCnt = (int)(avoidDetectTime / avoidDetectInterval);
				for (int step = 0; step <= loopCnt; step++)
				{
					if (Vector3.Distance(dangerMissile.Position + (dangerMissile.Velocity * avoidDetectInterval * step), Position) <= tankDamageRadius)
					{
						isStopAvoid = false;
						break;
					}
				}
				if (isStopAvoid) return Position;
			}


			Vector3 safePos;
			Vector3 avoidDir = Vector3.Cross(Vector3.Cross(Velocity.normalized, (dangerMissile.Position - dangerPosition).normalized).y > 0.1 ? Vector3.down : Vector3.up, dangerMissile.Velocity).normalized;

			for (int step = avoidMinStep; step <= avoidMaxStep; step++)
			{
				safePos = avoidDir * step + dangerPosition;
				if (CaculatePath(safePos) != null && CanSeePos(safePos, true))
				{
					return safePos;
				}
			}

			avoidDir = Vector3.Cross(Vector3.Cross(Velocity.normalized, (dangerMissile.Position - dangerPosition).normalized).y <= 0.1 ? Vector3.down : Vector3.up, dangerMissile.Velocity).normalized;

			for (int step = avoidMinStep; step <= avoidMaxStep; step++)
			{
				safePos = avoidDir * step + dangerPosition;
				if (CaculatePath(safePos) != null && CanSeePos(safePos, true))
				{
					return safePos;
				}
			}

			return NextDestination;
		}

		private Vector3 GetAimOffset(Tank enemy)
		{
			Vector3 simulatedAim = enemy.Position;
			int loopCnt = (int)(predictTime / predictInterval);

			for (int i = 0; i < predictMaxDis; i++)
			{
				simulatedAim += enemy.Forward;
				for (int step = 0; step < loopCnt; step++)
				{
					if (Vector3.Distance(Position + (Vector3.Normalize(simulatedAim - Position) * knowledgePool.missileSpeed * predictInterval * step), enemy.Position + (enemy.Velocity * predictInterval * (step + 1))) <= predictdeviantDis)
					{
						return simulatedAim;
					}
				}
			}

			simulatedAim = enemy.Position;

			for (int i = 0; i < predictMaxDis; i++)
			{
				simulatedAim -= enemy.Forward;
				for (int j = 0; j < loopCnt; j++)
				{
					if (Vector3.Distance(Position + (Vector3.Normalize(simulatedAim - Position) * knowledgePool.missileSpeed * predictInterval * j), enemy.Position + (enemy.Velocity * predictInterval * (j + 1))) <= predictdeviantDis)
					{
						return simulatedAim;
					}
				}
			}
			return enemy.Position;
		}

		private float GetAimOffsetAngle(Tank enemy)
		{
			float disWithEnemy = Vector3.Distance(enemy.Position, Position);
			if (disWithEnemy > 50) return 0.5f;
			else if (disWithEnemy > 40) return 0.7f;
			else if (disWithEnemy > 30) return 1f;
			else if (disWithEnemy > 20) return 2f;
			else if (disWithEnemy > 10) return 5f;
			else if (disWithEnemy > 5) return 10f;
			else return 20f;
		}

		/// <summary>
		/// 从我的位置看过去，是否能看见某个位置
		/// </summary>
		/// <param name="targetPos">目标位置</param>
		/// <param name="isRangeDetect">是不是范围检测，如果是范围检测，探测将变为一个细细的圆柱，如果不是就是一根线</param>
		/// <returns>返回是否能够看到</returns>
		private bool CanSeePos(Vector3 targetPos, bool isRangeDetect)
		{
			if (isRangeDetect)
			{
				return (!Physics.Linecast(Position, targetPos, PhysicsUtils.LayerMaskScene)
				 && !Physics.Linecast(Position, targetPos + new Vector3(rangeDetectOffset, 0, 0), PhysicsUtils.LayerMaskScene)
				 && !Physics.Linecast(Position, targetPos + new Vector3(-rangeDetectOffset, 0, 0), PhysicsUtils.LayerMaskScene)
				 && !Physics.Linecast(Position, targetPos + new Vector3(0, 0, rangeDetectOffset), PhysicsUtils.LayerMaskScene)
				 && !Physics.Linecast(Position, targetPos + new Vector3(0, 0, rangeDetectOffset), PhysicsUtils.LayerMaskScene));
			}
			else
			{
				return !Physics.Linecast(Position, targetPos, PhysicsUtils.LayerMaskScene);
			}

		}

		/// <summary>
		/// 从起点位置看过去，是否能看到目标位置
		/// </summary>
		/// <param name="from">起点位置</param>
		/// <param name="to">终点位置</param>
		/// <param name="isRangeDetect">是不是范围检测，如果是范围检测，探测将变为一个细细的圆柱，如果不是就是一根线。</param>
		/// <returns>返回是否能够看到</returns>
		private bool CanSeePos(Vector3 from, Vector3 to, bool isRangeDetect)
		{
			if (isRangeDetect)
			{
				return (!Physics.Linecast(from, to, PhysicsUtils.LayerMaskScene)
				 && !Physics.Linecast(from, to + new Vector3(rangeDetectOffset, 0, 0), PhysicsUtils.LayerMaskScene)
				 && !Physics.Linecast(from, to + new Vector3(-rangeDetectOffset, 0, 0), PhysicsUtils.LayerMaskScene)
				 && !Physics.Linecast(from, to + new Vector3(0, 0, rangeDetectOffset), PhysicsUtils.LayerMaskScene)
				&& !Physics.Linecast(from, to + new Vector3(0, 0, -rangeDetectOffset), PhysicsUtils.LayerMaskScene));
			}
			else
			{
				return !Physics.Linecast(from, to, PhysicsUtils.LayerMaskScene);
			}

		}

		#endregion
		#region BehaviorLayer

		private void SendRequest(Request request)
		{
			if (curruntRequest?.requestType < request.requestType
				|| (curruntRequest?.requestType is ERequestType.Avoid && request.requestType is ERequestType.Avoid))
			{
				return;
			}

			switch (request.requestType)
			{
				case ERequestType.Fire:
					Fire();
					break;
				case ERequestType.TurnFire:
					TurretTurnTo((request as TurnFireRequest).aimPos);
					break;
				case ERequestType.Avoid:
					Move((request as AvoidRequest).avoidAimPosition);
					break;
				case ERequestType.Compete:
					goto case ERequestType.Move;
				case ERequestType.Return:
					goto case ERequestType.Move;
				case ERequestType.Move:
					Move((request as MoveRequest).targetPosition);
					break;
				case ERequestType.Idle:
					Move(Position);
					break;
				default:
					Debug.LogError("未知请求类型！");
					break;
			}

			if (request.requestType != ERequestType.Fire && request.requestType != ERequestType.TurnFire)
			{
				curruntRequest = request;
			}
		}

		private void OnBehaviorUpdate()
		{
			if (curruntRequest?.requestType == ERequestType.Avoid)
			{
				AvoidRequest request = (curruntRequest as AvoidRequest);
				if (request.dangerMissile == null
					|| Vector3.Angle((Position - request.dangerMissile.Position), request.dangerMissile.Velocity) > ignoreAvoidAngle)
				{
					curruntRequest = null;
				}
			}
			else if (curruntRequest?.requestType == ERequestType.Compete && knowledgePool.remainingTime < 90)
			{
				curruntRequest = null;
			}
			else if ((curruntRequest?.requestType == ERequestType.Idle || curruntRequest?.requestType == ERequestType.Return) && HP >= 80)
			{
				curruntRequest = null;
			}
			else if (curruntRequest?.requestType == ERequestType.Move && Velocity.magnitude <= 0.1f)
			{
				curruntRequest = null;
			}
		}

		#endregion

		protected override void OnOnDrawGizmos()
		{
			/*
			float angle = GetAimOffsetAngle(knowledgePool.enemyTank);
			if (Mathf.Abs(angle - 0.2f) < 0.01f) Gizmos.color = Color.gray;
			else if (Mathf.Abs(angle - 0.5f) < 0.01f) Gizmos.color = Color.blue;
			else if (angle - 1 == 0) Gizmos.color = Color.green;
			else if (angle - 2 == 0) Gizmos.color = Color.yellow;
			else if (angle - 5 == 0) Gizmos.color = Color.red;
			else if (angle - 10 == 0) Gizmos.color = Color.black;
			else Gizmos.color = Color.white;

			Quaternion r1 = Quaternion.AngleAxis(angle, Vector3.up);
			Quaternion r2 = Quaternion.AngleAxis(-angle, Vector3.up);
			Gizmos.DrawLine(Position, Position + (r1 * knowledgePool.turretAiming * 100));
			Gizmos.DrawLine(Position, Position + (r2 * knowledgePool.turretAiming * 100));

			Gizmos.color = Color.red;
			Gizmos.DrawLine(Position, Position + (TurretAiming * 10));

			Gizmos.DrawSphere(knowledgePool.idlePos, 1); 
			*/
		}
	}

	internal class KnowledgePool
	{
		public Tank enemyTank;
		public Vector3 rebornPos;
		public Vector3 enemyRebornPos;
		public Vector3 middlePos;
		public Vector3 idlePos;
		public Vector3 turretAiming;
		public Dictionary<int, Star> stars;
		public Dictionary<int, Missile> missiles;
		public float remainingTime;
		public float superstarAppearTime;
		public float homeZoneRadius;
		public float missileSpeed;
		public bool isSuperstarExist;
		public int damagePerHit;
	}
}

namespace JYM
{
	internal enum ERequestType
	{
		Fire,
		TurnFire,
		Idle,
		Avoid,
		Compete,
		Return,
		Move,
	}

	internal class Request
	{
		public ERequestType requestType { get; protected set; }
	}

	internal class FireRequest : Request
	{
		public FireRequest()
		{
			requestType = ERequestType.Fire;
		}
	}

	internal class TurnFireRequest : Request
	{
		public Vector3 aimPos;
		public TurnFireRequest(Vector3 aimPos)
		{
			requestType = ERequestType.TurnFire;
			this.aimPos = aimPos;
		}
	}

	internal class IdleRequest : Request
	{
		public IdleRequest()
		{
			requestType = ERequestType.Idle;
		}
	}

	internal class AvoidRequest : Request
	{
		public Vector3 avoidAimPosition;
		public Missile dangerMissile;
		public AvoidRequest(Vector3 avoidAimPosition, Missile dangerMissile)
		{
			requestType = ERequestType.Avoid;
			this.avoidAimPosition = avoidAimPosition;
			this.dangerMissile = dangerMissile;
		}
	}

	internal class CompeteRequest : MoveRequest
	{
		public CompeteRequest(Vector3 targetPosition)
		{
			requestType = ERequestType.Compete;
			this.targetPosition = targetPosition;
		}
	}

	internal class ReturnRequest : MoveRequest
	{
		public ReturnRequest(Vector3 targetPosition)
		{
			requestType = ERequestType.Return;
			this.targetPosition = targetPosition;
		}
	}

	internal class MoveRequest : Request
	{
		public Vector3 targetPosition { get; protected set; }

		protected MoveRequest() { }
		public MoveRequest(Vector3 targetPosition)
		{
			requestType = ERequestType.Move;
			this.targetPosition = targetPosition;
		}
	}

}
