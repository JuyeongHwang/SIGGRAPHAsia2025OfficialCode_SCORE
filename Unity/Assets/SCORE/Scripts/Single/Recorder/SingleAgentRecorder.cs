using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

public class SingleAgentRecorder : MonoBehaviour
{
    public Transform currentHeadRotation;
    public Transform bodyTransform; // bodyTransform 직접 지정 가능
    public SingleCurrentMovement currentMovement;
    public SigneFutureMovement futureMovement;

    [Tooltip("회전값을 저장할 간격(초)")]
    public float recordInterval = 1f;

    //[Tooltip("저장할 파일 이름 (Application.persistentDataPath 폴더)")]
    //public string saveFileName = "rotations.csv";

    private List<Quaternion> rotations = new List<Quaternion>();
    private List<Quaternion> body_rotations = new List<Quaternion>();
    private bool isRecording = false;
    private List<Vector2> diffs = new List<Vector2>();
    private List<Vector3> poss = new List<Vector3>();
    private List<float> times = new List<float>(); // trajectory clock of the current agent

    public string csvOutName = "";//sceneName_Score01로 할 것.

    private void Start()
    {
        if (currentMovement != null) currentMovement.recorderName = csvOutName; // plan json uses the same name
    }

    private void Update()
    {

        if (!currentMovement.gameObject.activeSelf && isRecording != true)
        {
            return;
        }

        if (currentMovement.currentWaypointIndex == 0 && !isRecording)
        {
            isRecording = true;
            InvokeRepeating(nameof(RecordRotation), 0f, recordInterval);
        }

        if (currentMovement.isReached)
        {
            StopRecordingAndSave();
        }

    }

    void RecordRotation()
    {
        if (!isRecording) return;


        print("Record");
        // 현재 로컬 회전값 저장
        //rotations.Add(currentHeadRotation.rotation);
        Quaternion headRot = currentHeadRotation.rotation;
        Quaternion bodyRot = bodyTransform.rotation;

        rotations.Add(headRot);

        Vector2 diff = GetSignedDelta(headRot, bodyRot);
        diffs.Add(diff);
 
        poss.Add(this.transform.position);
        times.Add(currentMovement.trajectoryTime);

        body_rotations.Add(bodyRot);
    }


    public void StopRecordingAndSave()
    {
        if (!isRecording) return;
        isRecording = false;
        CancelInvoke(nameof(RecordRotation));
        SaveRotationsToCSV();
    }

    private void SaveRotationsToCSV()
    {
        Directory.CreateDirectory(Application.dataPath + "/SCOREResult");
        string path = Application.dataPath + $"/SCOREResult/{csvOutName}.csv"; //.Combine(Application.persistentDataPath, saveFileName);

        using (var writer = new StreamWriter(path))
        {
            // 헤더 추가
            writer.WriteLine("HeadRotx,HeadRoty,HeadRotz,HeadRotw,HeadBodyposx, HeadBodyposy, HeadBodyposz,BodyRotx, BodyRoty, BodyRotz, BodyRotw, HeadDiffPitch,HeadDiffYaw,Time");

            for (int i = 0; i < rotations.Count; i++)
            {
                Quaternion q = rotations[i];
                Vector2 d = diffs[i];
                Vector3 p = poss[i];
                Quaternion bq = body_rotations[i];
                //q.x:F4
                writer.WriteLine($"{q.x:F4},{q.y:F4},{q.z:F4},{q.w:F4},{p.x:F4},{p.y:F4},{p.z:F4},{bq.x:F4},{bq.y:F4},{bq.z:F4},{bq.w:F4},{d.x:F2},{d.y:F2},{times[i]:F3}");
            }
        }

        Debug.Log($"[RotationRecorder] Saved {rotations.Count} rotations + diffs to:\n{path}");
    }


    Vector2 GetSignedDelta(Quaternion head, Quaternion body)
    {
        Quaternion rel = Quaternion.Inverse(body) * head;

        Vector3 e = rel.eulerAngles;
        float pitch = e.x > 180f ? e.x - 360f : e.x;
        float yaw = e.y > 180f ? e.y - 360f : e.y;

        return new Vector2(pitch, yaw);
    }
}
