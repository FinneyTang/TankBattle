using Main;
using UnityEngine;

namespace HZR
{
    public class MyMath
    {
        public static void CalculationDualPlanePoint(float a1, float b1, float c1,float d1, float a2, float b2, float c2,float d2,
            out float m, out float n,int choose,float value)
        {
            switch (choose)
            {
                case 1:
                    d1 = d1 + value * a1;
                    d2 = d2 + value * a2;
                    CalculationDualLinearEquation(b1, c1, d1, b2, c2, d2, out m, out n);
                    break;
                case 2:
                    d1 = d1 + value * b1;
                    d2 = d2 + value * b2;
                    CalculationDualLinearEquation(a1, c1, d1, a2, c2, d2, out m, out n);
                    break;
                case 3:
                    d1 = d1 + value * c1;
                    d2 = d2 + value * c2;
                    CalculationDualLinearEquation(a1, b1, d1, a2, b2, d2, out m, out n);
                    break;
                default:
                    m = 0;
                    n = 0;
                    break;
            }
        }
        public static void CalculationDualLinearEquation(float a1, float b1, float c1, float a2, float b2, float c2,
            out float x, out float y)
        {
            x = (c2 * b1 - c1 * b2) / (a1 * b2 - a2 * b1);
            y = (a1 * c2 - a2 * c1) / (a2 * b1 - a1 * b2);
        }

        public static void CalculationLineEquation(Vector3 forward, Vector3 position,out float a,out float b,out float c,out float d)
        {
            a = forward.x;
            b = forward.y;
            c = forward.z;
            d = -a * position.x - b * position.y - c * position.z;
        }
        
    }

    public class MyTool
    {
        public static bool JudgeHitIsTank(RaycastHit hit,Tank tank)
        {
            var fireCollider = hit.transform.GetComponent<FireCollider>();
            if (fireCollider != null)
            {
                if (fireCollider.Owner == tank)
                    return true;
                else
                {
                    return false;
                }
            }
            return false;
        }

        public static bool JudgeHitWall(RaycastHit hit)
        {
            var fireCollider = hit.transform.GetComponent<FireCollider>();
            if (fireCollider == null)
                return true;
            return false;
        }

        public static Vector3 PredictedFireForward(Vector3 firePos,Vector3 TargetPos,Vector3 Speed,float MissileSpeed)
        {
            Vector3 targetSpeed = Speed;
            Vector3 firePosition = firePos;
            Vector3 d = TargetPos - firePosition;
            float vp = MissileSpeed;
            float v0 = targetSpeed.magnitude;
            float cosp0 = Mathf.Cos(Vector3.Angle(-d, targetSpeed) * (Mathf.PI / 180));
            float a = v0 * v0 - vp * vp;
            float b = -2 * v0 * d.magnitude * cosp0;
            float c = d.sqrMagnitude;
            float delta = b * b - 4 * a * c;
            float predictedTime = (-b - Mathf.Sqrt(delta)) / (2 * a);
            Vector3 turnToForward = d + targetSpeed * predictedTime;
            return turnToForward;
        }

    }
}