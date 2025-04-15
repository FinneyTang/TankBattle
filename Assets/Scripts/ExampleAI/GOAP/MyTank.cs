using System.Collections.Generic;
using AI.GOAP;
using Main;
using UnityEditor;
using UnityEngine;

namespace GOAP
{
    public static class WorldStateKey
    {
        public const string HasEnemy = "HasEnemy";
        public const string HasFullHP = "HasFullHP";
        public const string StarAvailable = "StarAvailable";
        public const string SuperStarAvailable = "SuperStarAvailable";
        
        public const string EnemyKilled = "EnemyKilled";
        public const string ScoreIncreased = "ScoreIncreased";
    }
    
    public class KillEnemyAction : GOAPAction
    {
        public KillEnemyAction()
        {
            AddPrecondition(WorldStateKey.HasEnemy, true);
            AddPrecondition(WorldStateKey.HasFullHP, true);
            AddEffect(WorldStateKey.EnemyKilled, true);
        }
        public override bool Update()
        {
            var t = (Tank)Agent;
            var oppTank = Match.instance.GetOppositeTank(t.Team);
            if (oppTank == null || oppTank.IsDead)
            {
                return true;
            }
            if (t.HP < 30)
            {
                return true;
            }
            t.Move(oppTank.Position);
            return false;
        }
    }

    public class FindStarAction : GOAPAction
    {
        private int m_StarID = -1;
        public FindStarAction()
        {
            AddPrecondition(WorldStateKey.StarAvailable, true);
            AddEffect(WorldStateKey.ScoreIncreased, true);
        }
        public override void Init()
        {
            var t = (Tank)Agent;
            
            bool hasStar = false;
            float nearestDist = float.MaxValue;
            Star nearestStar = null;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    hasStar = true;
                    nearestStar = s;
                    break;
                }
                else
                {
                    float dist = (s.Position - t.Position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        hasStar = true;
                        nearestDist = dist;
                        nearestStar = s;
                    }
                }
            }
            if (hasStar)
            {
                m_StarID = nearestStar.ID;
                t.Move(nearestStar.Position);
            }
            else
            {
                m_StarID = -1;
            }
        }
        public override bool Update()
        {
            if (m_StarID > 0 && Match.instance.GetStarByID(m_StarID) != null)
            {
                return false;
            }
            return true;
        }
    }

    public class BackHomeAction : GOAPAction
    {
        public BackHomeAction()
        {
            AddEffect(WorldStateKey.HasFullHP, true);
        }

        public override void Init()
        {
            var t = (Tank)Agent;
            t.Move(Match.instance.GetRebornPos(t.Team));
        }
        public override bool Update()
        {
            var t = (Tank)Agent;
            return t.HP == Match.instance.GlobalSetting.MaxHP;
        }
    }

    public class FindSuperStar : GOAPAction
    {
        private int m_StarID = -1;
        public FindSuperStar()
        {
            AddPrecondition(WorldStateKey.SuperStarAvailable, true);
            AddEffect(WorldStateKey.ScoreIncreased, true);
            Cost = 0;
        }
        public override void Init()
        {
            var t = (Tank)Agent;
            m_StarID = -1;
            foreach (var pair in Match.instance.GetStars())
            {
                Star s = pair.Value;
                if (s.IsSuperStar)
                {
                    m_StarID = s.ID;
                    t.Move(s.Position);
                    break;
                }
            }
        }
        public override bool Update()
        {
            if (m_StarID > 0 && Match.instance.GetStarByID(m_StarID) != null)
            {
                return false;
            }
            return true;
        }
    }
    
    public class MyTank : Tank
    {
        public override string GetName()
        {
            return "GOAPTank";
        }

        private Planner m_Planner;
        
        private readonly WorldState m_CurrentState = new WorldState();
        private readonly WorldState m_NextGoal = new WorldState();
        private WorldState m_Goal = new WorldState();
        
        private List<GOAPAction> m_Plan = new List<GOAPAction>();
        private GOAPActionMachine m_ActionMachine;
        
        protected override void OnStart()
        {
            m_Planner = new Planner(this);
            m_Planner.AddAction(new FindStarAction());
            m_Planner.AddAction(new BackHomeAction());
            m_Planner.AddAction(new KillEnemyAction());
            m_Planner.AddAction(new FindSuperStar());
            
            m_ActionMachine = new GOAPActionMachine(this);
        }

        protected override void OnUpdate()
        {
            UpdateWorldState();
            UpdateGoal();
            UpdatePlanner();
            UpdateAction();
        }

        protected override void OnReborn()
        {
            m_ActionMachine.Clear();
            m_Goal.Clear();
            m_NextGoal.Clear();
        }

        private void UpdateWorldState()
        {
            var stars = Match.instance.GetStars();
            m_CurrentState.SetState(WorldStateKey.StarAvailable, stars.Count > 0);
            bool hasSuperStar = false;
            foreach (var pair in stars)
            {
                if (pair.Value.IsSuperStar)
                {
                    hasSuperStar = true;
                    break;
                }
            }
            m_CurrentState.SetState(WorldStateKey.SuperStarAvailable, hasSuperStar);
            m_CurrentState.SetState(WorldStateKey.HasFullHP, HP == Match.instance.GlobalSetting.MaxHP);
            var oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank == null || oppTank.IsDead)
            {
                m_CurrentState.SetState(WorldStateKey.HasEnemy, false);
            }
            else
            {
                m_CurrentState.SetState(WorldStateKey.HasEnemy, true);
            }
        }
        
        private void UpdateGoal()
        {
            m_NextGoal.Clear();
            //select goal
            if (m_CurrentState.GetState<bool>(WorldStateKey.SuperStarAvailable))
            {
                m_NextGoal.SetState(WorldStateKey.ScoreIncreased, true);
            }
            else if (HP < 30)
            {
                m_NextGoal.SetState(WorldStateKey.HasFullHP, true);
            }
            else if (m_CurrentState.GetState<bool>(WorldStateKey.HasEnemy))
            {
                var oppTank = Match.instance.GetOppositeTank(Team);
                if (oppTank == null || oppTank.IsDead)
                {
                    m_NextGoal.SetState(WorldStateKey.ScoreIncreased, true);
                }
                else
                {
                    m_NextGoal.SetState(WorldStateKey.EnemyKilled, true);
                }
            }
            else
            {
                m_NextGoal.SetState(WorldStateKey.ScoreIncreased, true);
            }
        }

        private void UpdatePlanner()
        {
            //check if goal changed and if the action machine is not running
            if (m_Goal.Equals(m_NextGoal) && m_ActionMachine.IsRunning)
            {
                return;
            }
            m_Goal = m_NextGoal.Clone(); //change goal and re-plan
            m_Plan = m_Planner.Plan(m_CurrentState, m_Goal, m_Plan);
            if (m_Plan.Count > 0)
            {
                m_ActionMachine.AddActionList(m_Plan);
            }
            else
            {
                m_ActionMachine.Clear();
            }
        }

        private void UpdateAction()
        {
            //fire
            var oppTank = Match.instance.GetOppositeTank(Team);
            if (oppTank != null && !oppTank.IsDead)
            {
                TurretTurnTo(oppTank.Position);
                if (CanFire() && CanSeeOthers(oppTank))
                {
                    Fire();
                }
            }
            m_ActionMachine.Update();
        }

#if UNITY_EDITOR
        private GUIStyle m_ScoreStyle;
#endif
        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();
#if UNITY_EDITOR
            if (m_ScoreStyle == null)
            {
                m_ScoreStyle = new GUIStyle();
                m_ScoreStyle.normal.textColor = Color.yellow;
                m_ScoreStyle.fontSize = 16;
                m_ScoreStyle.fontStyle = FontStyle.Bold;
            }
            string plan = string.Empty;
            if (m_Plan.Count > 0)
            {
                for(int i = 0; i < m_Plan.Count; i++)
                {
                    plan += m_Plan[i].GetType().Name;
                    if (i < m_Plan.Count - 1)
                    {
                        plan += "->";
                    }
                }
            }
            string score = $"{m_Goal}({plan})\n{m_ActionMachine}";
            Handles.Label(Position + Forward * 5, score, m_ScoreStyle);
#endif
        }
    }
}