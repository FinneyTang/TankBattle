using UnityEngine;

namespace Main
{
    public class RotateAroundLocalAxis : MonoBehaviour
    {
        public enum ERotateAxis
        {
            X, Y, Z
        }

        public ERotateAxis Axis = ERotateAxis.Z;
        public float Speed = 1f;

        private Vector3 m_RotationAxis;

        private void Start()
        {
            switch (Axis)
            {
                case ERotateAxis.X:
                    m_RotationAxis = Vector3.right;
                    break;
                case ERotateAxis.Y:
                    m_RotationAxis = Vector3.up;
                    break;
                case ERotateAxis.Z:
                    m_RotationAxis = Vector3.forward;
                    break;
                default:
                    m_RotationAxis = Vector3.forward;
                    break;
            }
        }

        private void Update()
        {
            transform.Rotate(m_RotationAxis, Speed * Time.deltaTime, Space.Self);
        }
    }
}
