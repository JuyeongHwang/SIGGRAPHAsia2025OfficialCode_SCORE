using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public Transform target;
    public Transform pos1;   // 시작 위치
    public Transform pos2;   // 목표 위치
    public float duration = 2f; // 이동에 걸리는 시간

    public Transform head;
    private float elapsed = 0f;

    private void Start()
    {
        target.position = pos1.position;
        target.rotation = pos1.rotation;
    }

    void Update()
    {
        if (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration); // 0 → 1

            // 위치 보간
            target.position = Vector3.Lerp(pos1.position, pos2.position, t);

            // 회전 보간 (Slerp 권장: 자연스러움)
            target.rotation = Quaternion.Slerp(pos1.rotation, pos2.rotation, t);

            // head도 동기화
            head.position = target.position;
        }
    }
}
