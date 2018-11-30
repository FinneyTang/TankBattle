using UnityEngine;

namespace Main
{
    public static class PhysicsUtils
    {
        public static readonly int MaxFieldSize = 100;

        public static readonly int LayerMaskCollsion = LayerMask.GetMask("Layer_Fire", "Layer_StaticObject");
        public static readonly int LayerMaskScene = LayerMask.GetMask("Layer_StaticObject");
        public static readonly int LayerMaskTank = LayerMask.GetMask("Layer_Fire");

        private static readonly string TagFire = "Fire";

        public static bool IsFireCollider(Collider col)
        {
            return col.CompareTag(TagFire);
        }
    }
}
