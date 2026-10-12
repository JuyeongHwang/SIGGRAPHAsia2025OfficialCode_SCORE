using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class TrackRotation : MonoBehaviour
{
    public int agentId = -1;
    public Transform bodyTransform;
    public Transform headTransform;
    public Transform trajectory;

    //public string save_csv_name;
    [TextArea]
    public List<string> inputData; // 데이터를 문자열로 입력받는 변수

    public Vector3[] radianData; // 라디안 데이터를 저장할 배열

    int id = 0;

    public float moveSpeed = 1f;
    public float rotationSpeed = 30f;

    //public List<string> rotationData = new List<string>(); // 회전 데이터를 저장할 리스트

    void Start()
    {
        bodyTransform.position = trajectory.GetChild(0).transform.position;
        headTransform.position = new Vector3(bodyTransform.position.x, headTransform.position.y, bodyTransform.position.z) ;
        if (inputData.Count - 1 < agentId)
        {
            return;
        }
        // 문자열 데이터를 파싱하여 radianData 배열에 저장
        ParseInputData();

        // 각 라디안 값을 회전으로 변환하여 출력
        foreach (var radians in radianData)
        {
            Quaternion rotation = Quaternion.Euler(radians * Mathf.Rad2Deg); // 라디안을 각도로 변환 후 Quaternion 생성
            Debug.Log($"Radians: {radians} -> Rotation: {rotation.eulerAngles}");
        }

    }

    void Update()
    {
        // 방어: trajectory에 자식이 없거나 데이터가 비었으면 리턴
        if (trajectory == null || trajectory.childCount == 0 || radianData == null || radianData.Length == 0)
            return;

        // trajectory 자식 수와 radianData 길이 중 작은 쪽으로 진행 길이 결정
        int maxLen = Mathf.Min(trajectory.childCount, radianData.Length);
        int lastIdx = maxLen - 1;

        // 진행 가능한 범위를 벗어났으면 마지막 지점으로 고정 이동 + 저장/내보내기
        if (id >= maxLen)
        {
            Vector3 finalTargetPosition = trajectory.GetChild(lastIdx).position;
            Quaternion finalTargetRotation = Quaternion.Euler(radianData[lastIdx] * Mathf.Rad2Deg);

            bodyTransform.position = Vector3.MoveTowards(bodyTransform.position, finalTargetPosition, moveSpeed * Time.deltaTime);
            headTransform.position = new Vector3(bodyTransform.transform.position.x, headTransform.position.y, bodyTransform.position.z);

            Vector3 direction = (finalTargetPosition - bodyTransform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                Quaternion yOnlyRotation = Quaternion.Euler(0, lookRotation.eulerAngles.y, 0);
                bodyTransform.rotation = Quaternion.RotateTowards(bodyTransform.rotation, yOnlyRotation, rotationSpeed * Time.deltaTime);
            }

            //SaveRotationData();

            if (Vector3.Distance(bodyTransform.position, finalTargetPosition) < 0.01f && id == maxLen)
            {
                //ExportToCSV();
                id++; // 중복 저장 방지
            }
            return;
        }

        // 진행 중인 경우
        int targetIdx = Mathf.Clamp(id, 0, lastIdx);

        // ★ 여기서부터는 반드시 targetIdx로 접근 ★
        Vector3 currentTargetPosition = trajectory.GetChild(targetIdx).position;
        Quaternion currentTargetRotation = Quaternion.Euler(radianData[targetIdx] * Mathf.Rad2Deg);

        bodyTransform.position = Vector3.MoveTowards(bodyTransform.position, currentTargetPosition, moveSpeed * Time.deltaTime);
        headTransform.position = new Vector3(bodyTransform.transform.position.x, headTransform.position.y, bodyTransform.position.z);

        Vector3 dir = (currentTargetPosition - bodyTransform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            Quaternion yOnly = Quaternion.Euler(0, lookRot.eulerAngles.y, 0);
            bodyTransform.rotation = Quaternion.RotateTowards(bodyTransform.rotation, yOnly, rotationSpeed * Time.deltaTime);
        }

        Vector3 degEuler = radianData[targetIdx] * Mathf.Rad2Deg;
        degEuler = new Vector3(
            Mathf.Repeat(degEuler.x, 360f),
            Mathf.Repeat(degEuler.y, 360f),
            Mathf.Repeat(degEuler.z, 360f)
        );

        Quaternion relativeRot = Quaternion.Euler(degEuler);
        Quaternion targetHeadRot = bodyTransform.rotation * relativeRot;

        headTransform.rotation = Quaternion.RotateTowards(
            headTransform.rotation,
            targetHeadRot,
            rotationSpeed * Time.deltaTime
        );

        //SaveRotationData();

        if (Vector3.Distance(bodyTransform.position, currentTargetPosition) < 0.01f)
        {
            id += 1; // 다음 타겟으로
                     // maxLen을 다 소비했다면 다음 프레임에 위의 "마지막 지점" 분기로 들어가게 됨
            if (id == maxLen)
            {
                // 마지막 지점으로 도달 처리 트리거
            }
        }
    }

    //void Update()
    //{
    //    if (id < radianData.Length - 1)
    //    {
    //        int lastIdx = Mathf.Min(radianData.Length, trajectory.childCount) - 1;
    //        int targetIdx = Mathf.Clamp(id, 0, lastIdx);
    //        Vector3 targetPos = trajectory.GetChild(targetIdx).position;

    //        Vector3 currentTargetPosition = trajectory.GetChild(id).position;
    //        Quaternion currentTargetRotation = Quaternion.Euler(radianData[id] * Mathf.Rad2Deg);

    //        // 일정한 속도로 Body position 이동
    //        bodyTransform.position = Vector3.MoveTowards(bodyTransform.position, currentTargetPosition, moveSpeed * Time.deltaTime);
    //        headTransform.position = new Vector3(bodyTransform.transform.position.x, headTransform.position.y, bodyTransform.position.z);
    //        Vector3 direction = (currentTargetPosition - bodyTransform.position).normalized;

    //        // 수평 방향만 고려 (y = 0으로 고정)
    //        direction.y = 0;

    //        if (direction != Vector3.zero) // 방향 벡터가 0이 아닌 경우에만
    //        {
    //            Quaternion lookRotation = Quaternion.LookRotation(direction);
    //            Quaternion yOnlyRotation = Quaternion.Euler(0, lookRotation.eulerAngles.y, 0);

    //            bodyTransform.rotation = Quaternion.RotateTowards(bodyTransform.rotation, yOnlyRotation, rotationSpeed * Time.deltaTime);
    //        }

    //        Vector3 degEuler = radianData[targetIdx] * Mathf.Rad2Deg;
    //        degEuler = new Vector3(
    //            Mathf.Repeat(degEuler.x, 360f),
    //            Mathf.Repeat(degEuler.y, 360f),
    //            Mathf.Repeat(degEuler.z, 360f)
    //        );

    //        // body 기준 회전 = body의 회전에 상대 회전을 곱한다
    //        Quaternion relativeRot = Quaternion.Euler(degEuler);
    //        Quaternion targetHeadRot = bodyTransform.rotation * relativeRot;

    //        headTransform.rotation = Quaternion.RotateTowards(
    //            headTransform.rotation,
    //            targetHeadRot,
    //            rotationSpeed * Time.deltaTime
    //        );

    //        // 현재 Head rotation을 저장
    //        SaveRotationData();

    //        // Check if we reached the current target
    //        if (Vector3.Distance(bodyTransform.position, currentTargetPosition) < 0.01f)
    //        {
    //            id += 1; // Move to the next target
    //        }
    //    }
    //    else
    //    {
    //        // 모든 지점을 지나면 마지막 지점으로 계속 이동
    //        Vector3 finalTargetPosition = trajectory.GetChild(trajectory.childCount - 1).position;
    //        Quaternion finalTargetRotation = Quaternion.Euler(radianData[radianData.Length - 1] * Mathf.Rad2Deg);

    //        bodyTransform.position = Vector3.MoveTowards(bodyTransform.position, finalTargetPosition, moveSpeed * Time.deltaTime);
    //        headTransform.position = new Vector3(bodyTransform.transform.position.x, headTransform.position.y, bodyTransform.position.z);
    //        Vector3 direction = (finalTargetPosition - bodyTransform.position).normalized;

    //        // 수평 방향만 고려 (y = 0으로 고정)
    //        direction.y = 0;

    //        if (direction != Vector3.zero) // 방향 벡터가 0이 아닌 경우에만
    //        {
    //            Quaternion lookRotation = Quaternion.LookRotation(direction);
    //            Quaternion yOnlyRotation = Quaternion.Euler(0, lookRotation.eulerAngles.y, 0);

    //            bodyTransform.rotation = Quaternion.RotateTowards(bodyTransform.rotation, yOnlyRotation, rotationSpeed * Time.deltaTime);
    //        }

    //        // 현재 Head rotation을 저장
    //        SaveRotationData();

    //        // 마지막에 도달했으면 데이터를 저장
    //        if (Vector3.Distance(bodyTransform.position, finalTargetPosition) < 0.01f && id == radianData.Length - 1)
    //        {
    //            ExportToCSV();
    //            id++; // Prevent multiple saves
    //        }
    //    }
    //}

    private void ParseInputData()
    {

        try
        {
            // 문자열 데이터를 파싱
            string trimmedData = inputData[0].Replace("[", "").Replace("]", ""); // [] 제거
            string[] rows = trimmedData.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);

            if (rows.Length % 3 != 0)
            {
                Debug.LogError("Invalid data format: Data must be a multiple of 3.");
                return;
            }
            print("Radian " + agentId);
            radianData = new Vector3[rows.Length / 3];
            for (int i = 0; i < radianData.Length; i++)
            {
                float x = float.Parse(rows[i * 3]);
                float y = float.Parse(rows[i * 3 + 1]);
                float z = float.Parse(rows[i * 3 + 2]);
                radianData[i] = new Vector3(x, y, z);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to parse input data: {e.Message}");
        }
    }

    //private void SaveRotationData()
    //{
    //    Quaternion rotation = headTransform.rotation; //Camera.rotation; //
    //    string rotationString = $"{rotation.x},{rotation.y},{rotation.z},{rotation.w}";
    //    rotationData.Add(rotationString);
    //}

    //private void ExportToCSV()
    //{
    //    //string filePath = Path.Combine(Application.dataPath, $"/TRACK/{save_csv_name}.csv");
    //    string filePath = Path.Combine(Application.dataPath, "TRACK", save_csv_name + ".csv");
        
    //    List<string> lines = new List<string>();
    //    lines.Add("HeadRotx,HeadRoty,HeadRotz,HeadRotw");
    //    lines.AddRange(rotationData);

    //    File.WriteAllLines(filePath, lines);

    //    //File.WriteAllLines(filePath, rotationData);
    //    Debug.Log($"Rotation data exported to {filePath}");
    //}
}
