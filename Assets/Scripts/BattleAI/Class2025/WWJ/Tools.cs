using Main;
using UnityEngine;

namespace WWJ
{
    public class Tools
    {
        // 解二元一次方程组
        public static void CalculationDualLinearEquation(float a1, float b1, float c1, float a2, float b2, float c2,
            out float x, out float y)
        {
            x = (c2 * b1 - c1 * b2) / (a1 * b2 - a2 * b1);
            y = (a1 * c2 - a2 * c1) / (a2 * b1 - a1 * b2);
        }

        // 判断碰撞是否为自身坦克
        public static bool JudgeHitIsTank(RaycastHit hit,Tank tank)
        {
            var fireCollider = hit.transform.GetComponent<FireCollider>();
            if (fireCollider != null)
            {
                if (fireCollider.Owner == tank)
                    return true;
            }
            return false;
        }

        // 判断碰撞是否为墙壁
        public static bool JudgeHitWall(RaycastHit hit)
        {
            var fireCollider = hit.transform.GetComponent<FireCollider>();
            if (fireCollider == null)
                return true;
            return false;
        }
    }
}