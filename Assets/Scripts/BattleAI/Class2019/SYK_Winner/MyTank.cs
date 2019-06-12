using System;
using System.Collections;
using System.Collections.Generic;
using Main;
using UnityEngine;
using UnityEngine.AI;

namespace SYK
{
    class MyTank : Tank
    {
        struct PosLog
        {
            public Vector3 pos;
            public float time;

            public PosLog(Vector3 pos, float time)
            {
                this.pos = pos;
                this.time = time;
            }
        }
        //List<PosLog> enemyPos = new List<PosLog>();

        Match match;
        Vector3 eTarget = Vector3.zero;
        Vector3 fTarget = Vector3.zero;
        Vector3 mTarget = Vector3.zero;
        Vector3 escape = Vector3.zero;
        int maxMissileKey;
        Tank enemyTank;
        List<Action> actions = new List<Action>();

        public struct Action : IComparable<Action>
        {
            public Vector3 target;
            float weight;

            public Action(Vector3 target, float weight)
            {
                this.target = target;
                this.weight = weight;
            }

            public int CompareTo(Action other)
            {
                if (weight > other.weight)
                {
                    return 1;
                }
                else if (weight < other.weight)
                {
                    return -1;
                }
                return 0;
            }
        }

        public float CalcU(Vector3 target)
        {
            NavMeshPath path = CaculatePath(target);
            float distance = 0;
            for (int i = 0; i < path.corners.Length - 1; i++)
            {
                distance += Vector3.Distance(path.corners[i], path.corners[i + 1]);
            }
            //float distance = Vector3.Distance(Position, target);
            float f = Mathf.Clamp(Vector3.Distance(match.GetRebornPos(Team), target), 0, 120);
            f = Mathf.Sqrt(121 - f);
            if (Vector3.Distance(match.GetRebornPos(Team), target) < match.GlobalSetting.HomeZoneRadius)
            {
                return 1f / distance / distance * (80 - HP) * f;
            }
            else
            {
                return 1f / distance / distance * HP * f;
            }
        }

        public override string GetName()
        {
            return "SYK";
        }
        protected override void OnStart()
        {
            base.OnStart();
            match = Match.instance;
            enemyTank = match.GetOppositeTank(Team);
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            eTarget = ProjectilePrediction(this);
            fTarget = ProjectilePrediction(enemyTank);
            StarGazer();
            Evasion();

            PMove();
            PFire();
        }

        private void PMove()
        {
            actions.Sort();
            actions.Reverse();
            mTarget = actions[0].target;
            Move(CaculatePath(mTarget));
        }

        private void PFire()
        {
            TurretTurnTo(fTarget);
            Vector3 direction = (fTarget - Position).normalized;
            if (Vector3.Dot(TurretAiming, direction) > 0.99f && CanSeeOthers(fTarget))
            {
                Fire();
            }
        }

        new bool CanSeeOthers(Vector3 pos)
        {
            return !Physics.Linecast(Position, pos, PhysicsUtils.LayerMaskScene);
        }

        Vector3 ProjectilePrediction(Tank tank)
        {
            if (tank.IsDead)
            {
                return match.GetRebornPos(tank.Team);
            }
            float distance = Vector3.Distance(tank.Position, match.GetOppositeTank(tank.Team).Position);
            float pTime = distance / match.GlobalSetting.MissileSpeed;
            Vector3 pPos = tank.Position + tank.Velocity * pTime;
            for (int i = 0; i < 2; i++)
            {
                distance = Vector3.Distance(match.GetOppositeTank(tank.Team).Position, pPos);
                pTime = distance / match.GlobalSetting.MissileSpeed;
                pPos = tank.Position + tank.Velocity * pTime;
            }
            return pPos;
        }

        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();

            Gizmos.DrawWireSphere(fTarget, 3f);
            Gizmos.DrawWireSphere(eTarget, 2f);
            Gizmos.DrawWireCube(mTarget, Vector3.one * 3f);
        }

        void StarGazer()
        {
            actions.Clear();
            actions.Add(new Action(match.GetRebornPos(Team), CalcU(match.GetRebornPos(Team))));
            var pairs = match.GetStars();
            foreach (var item in pairs)
            {
                if (item.Value.IsSuperStar)
                {
                    actions.Add(new Action(item.Value.Position, float.PositiveInfinity));
                }
                actions.Add(new Action(item.Value.Position, CalcU(item.Value.Position)));
            }
            actions.Add(new Action(Vector3.zero, float.Epsilon));
        }

        void Evasion()
        {
            if (Vector3.Distance(match.GetRebornPos(Team), Position) < match.GlobalSetting.HomeZoneRadius)
            {
                return;
            }
            if (HP >= enemyTank.HP && Vector3.Distance(Position, enemyTank.Position) < 10f && CanSeeOthers(enemyTank))
            {
                actions.Add(new Action(enemyTank.Position, float.PositiveInfinity));
            }
            var missiles = match.GetOppositeMissiles(Team);
            Missile missile;
            missiles.TryGetValue(maxMissileKey, out missile);
            foreach (var item in missiles)
            {
                if (item.Key > maxMissileKey)
                {
                    maxMissileKey = item.Key;
                    missile = item.Value;
                    escape = Vector3.zero;
                }
                if (Vector3.Distance(item.Value.Position, Position) < 10f)
                {
                    continue;
                }
                if (Vector3.Dot((escape - item.Value.Position).normalized, item.Value.Velocity.normalized) < 0.97f
                    && Vector3.Dot((eTarget - item.Value.Position).normalized, item.Value.Velocity.normalized) < 0.97f
                    && escape != Vector3.zero)
                {
                    break;
                }
                if (Vector3.Dot((eTarget - item.Value.Position).normalized, item.Value.Velocity.normalized) > 0.99f
                    && !Physics.Linecast(eTarget, item.Value.Position, PhysicsUtils.LayerMaskScene)
                    && Velocity.magnitude > 5f)
                {
                    escape = Vector3.zero;
                    for (int i = 8; i < 15; i++)
                    {
                        escape = Vector3.Cross(Vector3.Cross(Velocity.normalized, (enemyTank.Position - eTarget).normalized).y > 0.1f ? Vector3.down : Vector3.up, item.Value.Velocity).normalized * i + eTarget;
                        if (CaculatePath(escape) != null)
                        {
                            break;
                        }
                    }
                    break;
                }
                else if (Vector3.Dot((Position - item.Value.Position).normalized, item.Value.Velocity.normalized) > 0.98f
                    && CanSeeOthers(item.Value.Position))
                {
                    escape = Vector3.zero;
                    for (int i = 8; i < 15; i++)
                    {
                        escape = Vector3.Cross(Vector3.Cross(Velocity.normalized, (enemyTank.Position - Position).normalized).y > 0.1f ? Vector3.up : Vector3.down, item.Value.Velocity).normalized * i + Position;
                        if (CaculatePath(escape) != null)
                        {
                            break;
                        }
                    }
                }
            }
            if (escape != Vector3.zero && CaculatePath(escape) != null)
            {
                actions.Add(new Action(escape, float.MaxValue));
            }
            if (missile == null ||
                Vector3.Dot((Position - missile.Position).normalized, missile.Velocity.normalized) < 0.9f)
            {
                escape = Vector3.zero;
            }
        }
    }
}
