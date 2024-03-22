using AI.Base;
using AI.RuleBased;
using Main;
using System.Collections.Generic;
using UnityEngine;

namespace ZYH_ICE
{
    class SuperStarPreparation : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            float time = Match.instance.RemainingTime;
            return time < 110 && time > 100;
        }
    }

    class SuperStarAboutToGenerate : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            foreach (var pair in Match.instance.GetStars())
            {
                if (pair.Value.IsSuperStar)
                {
                    return true;
                }
            }
            float time = Match.instance.RemainingTime;
            return time <= 100 && time > 90;
        }
    }

    class superStarGenerateFewSecondsLeft : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            float time = Match.instance.RemainingTime;
            foreach (var pair in Match.instance.GetStars())
            {
                if (pair.Value.IsSuperStar)
                {
                    return true;
                }
            }
            if(time < 89)
            {
                return false;
            }
            if(time < 95 && myTank.Position.magnitude > 17)
            {
                return true;
            }
            return time < 93.5f;
        }
    }

    class SuperStarOtherCondition : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            return new AndCondition(
                new NotCondition(new SuperStarPreparation()),
                new NotCondition(new SuperStarAboutToGenerate())
                ).IsTrue(agent);
        }
    }

    class EnemyAlive : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank t = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(t.Team);
            if(oppTank != null)
            {
                return !oppTank.IsDead;
            }
            else
            {
                return false;
            }
        }
    }

    class ResistMoreHit : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            if(oppTank != null && !oppTank.IsDead)
            {
                return myTank.HP / 20 > oppTank.HP / 20;
            }
            else
            {
                return true;
            }
        }
    }

    class EnemyFarFromHome : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            Vector3 enemyPosition = oppTank.Position;
            Vector3 enemyHomePosition = Match.instance.GetRebornPos(oppTank.Team);
            return (enemyPosition - enemyHomePosition).magnitude > 20;
        }
    }

    class FullHP : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            return myTank.HP == 100;
        }
    }

    class hasStars : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            int count = Match.instance.GetStars().Count;
            if (count > 0)
            {
                MyTank myTank = (MyTank)agent;
                float distance = 1000;
                int index = -1;
                foreach (var pair in Match.instance.GetStars())
                {
                    if (distance > (myTank.Position - pair.Value.Position).magnitude)
                    {
                        distance = (myTank.Position - pair.Value.Position).magnitude;
                        index = pair.Key;
                    }
                }
                myTank.index = index;
            }
            return count > 0;
        }
    }

    class starNearToMe : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            MyTank myTank = (MyTank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            Vector3 myPosition = myTank.Position;
            float distance = 1000;
            int index = -1;
            foreach (var pair in Match.instance.GetStars())
            {
                Vector3 starPosition = pair.Value.Position;
                if(distance > (myPosition - starPosition).magnitude)
                {
                    distance = (myPosition - starPosition).magnitude;
                    index = pair.Key;
                }
            }
            if (oppTank == null)
            {
                return true;
            }
            Vector3 enemyPosition = oppTank.Position;
            if (distance < (enemyPosition - Match.instance.GetStars()[index].Position).magnitude)
            {
                myTank.index = index;
                return true;
            }
            else
            {
                distance = 1000;
                foreach (var pair in Match.instance.GetStars())
                {
                    if(pair.Key == index)
                    {
                        continue;
                    }
                    Vector3 starPosition = pair.Value.Position;
                    if (distance > (myPosition - starPosition).magnitude)
                    {
                        distance = (myPosition - starPosition).magnitude;
                        index = pair.Key;
                    }
                }
                myTank.index = index;
                return false;
            }
        }
    }

    class AnotherStar : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            return Match.instance.GetStars().Count > 1;
        }
    }

    class canResistThreeAttack : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            return myTank.HP / 20 > 3;
        }
    }

    class OnMissileTrajectory : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            MyTank myTank = (MyTank)agent;
            myTank.directMissiles.Clear();
            Dictionary<int,Missile> missiles = Match.instance.GetOppositeMissiles(myTank.Team);
            foreach(var pair in missiles)
            {
                Missile missile = pair.Value;
                if (Physics.SphereCast(missile.Position, 0.1f, missile.Velocity, out RaycastHit hit, 40))
                {
                    FireCollider fireCollider = hit.transform.GetComponent<FireCollider>();
                    if (fireCollider != null)
                    {
                        if (fireCollider.Owner == myTank)
                        {
                            myTank.directMissiles.Add(pair.Key, missile);
                        }
                    }
                }
            }
            if(myTank.directMissiles.Count > 0)
            {
                return true;
            }
            return false;
        }
    }

    class willHitPredictionMissile : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            MyTank myTank = (MyTank)agent;
            Dictionary<int, Missile> missiles = Match.instance.GetOppositeMissiles(myTank.Team);
            foreach (var pair in missiles)
            {
                Missile missile = pair.Value;
                Collider[] colliders = Physics.OverlapSphere(missile.Position, 5f);
                foreach (var collider in colliders)
                {
                    if (collider != null)
                    {
                        if (collider.gameObject == myTank.gameObject)
                        {
                            myTank.Move(myTank.Position);
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }

    class enemyNearToMe : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            Tank oppTank = Match.instance.GetOppositeTank(myTank.Team);
            if(oppTank != null)
            {
                if((myTank.Position - oppTank.Position).magnitude < 15)
                {
                    return true;
                }
            }
            return false;
        }
    }

    class onlyResistOneHit : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            Tank myTank = (Tank)agent;
            return (myTank.HP / 20) <= 1;
        }
    }

    class NearToGoodPosition : Condition
    {
        public override bool IsTrue(IAgent agent)
        {
            MyTank myTank = (MyTank)agent;
            return (myTank.Position - myTank.goodPosition).magnitude < 2;
        }
    }

    class MyTank : Tank
    {
        private Condition goToSuperStar;
        private Condition goToEnemy;
        private Condition goBackHome;
        private Condition goToStar;
        private Condition goToGoodPosition;
        private Condition avoidMissile;

        private Condition goToEnemy1;
        private Condition goToEnemy2;
        private Condition goBackHome1;
        private Condition goBackHome2;
        private Condition goBackHome3;
        private Condition goToStar1;
        private Condition goToStar2;
        private Condition goToStar3;
        private Condition goToGoodPosition1;
        private Condition goToGoodPosition2;
        private Condition goToGoodPosition3;

        private Condition enemyLessHitAndFarFromHome;
        private Condition goToStarNodeContion;

        public int index;    //要去的星星的索引
        public Vector3 goodPosition;    //好位置
        public Dictionary<int, Missile> directMissiles;    //直向导弹

        protected override void OnStart()
        {
            base.OnStart();

            index = -1;

            enemyLessHitAndFarFromHome = new AndCondition(new ResistMoreHit(),
                new AndCondition(new EnemyFarFromHome(), new enemyNearToMe()));
            goToStarNodeContion = new AndCondition(
                new SuperStarOtherCondition(),
                new OrCondition(
                    new AndCondition(
                        new EnemyAlive(),
                        new NotCondition(enemyLessHitAndFarFromHome)),
                    new NotCondition(new EnemyAlive())));

            goToEnemy1 = new AndCondition(
                new SuperStarPreparation(),
                new AndCondition(
                    new EnemyAlive(),
                    enemyLessHitAndFarFromHome));
            goToEnemy2 = new AndCondition(
                new SuperStarOtherCondition(),
                new AndCondition(
                    new EnemyAlive(),
                    enemyLessHitAndFarFromHome));
            goBackHome1 = new AndCondition(
                new SuperStarPreparation(),
                new AndCondition(
                    new OrCondition(
                        new AndCondition(
                            new EnemyAlive(),
                            new NotCondition(enemyLessHitAndFarFromHome)),
                        new NotCondition(new EnemyAlive())),
                    new NotCondition(new FullHP())));
            goBackHome2 = new AndCondition(
                new SuperStarOtherCondition(),
                new AndCondition(
                    new OrCondition(
                        new AndCondition(
                            new EnemyAlive(),
                            new NotCondition(enemyLessHitAndFarFromHome)),
                        new NotCondition(new EnemyAlive())),
                    new AndCondition(
                        new OrCondition(
                            new AndCondition(
                                new hasStars(),
                                new NotCondition(new starNearToMe())),
                            new NotCondition(new hasStars())),
                        new NotCondition(new canResistThreeAttack()))));
            goBackHome3 = new OrCondition(new onlyResistOneHit(),
                new AndCondition(new enemyNearToMe(),
                new NotCondition(new ResistMoreHit())));
            goToStar1 = new AndCondition(
                new SuperStarPreparation(),
                new AndCondition(
                    new OrCondition(
                        new AndCondition(
                            new EnemyAlive(),
                            new NotCondition(enemyLessHitAndFarFromHome)),
                        new NotCondition(new EnemyAlive())),
                    new AndCondition(
                        new FullHP(),
                        new hasStars())));
            goToStar2 = new AndCondition(
                goToStarNodeContion,
                new AndCondition(
                    new hasStars(),
                    new starNearToMe()));
            goToStar3 = new AndCondition(
                goToStarNodeContion,
                new AndCondition(
                    new OrCondition(
                        new AndCondition(
                            new hasStars(),
                            new NotCondition(new starNearToMe())),
                        new NotCondition(new hasStars())),
                    new AndCondition(
                        new canResistThreeAttack(),
                        new AnotherStar())));
            goToGoodPosition1 = new AndCondition(
                new SuperStarPreparation(),
                new AndCondition(
                    new OrCondition(
                        new AndCondition(
                            new EnemyAlive(),
                            new NotCondition(enemyLessHitAndFarFromHome)),
                        new NotCondition(new EnemyAlive())),
                    new AndCondition(
                        new FullHP(),
                        new NotCondition(new hasStars()))));
            goToGoodPosition2 = new AndCondition(
                goToStarNodeContion,
                new AndCondition(
                    new OrCondition(
                        new AndCondition(
                            new hasStars(),
                            new NotCondition(new starNearToMe())),
                        new NotCondition(new hasStars())),
                    new AndCondition(
                        new canResistThreeAttack(),
                        new NotCondition(new AnotherStar()))));
            goToGoodPosition3 = new AndCondition(new SuperStarAboutToGenerate(),
                new NotCondition(new NearToGoodPosition()));

            goToSuperStar = new superStarGenerateFewSecondsLeft();
            goToEnemy = new OrCondition(goToEnemy1, goToEnemy2);
            goBackHome = new OrCondition(goBackHome1, new OrCondition(goBackHome2, goBackHome3));
            goToStar = new OrCondition(goToStar1, new OrCondition(goToStar2, goToStar3));
            goToGoodPosition = new OrCondition(goToGoodPosition1,new OrCondition(
                goToGoodPosition2, goToGoodPosition3));
            avoidMissile = new AndCondition(
                new NotCondition(
                    new AndCondition(
                        new SuperStarPreparation(),
                        new NotCondition(new FullHP()))), 
                new AndCondition(
                    new NotCondition(
                        new AndCondition(
                            new SuperStarAboutToGenerate(),
                            new NotCondition(new NearToGoodPosition())))
                    ,new OrCondition(
                        new OnMissileTrajectory(), 
                        new willHitPredictionMissile())));

            directMissiles = new Dictionary<int, Missile>();
    }
        protected override void OnUpdate()
        {
            base.OnUpdate();

            Tank oppTank = Match.instance.GetOppositeTank(Team);

            //炮管瞄准敌人
            //具备开火条件则开火
            TurnTurret();

            chooseGoodPosition();
            if (goToSuperStar.IsTrue(this))
            {
                Debug.Log("去超级星星那");
                Move(Vector3.zero);
            }
            else if (avoidMissile.IsTrue(this))
            {
                if(new OnMissileTrajectory().IsTrue(this))
                {
                    avoidDirectMissile();
                }else if(new willHitPredictionMissile().IsTrue(this))
                {
                    Move(this.Position);
                }
            }
            else
            {
                if (goToEnemy.IsTrue(this))
                {
                    Debug.Log("去敌人那");
                    Move(oppTank.Position);
                }
                else if (goBackHome.IsTrue(this))
                {
                    Debug.Log("回家");
                    Move(Match.instance.GetRebornPos(Team));
                }
                else if (goToStar.IsTrue(this))
                {
                    Debug.Log("去星星那");
                    Move(Match.instance.GetStarByID(index).Position);
                }
                else if (goToGoodPosition.IsTrue(this))
                {
                    Debug.Log("去好位置");
                    Move(goodPosition);
                }
                else
                {
                    Debug.Log("不合理啊");
                }
            }
        }
        protected override void OnReborn()
        {
            base.OnReborn();
            index = -1;
        }

        public override string GetName()
        {
            return "ZYH_ICE_Tank";
        }

        private void TurnTurret()
        {
            Tank oppTank = Match.instance.GetOppositeTank(this.Team);
            if (oppTank != null && oppTank.IsDead == false)
            {
                Transform turret = this.transform.GetChild(1);
                Vector2 oppPosition = new Vector2(oppTank.Position.x, oppTank.Position.z);
                Vector2 oppVelocity = new Vector2(oppTank.Velocity.x, oppTank.Velocity.z);
                Vector2 myFirePosition = new Vector2(this.FirePos.x, this.FirePos.z);
                Vector2 deltaPosition = oppPosition - myFirePosition;
                float a = Mathf.Pow(oppVelocity.x, 2) + Mathf.Pow(oppVelocity.y, 2) - 1600;
                float b = 2 * (deltaPosition.x * oppVelocity.x + deltaPosition.y * oppVelocity.y);
                float c = Mathf.Pow(deltaPosition.x, 2) + Mathf.Pow(deltaPosition.y, 2);
                float delta = b * b - 4 * a * c;
                float predictedTime = (-b - Mathf.Sqrt(delta)) / (2 * a);
                Vector2 predictedPosition = deltaPosition + oppVelocity * predictedTime;
                Vector3 targetDirection = new Vector3(predictedPosition.x, 0, predictedPosition.y);
                turret.forward = Vector3.Lerp(turret.forward, targetDirection, Time.deltaTime * 180);
                if ((this.Position - oppTank.Position).magnitude < 15)
                {
                    this.Fire();
                }
                else if (Physics.SphereCast(this.FirePos, 0.24f, targetDirection, out RaycastHit hit,
                              (targetDirection - this.FirePos).magnitude - 2))
                {
                    FireCollider fireCollider = hit.transform.GetComponent<FireCollider>();
                    if (fireCollider != null)
                    {
                        if (Vector3.Angle(this.TurretAiming, targetDirection) < 2)
                            this.Fire();
                    }
                }
                else
                {
                    if (Vector3.Angle(this.TurretAiming, targetDirection) < 2)
                        this.Fire();
                }
            }
            else
            {
                this.TurretTurnTo(this.Position + this.Forward);
            }
        }

        private void chooseGoodPosition()
        {
            if (new SuperStarAboutToGenerate().IsTrue(this) &&
                        new NotCondition(new superStarGenerateFewSecondsLeft()).IsTrue(this))
            {
                Tank oppTank = Match.instance.GetOppositeTank(this.Team);
                if(Match.instance.GetRebornPos(this.Team).x > 0)
                {
                    if (oppTank != null && !oppTank.IsDead)
                    {
                        if (oppTank.Position.x > 10 || (oppTank.Position.z >= 30 && oppTank.Position.z <= 50 &&
                            oppTank.Position.x >= -5 && oppTank.Position.x <= 10))
                        {
                            goodPosition = new Vector3(-7, 0, -33);
                        }
                        else
                        {
                            goodPosition = new Vector3(13, 0, -5);
                        }
                    }
                }
                else
                {
                    if (oppTank != null && !oppTank.IsDead)
                    {
                        if (oppTank.Position.x < -10 || (oppTank.Position.z <= 130 && oppTank.Position.z >= -50 &&
                            oppTank.Position.x <= 5  && oppTank.Position.x >= -10))
                        {
                            goodPosition = new Vector3(7, 0, 33);
                        }
                        else
                        {
                            goodPosition = new Vector3(-13, 0, 5);
                        }
                    }
                }
            }
            else
            {
                goodPosition = new Vector3(0, 0, 0);
            }
        }

        private void avoidDirectMissile()
        {
            foreach(var pair in directMissiles)
            {
                Missile missile = pair.Value;
                Vector3 onWhichSideInfo = Vector3.Cross(missile.Velocity, this.Position - missile.Position);
                //垂直于导弹速度躲避
                Vector3 cross = Vector3.Cross(missile.Velocity, Vector3.up).normalized;
                if (onWhichSideInfo.y > 0)
                    cross *= -1;
                this.Move(this.Position + cross * 4.2f);
            }
        }
    }
}