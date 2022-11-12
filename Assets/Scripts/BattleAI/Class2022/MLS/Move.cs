using System.Collections.Generic;
using Main;
using UnityEngine;

namespace MLS
{
    /// <summary>
    /// 移动策略
    /// 用类似状态机的思路实现
    /// </summary>

    //移动策略的分类
    public enum EMove
    {
        AvoidMove,
        StarFirst,
        GoHome,
        StayHome,
        ForSuperStar
    }
    
    //移动控制器
    public class MoveController
    {
        private MoveStrategy _currentMoveStrategy;
        private readonly Dictionary<EMove, MoveStrategy> _stateDict;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="conditions"></param>
        public MoveController(Conditions conditions)
        {
            _stateDict = new Dictionary<EMove, MoveStrategy>()
            {
                {EMove.AvoidMove,new AvoidMoveStrategy(conditions)},
                {EMove.StarFirst,new StarFirstStrategy(conditions)},
                {EMove.GoHome,new GoHome(conditions)},
                {EMove.StayHome,new StayHome(conditions)},
                {EMove.ForSuperStar,new ForSuperStar(conditions)},
            };
            _currentMoveStrategy = ChangeState(EMove.StarFirst);
        }

        private MoveStrategy ChangeState(EMove type)
        {
            MoveStrategy result;
            bool isExist = _stateDict.TryGetValue(type, out result);
            if (isExist)
                return result;
            else
                return null;
        }

        public void OnUpdate()
        {
            //评估当前条件
            var targetState = _currentMoveStrategy.Evaluate();
            
            bool changeState = targetState != _currentMoveStrategy.type;
            if (changeState)
            {
                //需要更换移动策略
                _currentMoveStrategy = ChangeState(targetState);
            }
            else
            {
                //执行当前条件的Update
                _currentMoveStrategy.Action();
            }
        }
    }
    
    //========================================
    //以下是各种移动的策略
    
    //状态基类
    public class MoveStrategy
    {
        public EMove type;
        protected readonly Tank tank;
        protected readonly Conditions conditions;

        public MoveStrategy(Conditions conditions)
        {
            Initial();
            this.conditions = conditions;
            tank = conditions.Self;
        }

        protected virtual void Initial() { }
        
        public virtual EMove Evaluate()
        {
            //评估方法,用于判断是否需要切换状态
            //判断是否需要回家补血
            //情况1：如果敌方坦克血量明显大于我方，且距离小于半场
            //情况2：敌人快复活了，坦克血量不满
            //情况3：1分40，为toCenter准备
            bool goHomeCondition =
                ( conditions.Self.HP < 60 
                  && conditions.Self.HP < conditions.Enemy.HP
                && Vector3.Distance( conditions.Enemy.Position , conditions.Self.Position) < Conditions.MaxDis/1.3f
                && (!conditions.Enemy.IsDead  ));
            goHomeCondition = goHomeCondition 
                              || (conditions.Enemy.IsDead 
                                  && conditions.Enemy.GetRebornCD(Time.time) <3.2f 
                                  && conditions.Self.HP <80);
            goHomeCondition = goHomeCondition 
                              || (conditions.Enemy.IsDead 
                                  && Match.instance.RemainingTime < 107
                                  && conditions.Self.HP <95);
            if (goHomeCondition)
            {
                return EMove.GoHome;
            }

            //to center拾取super star
            if (Match.instance.RemainingTime < 97 && Match.instance.RemainingTime > 89)
            {
                return EMove.ForSuperStar;
            }
            
            //根据时间，前往地图中间
            
            //已经取胜：
            //来自2021年的LGB的Tank
            //当我方分数-敌方分数>剩余时间，默认我方胜利
            var scoreDiff = conditions.Self.Score - conditions.Enemy.Score;
            if (scoreDiff > Match.instance.RemainingTime && scoreDiff >= 25)
            {
                return EMove.StayHome;
            }
            return type;
        }
        
        public virtual void Action() { }
    }
    
    public class StarTarget:MoveStrategy
    {
        public override void Action()
        {
            base.Action();
            //将权重最高的星星作为目标
            if (conditions.starInfos.Count != 0)
            {
                var target = conditions.GetBestStar();
                tank.Move(target.Position);
            }
        }

        public StarTarget(Conditions conditions) : base(conditions) { }
    }

    public class AvoidMoveStrategy : StarTarget
    {
        protected override void Initial()
        {
            type = EMove.AvoidMove;
        }

        //修正路线
        public AvoidMoveStrategy(Conditions conditions) : base(conditions) { }
    }

    public class StarFirstStrategy : StarTarget
    {
        protected override void Initial()
        {
            type = EMove.StarFirst;
        }

        public StarFirstStrategy(Conditions conditions) : base(conditions) { }
    }

    public class FindCover : MoveStrategy
    {
        
        public FindCover(Conditions conditions) : base(conditions) { }
    }

    public class ChaseToDie : MoveStrategy
    {
        public ChaseToDie(Conditions conditions) : base(conditions) { }
    }

    public class GoHome : MoveStrategy
    {
        protected override void Initial() { type = EMove.GoHome; }
        
        public override EMove Evaluate()
        {
            if (conditions.Self.HP < 100)
            {
                return type;
            }
            return EMove.StarFirst;
        }

        public override void Action()
        {
            conditions.Self.Move(Match.instance.GetRebornPos(conditions.Self.Team));
        }

        public GoHome(Conditions conditions) : base(conditions) { }
    }

    /// <summary>
    /// StayHome状态
    /// 即判定敌方与我方差距过大，直接龟家中
    /// </summary>
    public class StayHome : MoveStrategy
    {
        protected override void Initial() { type = EMove.StayHome; }
        
        //评估条件
        public override EMove Evaluate()
        {
            var scoreDiff = conditions.Self.Score - conditions.Enemy.Score;
            if (scoreDiff < 25)
            {
                return EMove.StarFirst;
            }
            return EMove.StayHome;
        }

        public override void Action()
        {
            conditions.Self.Move(Match.instance.GetRebornPos(conditions.Self.Team));
        }

        public StayHome(Conditions conditions) : base(conditions) { }
    }

    public class ForSuperStar : MoveStrategy
    {
        protected override void Initial() { type = EMove.ForSuperStar; }

        public override EMove Evaluate()
        {
            //通过时间和super是否吃到来判断是否退出该模式
            if (Match.instance.RemainingTime < 89)
            {
                bool notHasSuperStar = true;
                foreach (var pair in Match.instance.GetStars())
                {
                    Star s = pair.Value;
                    if (s.IsSuperStar)
                    {
                        notHasSuperStar = false;
                        break;
                    }
                }
                if (notHasSuperStar)
                {
                    return EMove.StarFirst;
                }
            }
            return type;
        }

        public override void Action()
        {
            conditions.Self.Move(new Vector3(0, 0.5f, 0));
        }

        public ForSuperStar(Conditions conditions) : base(conditions) { }
    }
}