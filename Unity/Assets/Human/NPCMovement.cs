using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCMovement : MonoBehaviour
{

    // trajectory
    [SerializeField] private Transform trajectory;
    private Transform[] waypoints;
    public int currentWaypointIndex = 0;

    // control avatar
    [SerializeField] private Transform currentAvatar;

    public float currentSpeed = 1f;
    public float rotationSpeed = 60f;


    void Start()
    {
        InitReferences();
        InitPosAndRot();

        StartCoroutine(MoveToWayPoints_Current());
    }

    
    void InitReferences()
    {

        print("Hello singleCurrentMovement");

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
        currentAvatar = this.transform;

    }
    void InitPosAndRot()
    {
        currentAvatar.transform.position = waypoints[currentWaypointIndex].position;

        Vector3 initialDirection = (waypoints[currentWaypointIndex + 1].position - waypoints[currentWaypointIndex].position).normalized;
        Quaternion initialRotation = Quaternion.LookRotation(initialDirection);

        currentAvatar.transform.rotation = initialRotation;
    }

    private IEnumerator MoveToWayPoints_Current()
    {
        while (currentWaypointIndex < waypoints.Length)
        {
            Transform targetWaypoint = null;

            try
            {
                targetWaypoint = waypoints[currentWaypointIndex + 1];
            }
            catch (System.IndexOutOfRangeException)
            {
                print("Waypoint index out of range. Ending Movement");
                break;
            }


            yield return StartCoroutine(MoveToTarget(targetWaypoint));


            currentWaypointIndex++;
        }

    }

    private float vlmTimer = 0f;
    private float vlmCooldown = 0.2f; // 2초마다 한 번 전송
    private List<string> capturedImages_Current = new List<string>();

    private IEnumerator MoveToTarget(Transform targetWaypoint)
    {
        bool hasSent = false;
        while (Vector3.Distance(currentAvatar.transform.position, targetWaypoint.position) > 0.1f)
        {
            Vector3 direction = (waypoints[currentWaypointIndex + 1].position - currentAvatar.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            if (Vector3.Distance(currentAvatar.transform.position, targetWaypoint.position) < 5.0f && direction != Vector3.zero)
            {
                currentAvatar.rotation = Quaternion.RotateTowards(currentAvatar.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            currentAvatar.position = Vector3.MoveTowards(currentAvatar.position, targetWaypoint.position, currentSpeed * Time.deltaTime);

            float dist = Vector3.Distance(currentAvatar.transform.position, targetWaypoint.position);
            if (dist <= 0.1f)
                break;


            yield return null;
        }
    }


}
