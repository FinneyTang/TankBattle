using System;
using System.Collections;
using System.Collections.Generic;
using AI.Base;
using Main;
using UnityEngine;

public class MathUtil : MonoBehaviour
{
    public static bool WillHitMyself(Tank enemyTank)
    {
        if (!enemyTank.IsDead)
        {
            bool coil = Physics.Raycast(enemyTank.FirePos, enemyTank.TurretAiming,float.MaxValue,PhysicsUtils.LayerMaskTank);
            return coil;
        }

        return false;
    }
    

    public static Star GetClosestStar(IAgent tank)
    {
        Tank t = (Tank)tank;
        Dictionary<int, Star> stars = Match.instance.GetStars();
        Star closestStar = null;
        float closetDis = 0;
        if (stars.Count > 0)
        {
            foreach (var star in stars)
            {
                if (star.Value.IsSuperStar)
                {
                    closestStar = star.Value;
                    break;
                }
                float dis = (t.Position - star.Value.Position).magnitude;
                if (!closestStar)
                {
                    closetDis = dis;
                    closestStar = star.Value;
                }
                else
                {
                    if (dis < closetDis)
                    {
                        closetDis = dis;
                        closestStar = star.Value;
                    }  
                }
            }
        }
        return closestStar;
    }

    
}
