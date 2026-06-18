using Main;
using UnityEngine;

namespace MXF
{
    /// <summary>
    ///     Tank 移动扩展方法
    /// </summary>
    public static class Movement
    {
        /// <summary>
        ///     沿世界方向设置目标点: target = Position + worldDirection * radius.
        /// </summary>
        public static void MoveInDirection(this Tank tank, Vector3 worldDirection, float radius = 15f)
        {
            Vector3 target = tank.Position + worldDirection.normalized * radius;
            tank.Move(target);
        }
    }
}
