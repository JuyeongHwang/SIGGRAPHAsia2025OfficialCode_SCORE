using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class humanAgentMovement : MonoBehaviour
{
    // Existing waypoints and movement variables
    public Transform trajectory;
    public Transform[] waypoints;
    private int currentWaypointIndex = 0;
    public float movementSpeed = 2.0f;
    private bool isReached = false;
    private bool isReverse = false;

    // CSV file as public TextAsset
    public TextAsset csvFile;

    // New variables for head rotation data
    private List<Vector3> bodyPositions = new List<Vector3>();
    private List<Quaternion> headRotations = new List<Quaternion>();
    public Transform headTransform; // Reference to the agent's head
    public Transform bodyTransform; // Reference to the agent's head


    void Start()
    {
        // Initialize waypoints
        waypoints = new Transform[trajectory.childCount];
        if (!isReverse)
        {
            for (int i = 0; i < trajectory.childCount; i++)
            {
                waypoints[i] = trajectory.GetChild(i);
            }
        }

        // Load CSV data
        if (csvFile != null)
        {
            LoadRotationData();
        }
        else
        {
            Debug.LogError("CSV file not assigned! Please assign a CSV file in the inspector.");
        }

        // Check for head transform
        if (headTransform == null)
        {
            Debug.LogWarning("Head transform not assigned! Head rotation will not be applied.");
        }
        this.transform.position = waypoints[0].position;
        // Start movement
        StartCoroutine(MoveToWaypoints(bodyTransform, headTransform));
    }

    void LoadRotationData()
    {
        string[] lines = csvFile.text.Split('\n');

        // Skip header
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] values = line.Split(',');
            if (values.Length >= 7)
            {
                try
                {
                    // Parse body position
                    float bodyX = float.Parse(values[0]);
                    float bodyY = float.Parse(values[1]);
                    float bodyZ = float.Parse(values[2]);

                    // Parse head rotation
                    float headX = float.Parse(values[3]);
                    float headY = float.Parse(values[4]);
                    float headZ = float.Parse(values[5]);
                    float headW = float.Parse(values[6]);

                    bodyPositions.Add(new Vector3(bodyX, bodyY, bodyZ));
                    headRotations.Add(new Quaternion(headX, headY, headZ, headW));
                }
                catch (System.FormatException e)
                {
                    Debug.LogWarning($"Error parsing line {i}: {e.Message}");
                }
            }
        }

        Debug.Log($"Loaded {bodyPositions.Count} position/rotation data points from CSV");
    }

    // Find the closest head rotation based on current position
    private Quaternion GetClosestHeadRotation(Vector3 currentPosition)
    {
        if (bodyPositions.Count == 0) return Quaternion.identity;

        float closestDistance = float.MaxValue;
        int closestIndex = 0;

        for (int i = 0; i < bodyPositions.Count; i++)
        {
            float distance = Vector3.Distance(currentPosition, bodyPositions[i]);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return headRotations[closestIndex];
    }

    private IEnumerator MoveToWaypoints(Transform avatar, Transform offset)
    {
        while (currentWaypointIndex < waypoints.Length - 1)
        {
            Transform targetWaypoint = null;
            try
            {
                targetWaypoint = waypoints[currentWaypointIndex + 1];
            }
            catch (System.IndexOutOfRangeException)
            {
                Debug.LogWarning("Waypoint index out of range. Ending movement.");
                break;
            }

            //Vector3 direction = targetWaypoint.position - avatar.position;
            //Quaternion targetRotation = Quaternion.LookRotation(direction);
            //avatar.rotation = targetRotation;

            // Move to next point
            yield return StartCoroutine(MoveToTarget(targetWaypoint, avatar, offset));

            currentWaypointIndex++;
            if (Vector3.Distance(transform.position, waypoints[waypoints.Length - 2].position) < 1.0f)
            {
                isReached = true;
            }
        }
    }

    public float rotationSpeed = 60f;
    private IEnumerator MoveToTarget(Transform targetWaypoint, Transform avatar, Transform offset)
    {

        while (Vector3.Distance(avatar.position, targetWaypoint.position) > 0.1f)
        {
            // Move body
            avatar.position = Vector3.MoveTowards(avatar.position, targetWaypoint.position, movementSpeed * Time.deltaTime);
            offset.position = new Vector3(avatar.position.x, offset.position.y, avatar.position.z);

            Vector3 direction = (targetWaypoint.position - avatar.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                if (Vector3.Distance(avatar.transform.position, targetWaypoint.position) < 5.0f)
                {
                    avatar.rotation = Quaternion.RotateTowards(
                        avatar.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }

            avatar.position = Vector3.MoveTowards(
                avatar.position, targetWaypoint.position, movementSpeed * Time.deltaTime);

            offset.transform.position = new Vector3(
                avatar.position.x, offset.transform.position.y, avatar.position.z);


            // Apply head rotation based on current position
            if (headTransform != null && bodyPositions.Count > 0)
            {
                Quaternion headRotation = GetClosestHeadRotation(avatar.position);

                // body 기준 상대 회전을 계산
                Quaternion relativeToBody = bodyTransform.rotation * headRotation;

                // 부드럽게 회전 적용 (Slerp)
                headTransform.rotation = Quaternion.Slerp(headTransform.rotation, relativeToBody, Time.deltaTime * 10f);


            }


            yield return null;
        }
    }

    private void OnDrawGizmos()
    {
        //if (bodyPositions == null || bodyPositions.Count < 2)
        //    return;

        //Gizmos.color = Color.cyan;

        //for (int i = 0; i < bodyPositions.Count - 1; i++)
        //{
        //    Gizmos.DrawLine(bodyPositions[i], bodyPositions[i + 1]);
        //    Gizmos.DrawSphere(bodyPositions[i], 0.03f); // 작은 점
        //}

        //Gizmos.color = Color.red;
        //Gizmos.DrawSphere(bodyPositions[bodyPositions.Count - 1], 0.05f);

        //====

        if (trajectory == null || trajectory.childCount < 2)
            return;

        Gizmos.color = Color.green;

        for (int i = 0; i < trajectory.childCount - 1; i++)
        {
            Transform current = trajectory.GetChild(i);
            Transform next = trajectory.GetChild(i + 1);

            Gizmos.DrawLine(current.position, next.position);
            Gizmos.DrawSphere(current.position, 0.05f); // small marker
        }

        // 마지막 지점 강조
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(trajectory.GetChild(trajectory.childCount - 1).position, 0.07f);
    }
}
