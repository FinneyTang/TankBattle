using UnityEngine;

namespace TsiU
{
    public class THoverCameraController : MonoBehaviour
    {
        private enum MouseButton { Left = 0, Right = 1, Middle = 2, None = 3 }
        private readonly string MouseHorizontalAxisName = "Mouse X";
        private readonly string MouseVerticalAxisName = "Mouse Y";
        private readonly string MouseScrollAxisName = "Mouse ScrollWheel";

        public Transform TargetObject               = null;
        public float CurrentPanAngle                = 0f;
        public float CurrentTiltAngle               = 30f;
        public float CurrentDistance                = 10f;
        public float MinPanAngle                    = 45f;
        public float MaxPanAngle                    = 360f;
        public float MinTiltAngle                   = 0f;
        public float MaxTiltAngle                   = 90f;
        public float PanMovementSensitivity         = 3f;
        public float TiltMovementSensitivity        = 3f;
        public float DistanceMovementSensitivity    = 3f;

        private Vector3 _lookAtPosition;
        void Start()
        {
            if(TargetObject != null)
            {
                _lookAtPosition = TargetObject.position;
            }
            else
            {
                _lookAtPosition = Vector3.zero;
            }
        }
        void Update()
        {
            if (Input.GetMouseButton((int)MouseButton.Left))
            {
                CurrentPanAngle += Input.GetAxis(MouseHorizontalAxisName) * PanMovementSensitivity;
                while (CurrentPanAngle > 360)
                {
                    CurrentPanAngle -= 360;
                }
                while (CurrentPanAngle < 0)
                {
                    CurrentPanAngle += 360;
                }
                CurrentTiltAngle += (Input.GetAxis(MouseVerticalAxisName) * TiltMovementSensitivity * -1);
            }
            CurrentDistance     = CurrentDistance + Input.GetAxis(MouseScrollAxisName) * DistanceMovementSensitivity * -1;
            CurrentPanAngle     = Mathf.Clamp(CurrentPanAngle, MinPanAngle, MaxPanAngle);
            CurrentTiltAngle    = Mathf.Clamp(CurrentTiltAngle, MinTiltAngle, MaxTiltAngle);

            float sinPan    = Mathf.Sin(CurrentPanAngle * Mathf.Deg2Rad);
            float cosPan    = Mathf.Cos(CurrentPanAngle * Mathf.Deg2Rad);
            float sinTilt   = Mathf.Sin(CurrentTiltAngle * Mathf.Deg2Rad);
            float cosTilt   = Mathf.Cos(CurrentTiltAngle * Mathf.Deg2Rad);

            Vector3 newPos = new Vector3();
            newPos.x = _lookAtPosition.x + CurrentDistance * sinPan * cosTilt;
            newPos.y = _lookAtPosition.y + CurrentDistance * sinTilt;
            newPos.z = _lookAtPosition.z + CurrentDistance * cosPan * cosTilt;
            Vector3 forward = _lookAtPosition - newPos;
            Quaternion newRot = Quaternion.LookRotation(forward);
            this.transform.position = newPos;
            this.transform.rotation = newRot;
        }
    }
}
