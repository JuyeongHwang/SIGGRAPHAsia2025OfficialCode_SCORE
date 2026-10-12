using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class SigneFutureMovement : MonoBehaviour
{
    public bool useVLM = false;
    // trajectory
    [SerializeField] private Transform trajectory;
    private Transform[] waypoints;
    public int futureWaypointIndex = 0;

    // control avatar
    [SerializeField] private Transform futureAvatar;
    [SerializeField] private Transform futureHeadOffset;

    public float futureSpeed = 1f;

    //connecting to the CurrentMovement
    //public UnityEvent onMovementEnd;
    public SingleCurrentMovement currentAvatar;

    //Image
    [Header("View Managing")]
    public TextMeshProUGUI ImageText;
    public Texture2D novelView = null;
    public SingleFutureCameraVisionSensor sensor;
    public bool saveImages = false;
    [SerializeField] private int count = 0;
    private List<string> capturedImages_Future = new List<string>();
    public float diffPercentCoef = 10f;

    //VLM/LLM
    public bool flag = false;

    // Trajectory clock: advances only while the agent actually walks (paused during VLM/LLM calls),
    // so plan time stamps can be replayed by the current agent at the same point of the trajectory.
    public float trajectoryTime = 0f;
    [Tooltip("PEM capture period in seconds (paper: 0.2 s)")]
    public float captureInterval = 0.2f;
    private float captureTimer = 0f;
    SingleFutureVLMLLMConnector connector;

    //
    List<Vector3> decisionPoints = new List<Vector3>();
    void Start()
    {
        
        InitReferences();
        InitPosAndRot();
        AgentManaver.Instance.futureAgents.Add(this);
        novelView = sensor.CaptureRawTexture(count, futureWaypointIndex, saveImages);

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
        sensor = futureHeadOffset.Find("AgentCamera").GetComponent<SingleFutureCameraVisionSensor>();
        if (sensor != null)
        {
            print("Check the future head offset's sensor plz.");
        }

        ImageText = this.transform.Find("Canvas").Find("ImageText").GetComponent<TextMeshProUGUI>();
        if(ImageText != null)
        {
            print("Check the imageText");
        }

        connector = this.transform.GetComponent<SingleFutureVLMLLMConnector>();
        if(connector != null)
        {
            print("Check the single future vlm/llm connector");
        }
    }
    void InitPosAndRot()
    {
        futureAvatar.transform.position = waypoints[futureWaypointIndex].position;
        futureHeadOffset.transform.position = new Vector3(futureAvatar.position.x, futureHeadOffset.transform.position.y, futureAvatar.position.z);

        Vector3 initialDirection = (waypoints[futureWaypointIndex+1].position - waypoints[futureWaypointIndex].position).normalized;
        Quaternion initialRotation = Quaternion.LookRotation(initialDirection);

        futureAvatar.transform.rotation = initialRotation;
        futureHeadOffset.transform.rotation = initialRotation;
    }


    public float rotationSpeed = 60f;

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
                // onMovementEnd?.Invoke();

                AgentManaver.Instance.endFutureMovement += 1;
                print("If current agent didn't start, Check the onMovementEnd Event");
                this.transform.gameObject.SetActive(false);
                break;
            }

            //Vector3 direction = targetWaypoint.position - futureAvatar.transform.position;
            //Quaternion targetRotation = Quaternion.LookRotation(direction);
            //futureAvatar.rotation = targetRotation;

            //=======
            yield return StartCoroutine(MoveToTarget_Future(targetWaypoint));

            futureWaypointIndex++;
        }

    }


    private IEnumerator MoveToTarget_Future(Transform targetWaypoint)
    {
        capturedImages_Future.Clear();
        while (Vector3.Distance(futureAvatar.transform.position, targetWaypoint.position) > 0.1f)
        {

            if (flag)
            {

                //futureAvatar.position = Vector3.MoveTowards(futureAvatar.position, targetWaypoint.position, futureSpeed * Time.deltaTime);
                //futureHeadOffset.transform.position = new Vector3(futureAvatar.position.x, futureHeadOffset.transform.position.y, futureAvatar.position.z);

                Vector3 direction = (targetWaypoint.position - futureAvatar.position).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    if (Vector3.Distance(futureAvatar.transform.position, targetWaypoint.position) < 5.0f)
                    {
                        futureAvatar.rotation = Quaternion.RotateTowards(
                            futureAvatar.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                    }
                }

                futureAvatar.position = Vector3.MoveTowards(
                    futureAvatar.position, targetWaypoint.position, futureSpeed * Time.deltaTime);
                trajectoryTime += Time.deltaTime;

                futureHeadOffset.transform.position = new Vector3(
                    futureAvatar.position.x, futureHeadOffset.transform.position.y, futureAvatar.position.z);
                
                
                captureTimer += Time.deltaTime;
                if(useVLM)
                    CalcNovelView();

            }


            float dist = Vector3.Distance(futureAvatar.transform.position, targetWaypoint.position);
            if (dist <= 0.1f)
                break;

            yield return null;
        }
    }

    private float vlmCooldownUntil = 0f;
    public void StartVlmCooldown(float seconds)
    {
        vlmCooldownUntil = Time.time + seconds;
    }

    private void CalcNovelView()
    {
        if (Time.time < vlmCooldownUntil) return;

        if (captureTimer >= captureInterval)
        {
            captureTimer = 0.0f;
            count++;

            //1. 이미지 저장
            Texture2D currentImage = sensor.CaptureRawTexture(count, futureWaypointIndex, saveImages);
            string base64Image = Convert.ToBase64String(currentImage.EncodeToPNG());
            capturedImages_Future.Add(base64Image);

            // 2. 이미지가 얼마나 다른가?
            if (novelView != null && flag)
            {
                float diffPercent = ComputeSSIM(novelView, currentImage);
                ImageText.text = diffPercent.ToString();
                ImageText.color = Color.white;

                if (diffPercent > diffPercentCoef)
                {
                    ImageText.color = Color.red;
                    novelView = currentImage;
                    //vlm 호출 (send a copy: frames captured since the last novel view)
                    connector.SendVLMToGeminiSplit_Future(new List<string>(capturedImages_Future));
                    capturedImages_Future.Clear();
                    decisionPoints.Add(this.transform.position);
                }
            }

        }

    }

    //private void OnDrawGizmos()
    //{
    //    Gizmos.color = Color.red;
    //    if (decisionPoints.Count > 0)
    //    {
    //        for (int i = 0; i < decisionPoints.Count; i++)
    //        {
    //            Gizmos.DrawSphere(decisionPoints[i]+Vector3.up, 0.05f);
    //        }
    //    }
    //}

    #region UTILS
    public static float ComputeSSIM(Texture2D tex1, Texture2D tex2)
    {
        if (tex1.width != tex2.width || tex1.height != tex2.height)
        {
            UnityEngine.Debug.LogError("Textures must have the same dimensions to compute SSIM.");
            return 0f;
        }

        int width = tex1.width;
        int height = tex1.height;
        int N = width * height;

        // Get pixel data
        Color[] pixels1 = tex1.GetPixels();
        Color[] pixels2 = tex2.GetPixels();

        // Convert to grayscale and compute means
        float[] gray1 = new float[N];
        float[] gray2 = new float[N];
        float mu1 = 0f, mu2 = 0f;

        for (int i = 0; i < N; i++)
        {
            gray1[i] = pixels1[i].grayscale;
            gray2[i] = pixels2[i].grayscale;
            mu1 += gray1[i];
            mu2 += gray2[i];
        }

        mu1 /= N;
        mu2 /= N;

        // Compute variances and covariance
        float sigma1 = 0f, sigma2 = 0f, sigma12 = 0f;

        for (int i = 0; i < N; i++)
        {
            float d1 = gray1[i] - mu1;
            float d2 = gray2[i] - mu2;
            sigma1 += d1 * d1;
            sigma2 += d2 * d2;
            sigma12 += d1 * d2;
        }

        sigma1 /= (N - 1);
        sigma2 /= (N - 1);
        sigma12 /= (N - 1);

        // Constants to stabilize the division with weak denominators
        float C1 = Mathf.Pow(0.01f, 2);
        float C2 = Mathf.Pow(0.03f, 2);

        // SSIM formula
        float numerator = (2f * mu1 * mu2 + C1) * (2f * sigma12 + C2);
        float denominator = (mu1 * mu1 + mu2 * mu2 + C1) * (sigma1 + sigma2 + C2);
        return (1 - numerator / denominator) * 100f;
    }

    #endregion
}
