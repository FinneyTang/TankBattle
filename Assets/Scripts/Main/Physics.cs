using UnityEngine;

namespace Main
{
    public static class PhysicsUtils
    {
        public static readonly int LayerMask_Collsion = LayerMask.GetMask("Layer_Fire", "Layer_StaticObject");
        public static readonly string Tag_Fire = "Fire";

        public static bool IsFireCollider(Collider col)
        {
            return col.CompareTag(Tag_Fire);
        }
    }
}
