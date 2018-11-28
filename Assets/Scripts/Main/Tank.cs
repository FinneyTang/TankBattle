using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Tank : MonoBehaviour
{
    private NavMeshAgent m_Agent;
    private float m_LastTime = 0;
	void Start ()
    {
        m_Agent = this.GetComponent<NavMeshAgent>();
    }
	// Update is called once per frame
	void Update ()
    {
        if(Time.time > m_LastTime)
        {
            if(ApproachNextDestination())
            {
                m_LastTime = Time.time + 5;
            }
        }
    }
    bool ApproachNextDestination()
    {
        NavMeshPath path = new NavMeshPath();
        if (m_Agent.CalculatePath(new Vector3(Random.Range(-50, 50), 0, Random.Range(-50, 50)), path))
        {
            m_Agent.path = path;
            return true;
        }
        return false;
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(m_Agent.destination, 0.5f);
        Gizmos.DrawLine(m_Agent.destination, gameObject.transform.position);
    }
}
