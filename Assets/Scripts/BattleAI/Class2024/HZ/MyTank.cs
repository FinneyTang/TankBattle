using System;
using System.Collections;
using System.Collections.Generic;
using Main;
using UnityEngine;

namespace HZ
{
    public enum StateType
    {
        safe,
        stop,
        turnAround,
        goHome
    }

    public class MyTank : Tank
    {
        public float stopCircle = 6f;

        private Dictionary<int, Star> _stars;
        private Dictionary<int, Missile> _missiles;
        private Star _targetStar;
        private Tank _opposite;
        private readonly Dictionary<int, Vector3> _calculatedMissiles = new();

        public float timer;
        private float _degree;
        //private bool _needAvoid = true;
        public StateType _state;

        protected override void OnStart()
        {
            base.OnStart();
            _stars = Match.instance.GetStars();
            _opposite = Match.instance.GetOppositeTank(this.Team);
            StartCoroutine(TurningTurret());
        }

        public override string GetName()
        {
            return "HZ_Tank";
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            _missiles = Match.instance.GetOppositeMissiles(Team);
            
            Move(RotateAvoiding());
            if (timer == 0)
            {
                _state = StateController();
            }
            
            switch (_state)
            {
                // case Utility.AvoidingType.Safe:
                //     _needAvoid = false;
                //     timer = 0;
                //     _degree = Vector3.Angle(Forward, Vector3.right) * Mathf.Deg2Rad;
                //     Move(_targetStar != null ? _targetStar.Position : Vector3.zero);
                //     //Move(RotateAvoiding());
                //     break;
                // case Utility.AvoidingType.Stop:
                //     _needAvoid = true;
                //     timer += (Time.deltaTime * 10);
                //     Move(RotateAvoiding());
                //     break;
                case StateType.safe:
                    timer = 0;
                    Move(_targetStar != null ? _targetStar.Position : Vector3.zero);
                    break;
                case StateType.stop:
                    timer = 0;
                    Move(Position);
                    break;
                case StateType.turnAround:
                    Move(RotateAvoiding());
                    break;
                case StateType.goHome:
                    timer = 0;
                    Move(Match.instance.GetRebornPos(Team));
                    break;
            }
        }

        private bool CanHitTheTrueAimingPoint(Vector3 trueAimingPoint)
        {
            return !Physics.Linecast(this.FirePos, trueAimingPoint);
        }

        private void FireExe(Vector3 aimPos)
        {
            if (CanSeeOthers(_opposite) || CanHitTheTrueAimingPoint(aimPos))
                StartCoroutine(Wait2Fire());
        }

        private void TurretTurning(out Vector3 aimingPoint)
        {
            aimingPoint = Utility.CalculatePreAmount(this, _opposite);
            TurretTurnTo(aimingPoint);
        }

        private void SelectTarget()
        {
            if (_stars.Count > 0) _targetStar = Utility.ChooseTargetStar(this, _opposite, _stars);
        }

        private IEnumerator Wait2Fire()
        {
            yield return new WaitForSeconds(0.05f);
            Fire();
        }

        private IEnumerator TurningTurret()
        {
            while (true)
            {
                SelectTarget();
                TurretTurning(out var aimPos);
                FireExe(aimPos);
                yield return null;
            }
        }

        protected override void OnOnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(Utility.CalculatePreAmount(this, _opposite), 1f);
            foreach (var missileID in _calculatedMissiles.Keys)
            {
                if (_missiles.TryGetValue(missileID, out var missile))
                {
                    Gizmos.DrawSphere(Utility.CalculateMissileHitPoint(this, missile), 1);
                }
            }
        }

        private Utility.AvoidingType NeedAvoidingMissile()
        {
            var avoidingType = Utility.AvoidingType.safe;
            foreach (var missile in _missiles)
            {
                if (!_calculatedMissiles.ContainsKey(missile.Key))
                {
                    _calculatedMissiles.Add(missile.Key, Utility.CalculateMissileHitPoint(this, missile.Value));
                }
            }
            foreach (int calculatedMissile in _calculatedMissiles.Keys)
            {
                if (_missiles.TryGetValue(calculatedMissile, out var missile))
                {
                    avoidingType = (Utility.CalculateHitType(this, _calculatedMissiles[calculatedMissile]));
                }
            }
            Debug.Log(avoidingType);
            return avoidingType;
        }

        private Vector3 RotateAvoiding()
        {
            timer += (Time.deltaTime * 150);
            if (timer >= 360)
            {
                _state = StateType.safe;
            }
            float angle = Mathf.Deg2Rad * timer;

            float x = Mathf.Cos(angle) * 10;
            float y = Mathf.Sin(angle) * 10;

            var a = new Vector3(x, 0, y);

            Debug.Log(new Vector3(a.x + Position.x, 0, a.z + Position.z));
            return new Vector3(a.x + Position.x, 0, a.z + Position.z);
        }

        private StateType StateController()
        {
            if (_targetStar == null && this.HP <= 40)
            {
                return StateType.goHome;
            }
            switch (NeedAvoidingMissile())
            {
                case Utility.AvoidingType.safe:
                    return StateType.safe;

                case Utility.AvoidingType.stop:
                    return StateType.stop;

                case Utility.AvoidingType.turnAround:
                    return StateType.turnAround;
                default:
                    return StateType.safe;
            }
        }
    }
}
