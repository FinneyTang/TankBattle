using UnityEngine;

namespace Main
{
    public class Billboard : MonoBehaviour
    {
        private Camera m_MainCamera;

        private void Start()
        {
            m_MainCamera = Camera.main;
        }

        private void Update()
        {
            transform.LookAt(transform.position + m_MainCamera.transform.rotation * Vector3.forward,
                m_MainCamera.transform.rotation * Vector3.up);
        }
    }
}