using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

public class AgentSpeedController : MonoBehaviour
{
    private NavMeshAgent m_Agent;
    private Vector3[] m_PathCorners = new Vector3[3];

    [SerializeField] private Transform m_Target;

    private float MaxSpeedInStraightLine = 5.0f;
    [SerializeField] private float MaxSpeedAtTightCorner = 0.1f;
    [SerializeField] private float DistanceThreshold = 0.5f;

    void OnEnable()
    {
        if (m_Agent == null)
            m_Agent = GetComponent<NavMeshAgent>();

        if (m_Agent != null)
        {
            m_Agent.SetDestination(m_Target.position);
            MaxSpeedInStraightLine = m_Agent.speed;
        }
    }

    void Update()
    {
        if (m_Agent == null)
            return;

        int numCorners = m_Agent.path.GetCornersNonAlloc(m_PathCorners);

        if (numCorners > 2)
        {
            // m_PathCorners[0] is the agent position path so find the angle of the next corner.
            Vector3 first = (m_PathCorners[1] - m_PathCorners[0]).normalized;
            Vector3 second = (m_PathCorners[2] - m_PathCorners[1]).normalized;

            // A 90 degree turn requires the biggest reduction in speed.
            float speedFactor = Mathf.Clamp01(Vector3.Dot(first, second));

            // Only apply speed reduction based on angle if distance to next corner is less than DistanceThreshold.
            float distance = Vector3.Distance(m_PathCorners[0], m_PathCorners[1]);
            float distanceRatio = Mathf.Clamp01(distance / DistanceThreshold);

            // Pick a new max speed based on the upcoming turn.
            float angleMaxSpeed = Mathf.Lerp(MaxSpeedAtTightCorner, MaxSpeedInStraightLine, speedFactor);

            m_Agent.speed = Mathf.Lerp(angleMaxSpeed, MaxSpeedInStraightLine, distanceRatio);
        }
        else
        {
            m_Agent.speed = MaxSpeedInStraightLine;
        }
    }

    private void OnValidate()
    {
        MaxSpeedAtTightCorner = Mathf.Clamp(MaxSpeedAtTightCorner, 0.0f, MaxSpeedInStraightLine);
    }
}
