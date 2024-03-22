using System.Collections.Generic;
using Main;
using UnityEngine;

namespace HZ
{
    public abstract class Utility
    {
        public enum AvoidingType
        {
            safe, stop, turnAround,
        }

        //计算射击提前量
        public static Vector3 CalculatePreAmount(MyTank myTank, Tank opposite)
        {
            var oppositeCollider = opposite.gameObject.GetComponentInChildren<BoxCollider>();
            var oppositeColliderCenter = opposite.Position + oppositeCollider.center;
            float offset = oppositeCollider.size.z / 2;

            var signedDistance = myTank.FirePos - opposite.FirePos;
            float angle = Vector3.Angle(signedDistance, opposite.Forward);

            float distance = signedDistance.magnitude;
            float a = 1 -
                      Mathf.Pow((Match.instance.GlobalSetting.MissileSpeed / (opposite.Velocity.magnitude + 0.001f)),
                          2);

            float b = -(2 * distance * Mathf.Cos(angle * Mathf.Deg2Rad)); //要变换成弧度
            float c = distance * distance;
            float delta = b * b - 4 * a * c;

            if (delta < 0) return Vector3.zero;

            float f1 = (-b + Mathf.Sqrt(delta)) / (2 * a);
            float f2 = (-b - Mathf.Sqrt(delta)) / (2 * a);

            return oppositeColliderCenter + opposite.Forward * (offset * 0.5f) +
                   opposite.Forward * ((f1 > f2) ? f1 : f2);
        }

        //计算路径距离
        private static float CalculateRoutineLength(Tank tank, Vector3 target)
        {
            if (tank.IsDead) return float.MaxValue;

            var navMesh = tank.CaculatePath(target);
            var corners = navMesh.corners;
            float pathLength = Vector3.Distance(tank.Position, corners[0]);
            for (int i = 1; i < corners.Length; i++)
            {
                pathLength += Vector3.Distance(corners[i - 1], corners[i]);
            }
            return pathLength;
        }

        //计算返回最近星星
        private static Star CalculateNearestStar(Tank tank, Dictionary<int, Star> stars)
        {
            if (tank.IsDead) return null;

            float minLength = float.MaxValue;
            Star nearestStar = null;
            foreach (var star in stars)
            {
                if (CalculateRoutineLength(tank, star.Value.Position) < minLength)
                {
                    minLength = CalculateRoutineLength(tank, star.Value.Position);
                    nearestStar = star.Value;
                }
            }
            return nearestStar;
        }

        //选择星星
        public static Star ChooseTargetStar(MyTank myTank, Tank opposite, Dictionary<int, Star> stars)
        {
            if (opposite.IsDead) return CalculateNearestStar(myTank, stars);

            var myTarget = CalculateNearestStar(myTank, stars);
            var oppositeTarget = CalculateNearestStar(opposite, stars);

            if (myTarget != oppositeTarget) return myTarget;

            return CalculateRoutineLength(myTank, myTarget.Position) <
                   CalculateRoutineLength(opposite, oppositeTarget.Position)
                ? myTarget
                : null;
        }

        public static Vector3 CalculateMissileHitPoint(MyTank myTank, Missile missile)
        {
            var signedDistance = myTank.Position - missile.Position;
            float angle = Vector3.Angle(signedDistance, missile.Velocity);

            float distance = signedDistance.magnitude;
            float a = 1 - Mathf.Pow((missile.Velocity.magnitude / (myTank.Velocity.magnitude + 0.001f)), 2);

            float b = -(2 * distance * Mathf.Cos(angle * Mathf.Deg2Rad));
            float c = distance * distance;
            float delta = b * b - 4 * a * c;

            if (delta < 0) return Vector3.zero;

            float f1 = (-b + Mathf.Sqrt(delta)) / (2 * a);
            float f2 = (-b - Mathf.Sqrt(delta)) / (2 * a);

            return myTank.Position + myTank.Forward * ((f1 > f2) ? f1 : f2);
        }

        public static AvoidingType CalculateHitType(MyTank myTank, Vector3 hitPoint)
        {
            float distance = Vector3.Distance(myTank.Position, hitPoint);
            if (distance < myTank.stopCircle && distance >= 0.5)
            {
                return AvoidingType.stop;
            }
            if (distance < 0.5)
            {
                return AvoidingType.turnAround;
            }
            return AvoidingType.safe;
        }
    }
}
