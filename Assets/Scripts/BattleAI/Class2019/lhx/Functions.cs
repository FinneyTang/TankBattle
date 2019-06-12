using System.Collections.Generic;
using Main;
using UnityEngine;
using UnityEngine.AI;

namespace lhx
{
	public static class Functions
	{
		public static Vector3 CalculatePredictivePosition(Vector3 tPos, Vector3 oppPos, Vector3 oppVelocity)
		{
			// A: oppTankPos --> myFirePos
			// B: myFirePos  --> targetPos
			// C: oppTankPos --> targetPos
			Vector3 A = tPos - oppPos;
			float cosB = Vector3.Dot(A, oppVelocity) / A.magnitude / oppVelocity.magnitude;
			float sinB = Mathf.Sqrt(1f - cosB * cosB);
			float sinC = sinB / Match.instance.GlobalSetting.MissileSpeed * oppVelocity.magnitude;
			float cosC = Mathf.Sqrt(1f - sinC * sinC);
			// sinA = sin(B + C) = sinB × cosC + cosB × sinC
			float sinA = sinB * cosC + cosB + sinC;
			float lengthC = A.magnitude / sinA * sinC;
			return oppPos + Vector3.ClampMagnitude(oppVelocity, lengthC);
		}

		public static float CalculatePathLength(NavMeshPath path, Vector3 tankPos, Vector3 targetPos)
		{
			float pathLength = Vector3.Distance(tankPos, path.corners[0]);
			for (int i = 1; i < path.corners.Length; i++)
			{
				pathLength += Vector3.Distance(path.corners[i], path.corners[i - 1]);
			}
			if (path.corners.Length > 1)
			{
				pathLength += Vector3.Distance(path.corners[path.corners.Length - 1], targetPos);
			}
			return pathLength;
		}

		public static Missile GetLatestMissile(Dictionary<int, Missile> missiles, ETeam team)
		{
			int missileId = int.MaxValue;
			foreach (var missile in missiles)
			{
				if (missile.Key < missileId)
				{
					missileId = missile.Key;
				}
			}
			return Match.instance.GetOppositeMissiles(team)[missileId];
		}

		public static bool Vector3EqualTo(Vector3 a, Vector3 b)
		{
			return (Mathf.Abs(a.x - b.x) <= 1)
				&& (Mathf.Abs(a.y - b.y) <= 1)
				&& (Mathf.Abs(a.z - b.z) <= 1);
		}
	}
}
