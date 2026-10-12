using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using static System.Collections.Specialized.BitVector32;

public class SingleCurrentMovement : MonoBehaviour
{
    public bool useVLM = false;
    //private void Awake()
    //{
    //    this.transform.gameObject.SetActive(false);
    //}
    public void HandleMovementEnd()
    {
        this.transform.gameObject.SetActive(true);
        UnityEngine.Debug.Log("Future Movement finished");
    }

    [SerializeField] private SingleFutureCurrentAgentData agentData;
    // trajectory
    [SerializeField] private Transform trajectory;
    private Transform[] waypoints;
    public int currentWaypointIndex = 0;

    public Transform Triggers;


    // control avatar
    [SerializeField] private Transform currentAvatar;
    [SerializeField] private Transform currentHeadOffset;
    [SerializeField] private SingleCurrentHeadController currentHeadController;
    [SerializeField] private SingleCurrentCameraSensor sensor;
    [SerializeField] private SingleCurrentVLMLLMConnector connector;

    public float currentSpeed = 1f;
    [HideInInspector] public string recorderName = null; // set by SingleAgentRecorder (csvOutName)
    public float rotationSpeed = 60f;

    //VLM/LLM
    [HideInInspector] public bool flag = false;
    public bool isReached = false;
    // 
    public string curAction = "keep";

    // ---- Time-step scheduling (paper) ----
    // The current agent replays the trajectory and executes each plan entry when its own
    // trajectory clock reaches the entry's time stamp. RES (FastVLM) validates the entry
    // `resLookAhead` seconds before it is displayed.
    public float trajectoryTime = 0f;
    public float resLookAhead = 2.0f;
    [Tooltip("Speed of the DPS (future) agent; plan times are rescaled when it differs from currentSpeed")]
    public float planSpeed = -1f; // <0: read from the SigneFutureMovement in the same agent

    void Start()
    {


        InitReferences();
        InitPosAndRot();

        StartCoroutine(MoveToWayPoints_Current());
    }

    private void Update()
    {
        Vector2 diff = GetDiffValue();
        if (diff.y <= 90 && diff.y >= -90)
        {

        }
        else
        {
            currentHeadController.HandleFutureAction("forward");
        }
        if (diff.x <= 40 && diff.x >= -40)
        {

        }
        else
        {
            currentHeadController.HandleFutureAction("forward");
        }


        //if (curAction == "" || curAction == "keep")
        //{
        //    if (agentData.futureActionsByPos.TryGetValue(RoundedPosition(this.transform.position), out string action))
        //    {
        //        print($"MATCH: {RoundedPosition(this.transform.position)}, Action: {action}");
        //        currentHeadController.HandleFutureAction(action);
        //        agentData.currentAction.Add(action);
        //    }
        //    else
        //    {
        //        //print($"Not found: {RoundedPosition(this.transform.position)}");
        //    }
        //}
        //else
        //{
        //    currentHeadController.HandleFutureAction(curAction);
        //    agentData.currentAction.Add(curAction);
        //}

    }

    public int triggerId = 0;
    private void OnTriggerEnter(Collider other)
    {
        if (agentData.scheduleMode != ScheduleMode.SpatialTrigger) return; // legacy path only

        if (other.tag == "trigger")
        {
            string _futureAction = other.GetComponent<futureData>().futureAction;
            print("Action: "+_futureAction);
            currentHeadController.HandleFutureAction(_futureAction);
            triggerId++;
            if (useVLM)
            {
                connector.SendVLMToGeminiSplit(capturedImages_Current, _futureAction);

            }
        }
    }

    public void SetHeadAction(string action)
    {
        currentHeadController.HandleFutureAction(action);
        agentData.currentAction.Add(action);
    }

    // Convert a DPS time stamp to this agent's clock (same arc length along the trajectory).
    float ToExecutionTime(PlanEntry e)
    {
        float speed = planSpeed;
        if (speed <= 0f)
        {
            var future = this.transform.parent.GetComponentInChildren<SigneFutureMovement>(true);
            speed = future != null ? future.futureSpeed : currentSpeed;
        }
        return currentSpeed > 0f ? e.time * speed / currentSpeed : e.time;
    }

    void UpdateSchedule()
    {
        if (agentData.scheduleMode != ScheduleMode.TimeStep) return;

        foreach (var e in agentData.plan)
        {
            float tExec = ToExecutionTime(e);

            // RES: validate the pre-planned pose ~2 s before it is displayed
            if (useVLM && !e.resRequested && trajectoryTime >= tExec - resLookAhead)
            {
                e.resRequested = true;
                e.resQueryTime = trajectoryTime;
                connector.RequestValidation(new List<string>(capturedImages_Current), e);
            }

            if (!e.executed && trajectoryTime >= tExec)
            {
                e.executed = true;
                e.executedTime = trajectoryTime;
                string action = (!string.IsNullOrEmpty(e.resAction) && e.resAction != "keep") ? e.resAction : e.action;
                e.executedAction = action;
                print($"[t={trajectoryTime:F2}s] Action: {action} (planned: {e.action})");
                SetHeadAction(action);
                triggerId++;
            }
        }
    }

    // Called by RES when FastVLM answers after the entry was already executed.
    public void OnLateValidation(PlanEntry e)
    {
        if (!e.executed || string.IsNullOrEmpty(e.resAction) || e.resAction == "keep") return;
        e.executedAction = e.resAction;
        SetHeadAction(e.resAction);
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
        currentHeadOffset = this.transform.parent.Find("ControlHeadOffset_Single_Current");
        if (currentHeadOffset != null)
        {
            print("Check the current head offset's name plz.");
        }
        currentHeadController = this.GetComponent<SingleCurrentHeadController>();
        if(currentHeadController != null)
        {
            print("Check the single current head controller");
        }

        sensor = currentHeadOffset.Find("AgentCamera").GetComponent<SingleCurrentCameraSensor>();
        if(sensor != null)
        {
            //print();
        }
        connector = this.GetComponent<SingleCurrentVLMLLMConnector>();



    }
    void InitPosAndRot()
    {
        currentAvatar.transform.position = waypoints[currentWaypointIndex].position;
        currentHeadOffset.transform.position = new Vector3(currentAvatar.position.x, currentHeadOffset.transform.position.y, currentAvatar.position.z);

        Vector3 initialDirection = (waypoints[currentWaypointIndex+1].position - waypoints[currentWaypointIndex].position).normalized;
        Quaternion initialRotation = Quaternion.LookRotation(initialDirection);

        currentAvatar.transform.rotation = initialRotation;
        currentHeadOffset.transform.rotation = initialRotation;

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
                isReached = true;
                if (recorderName != null) agentData.ExportPlan(recorderName);
                //this.transform.gameObject.SetActive(false);
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
        
        while (Vector3.Distance(currentAvatar.transform.position, targetWaypoint.position) > 0.1f)
        {
            Vector3 direction = (waypoints[currentWaypointIndex + 1].position - currentAvatar.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            Quaternion combinedRotation = currentHeadOffset.rotation * targetRotation;

            if (Vector3.Distance(currentAvatar.transform.position, targetWaypoint.position) < 5.0f && direction != Vector3.zero)
            {
                currentAvatar.rotation = Quaternion.RotateTowards(currentAvatar.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            vlmTimer += Time.deltaTime;

            if (vlmTimer >= vlmCooldown)
            {
                vlmTimer = 0f;
                capturedImages_Current.Clear();

                Texture2D currentImage = sensor.CaptureRawTexture(currentWaypointIndex);
                string base64Image = Convert.ToBase64String(currentImage.EncodeToPNG());
                capturedImages_Current.Add(base64Image);
            }

            currentAvatar.position = Vector3.MoveTowards(currentAvatar.position, targetWaypoint.position, currentSpeed * Time.deltaTime);
            currentHeadOffset.transform.position = new Vector3(currentAvatar.position.x, currentHeadOffset.transform.position.y, currentAvatar.position.z);
            trajectoryTime += Time.deltaTime;
            UpdateSchedule();

            float dist = Vector3.Distance(currentAvatar.transform.position, targetWaypoint.position);
            if (dist <= 0.1f)
                break;


            yield return null;
        }
    }


    #region UTILITYS
    Vector3 RoundedPosition(Vector3 pos, int decimals = 2)
    {
        float factor = Mathf.Pow(10, decimals);
        return new Vector3(
            Mathf.Round(pos.x * factor) / factor,
            0,
            Mathf.Round(pos.z * factor) / factor
        );
    }


    public Vector2 GetDiffValue()
    {
        Vector2 headBodyDiff = GetSignedDelta(currentHeadController.currentHead.rotation, currentHeadController.currentBody.rotation);
        return new Vector2(headBodyDiff.x, headBodyDiff.y);
    }
    Vector2 GetSignedDelta(Quaternion head, Quaternion body)
    {
        Quaternion rel = Quaternion.Inverse(body) * head;

        Vector3 e = rel.eulerAngles;
        float pitch = e.x > 180f ? e.x - 360f : e.x;
        float yaw = e.y > 180f ? e.y - 360f : e.y;

        return new Vector2(pitch, yaw);
    }
    #endregion

}
