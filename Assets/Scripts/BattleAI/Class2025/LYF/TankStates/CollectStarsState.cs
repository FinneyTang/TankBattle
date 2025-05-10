using Main;
using UnityEngine;

namespace LYF
{
    public class CollectStarsState : TankState
    {
        public CollectStarsState(MyTank tank) : base(tank) { }


        public override void OnUpdate()
        {
            if (tank.HP <= tank.stateParams.minHpBeforeRetreat)
            {
                tank.ChangeState(new RetreatState(tank));
                return;
            }    

            Star target = FindTargetStar();

            // 不存在星星或者星星距离太远时
            if (target == null)
            {
                // 血少就回血
                if (tank.EnemyTank.IsDead && tank.HP <= 80)
                    tank.ChangeState(new RetreatState(tank));
                // 血多就占中
                else
                    tank.Move(Match.instance.gameObject.transform.position);
            }
            // 否则前去收集星星
            else
            {
                tank.Move(target.Position);
            }
        }

        private Star FindTargetStar()
        {
            var stars = Match.instance.GetStars().Values;
            if (stars.Count == 0) return null;

            // 寻找最近的星星或超级星星
            Star target = null;
            float minDist = float.MaxValue;
            foreach (var star in stars)
            {
                float dist = (star.Position - tank.Position).sqrMagnitude;
                if (star.IsSuperStar)
                {
                    return star;
                }
                if (dist < minDist)
                {
                    minDist = dist;
                    target = star;
                }
            }

            if (minDist < tank.stateParams.maxDistToCollectStars * tank.stateParams.maxDistToCollectStars
                * (tank.EnemyTank.IsDead ? 1.2 * 1.2 : 1) )
                return target;
            else
                return null;
        }
    }
}
