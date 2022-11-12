using System.Collections.Generic;
using Main;
using UnityEngine;

namespace MLS
{
    public class MyTank:Tank
    {
        #region  Properties

        private readonly MoveController _moveController;
        private readonly ShootController _shootController;
        private readonly Conditions _condition;

        #endregion
        
        /// <summary>
        /// 初始化
        /// </summary>
        public MyTank()
        {
            _condition = new Conditions(this);
            _moveController = new MoveController(_condition);
            _shootController = new ShootController(_condition);
        }
        
        public override string GetName()
        {
            return "MLS";
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            _condition.UpdateConditions();
            _moveController.OnUpdate();
            _shootController.OnUpdate();
        }
        
        protected override void OnReborn()
        {
            base.OnReborn();
            //Time.timeScale = 3;
        }

        protected override void OnOnDrawGizmos()
        {
            base.OnOnDrawGizmos();
            
        }
    }
}