using Main;
using ZJQ;
using UnityEngine;


public class idleState : FiniteStateMachine
{
    #region
    public override void action()
    {
        //如果没血了
        if (_obj.HP <= 40) {
            _obj.Move(Match.instance.GetRebornPos(_obj.Team));
        }
    }

    public override void enterState(MyTank obj)
    {
        _obj = obj;
    }



    public override void exitState(Tank enemey)
    {

        if (_obj.CanSeeOthers(_obj.enemy))
        {
            Debug.Log("开始攻击");
            _obj.switchState(_obj.attack);
        }

        if (!_obj.isNoStar())
        {
            Debug.Log("开始吃星星");
            _obj.switchState(_obj.finding);
        }
    }


    #endregion
}
