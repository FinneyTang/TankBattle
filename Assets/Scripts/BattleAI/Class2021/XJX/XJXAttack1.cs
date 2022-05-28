using System.Collections;
using Main;
using System.Collections.Generic;
using UnityEngine;


public class XJXAttack1 : Tank
{
    private float m_LastTime = 0;
    protected override void OnUpdate()
    {
        base.OnUpdate();
        if (HP <= 25)
        {
            Move(Match.instance.GetRebornPos(Team));
        }
        else
        {
            bool hasStar = false;
            float nearestDist = float.MaxValue;
            Vector3 nearestStarPos = Vector3.zero;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    hasStar = true;
                    nearestStarPos = s.Position + new Vector3(10, 0, 0);
                    break;
                }//获得最近的星星
                else
                {
                    float dist = (s.Position - Position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        hasStar = true;
                        nearestDist = dist;
                        nearestStarPos = s.Position + new Vector3(10, 0, 0);
                    }
                }
            }
            if (hasStar == true)
            {
                Move(nearestStarPos);
            }
            else
            {
                if (Time.time > m_LastTime)
                {
                    if (ApproachNextDestination())
                    {
                        m_LastTime = Time.time + Random.Range(3, 8);
                    }
                }
            }
        }
        Tank oppTank = Match.instance.GetOppositeTank(Team);//敌方坦克
        if (oppTank != null)
        {
            if (CanSeeOthers(oppTank))//旋转炮台，开火
            {
                TurretTurnTo(oppTank.Position);
                Vector3 toTarget = oppTank.Position - FirePos;
                toTarget.y = 0;
                toTarget.Normalize();
                if (Vector3.Dot(TurretAiming, toTarget) > 0.9995f)
                {
                    Fire();
                }
            }
            else
            {
                TurretTurnTo(Position + -Forward);//炮台默认前方
            }
        }
    }
    protected override void OnReborn()//
    {
        base.OnReborn();
        m_LastTime = 0;
    }
    private bool ApproachNextDestination()
    {
        float halfSize = PhysicsUtils.MaxFieldSize * 0.5f;
        return Move(new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize)));
    }
    public override string GetName()
    {
        return "XjxAttack";
    }
}