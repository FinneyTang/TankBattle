using UnityEngine;

namespace MXF
{
    /// <summary>
    ///     移动对象的结构体: 描述GameObject的瞬时 XZ 平面运动状态,
    ///     供 AIAttack 的弹道预判使用, 包含位置、移动方向单位向量和速率.
    /// </summary>
    public class MoveObject
    {
        public Vector2 dir;
        public Vector2 pos;
        public float speed;
    }
}
