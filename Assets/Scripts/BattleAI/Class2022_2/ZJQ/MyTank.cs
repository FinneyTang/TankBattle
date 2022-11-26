using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Main;
using UnityEngine;
using UnityEngine.AI;

namespace ZJQ {
    public class MyTank : Tank
    {
        public idleState idle = new idleState();
        public findStarState finding = new findStarState();
        public attackState attack = new attackState();
        public FiniteStateMachine curState;
        public Tank enemy = null;
        public Main.Match.MatchSetting gameSetting;
        public blackBoard treeBoard = new blackBoard();
        public override string GetName()
        {
            return "ZJQ";
        }

        protected override void OnAwake()
        {
            base.OnAwake();
            switchState(idle);
            gameSetting = new Main.Match.MatchSetting();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            enemy = Main.Match.instance.GetOppositeTank(Team);
            curState.action();
            curState.exitState(enemy);
        }

        protected override void OnReborn()
        {
            base.OnReborn();
            switchState(idle);
        }

        public void switchState(FiniteStateMachine changeState) {
            curState = changeState;
            curState.enterState(this);
        }

        //场上还有小星星？
        public bool isNoStar() {
            return Main.Match.instance.GetStars().Count == 0 ? true : false;
        }

        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();
            Gizmos.DrawWireSphere(this.Position + this.Forward * 3f + Vector3.up * 3f, 2.5f);

        }

    }
}
