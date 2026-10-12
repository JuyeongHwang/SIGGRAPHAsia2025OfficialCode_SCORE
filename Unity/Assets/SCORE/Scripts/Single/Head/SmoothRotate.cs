using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmoothRotate : MonoBehaviour
{
    Coroutine _routine;

    public void StopRotation()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }


    // 새 API: 쿼터니언으로 회전 (속도 기반)
    public void RotateToQuaternion(Quaternion targetRot, float degreesPerSecond = 36f) =>
        StartNewRoutine(RotateRoutine(targetRot, degreesPerSecond));

    // 타깃을 바라보게 회전 (속도 기반)
    public void RotateToLookAt(Transform target, float degreesPerSecond = 36f)
    {
        Quaternion lookRot = Quaternion.LookRotation(
            target.position - transform.position, Vector3.up);
        StartNewRoutine(RotateRoutine(lookRot, degreesPerSecond));
    }

    // 문자열 방향으로 회전 (속도 기반)
    public void RotateToAction(string action, float degreesPerSecond = 36f, Transform body = null)
    {
        Quaternion targetRot = body == null
            ? Quaternion.Euler(MapActionToRotation(action))
            : GetRelativeRotation(action, body);
        StartNewRoutine(RotateRoutine(targetRot, degreesPerSecond));
    }

    void StartNewRoutine(IEnumerator routine)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(routine);
    }

    // 수정된 회전 루틴: 속도 기반
    IEnumerator RotateRoutine(Quaternion targetRot, float degreesPerSecond)
    {
        Quaternion startRot = transform.rotation;
        float angle = Quaternion.Angle(startRot, targetRot); // 전체 회전 각도
        float time = angle / degreesPerSecond;               // 걸리는 시간 (초)
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / time;
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        transform.rotation = targetRot;
        _routine = null;
    }
    /* ---------- 유틸리티 ---------- */

    // 월드 기준 Euler 각도 반환
    Vector3 MapActionToRotation(string action)
    {
        switch (action.ToLower())
        {
            case "forward": return Vector3.zero;
            case "right": return new Vector3(0, 70, 0);
            case "left": return new Vector3(0, -70, 0);
            case "up": return new Vector3(-20, 0, 0);
            case "down": return new Vector3(20, 0, 0);
            default: return Vector3.zero;
        }
    }

    // body(기준 Transform) 상대 회전을 월드 쿼터니언으로
    Quaternion GetRelativeRotation(string direction, Transform bodyTransform)
    {
        Vector3 localEuler = direction.ToLower() switch
        {
            "forward" => Vector3.zero,
            "right" => new Vector3(0, 70, 0),
            "left" => new Vector3(0, -70, 0),
            "up" => new Vector3(-20, 0, 0),
            "down" => new Vector3(20, 0, 0),
            _ => Vector3.zero
        };
        return bodyTransform.rotation * Quaternion.Euler(localEuler);
    }
}
