using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class AgentMovement : MonoBehaviour
{
    // trajectory
    [SerializeField] private Transform trajectory;
    private Transform[] waypoints;
    public int futureWaypointIndex = 1;

    // control avatar
    [SerializeField] private Transform futureAvatar;
    [SerializeField] private Transform futureHeadOffset;

    public float futureSpeed = 1f;

    //connecting to the CurrentMovement
    public UnityEvent onMovementEnd;

    //VLM/LLM
    [HideInInspector] public bool flag = false;
    void Start()
    {
        InitReferences();
        InitPosAndRot();

        StartCoroutine(MoveToWayPoints_Future());
    }
    void InitReferences()
    {

        print("Hello singleFutureMovement");

        trajectory = this.transform.parent.Find("Trajectory");
        if (trajectory == null)
        {
            print("Check the trajectory's name plz.");
        }
        waypoints = new Transform[trajectory.childCount];
        for (int i = 0; i < trajectory.childCount; i++)
        {
            waypoints[i] = trajectory.GetChild(i);
        }
        futureAvatar = this.transform;
        futureHeadOffset = this.transform.parent.Find("ControlHeadOffset_Single_Future");
        if (futureHeadOffset != null)
        {
            print("Check the future head offset's name plz.");
        }
    }
    void InitPosAndRot()
    {
        futureAvatar.transform.position = waypoints[1].position;
        futureHeadOffset.transform.position = new Vector3(futureAvatar.position.x, futureHeadOffset.transform.position.y, futureAvatar.position.z);

        Vector3 initialDirection = (waypoints[2].position - waypoints[1].position).normalized;
        Quaternion initialRotation = Quaternion.LookRotation(initialDirection);

        futureAvatar.transform.rotation = initialRotation;
        futureHeadOffset.transform.rotation = initialRotation;
    }

    private IEnumerator MoveToWayPoints_Future()
    {
        while (futureWaypointIndex < waypoints.Length)
        {
            Transform targetWaypoint = null;

            try
            {
                targetWaypoint = waypoints[futureWaypointIndex + 1];
            }
            catch (System.IndexOutOfRangeException)
            {
                print("Waypoint index out of range. Ending Movement");
                onMovementEnd?.Invoke();
                this.transform.gameObject.SetActive(false);
                break;
            }

            Vector3 direction = targetWaypoint.position - futureAvatar.transform.position;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            futureAvatar.rotation = targetRotation;

            yield return StartCoroutine(MoveToTarget_Future(targetWaypoint));

            futureWaypointIndex++;
        }

    }
    private IEnumerator MoveToTarget_Future(Transform targetWaypoint)
    {
        while (Vector3.Distance(futureAvatar.transform.position, targetWaypoint.position) > 0.1f)
        {
            Vector3 direction = (waypoints[futureWaypointIndex + 1].position - futureAvatar.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            Quaternion combinedRotation = futureHeadOffset.rotation * targetRotation;

            if (Vector3.Distance(futureAvatar.transform.position, targetWaypoint.position) < 2.0f && direction != Vector3.zero)
            {
                futureAvatar.rotation = Quaternion.RotateTowards(futureAvatar.rotation, targetRotation, 15f * Time.deltaTime);
            }
            futureAvatar.position = Vector3.MoveTowards(futureAvatar.position, targetWaypoint.position, futureSpeed * Time.deltaTime);
            futureHeadOffset.transform.position = new Vector3(futureAvatar.position.x, futureHeadOffset.transform.position.y, futureAvatar.position.z);

            float dist = Vector3.Distance(futureAvatar.transform.position, targetWaypoint.position);
            if (dist <= 0.1f)
                break;


            yield return null;
        }
    }
}
