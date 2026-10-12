#if UNITY_EDITOR
using UnityEditor.Animations;
#endif
using UnityEngine;

public class BlendTreeController : MonoBehaviour
{
    public Transform parant_SCORE;
    public Transform Santa;
    public Animator animator;
    public RuntimeAnimatorController agentController; // 스페이스 누르면 바꿀 컨트롤러
    public RuntimeAnimatorController dance; // 스페이스 누르면 바꿀 컨트롤러
    public string blendParamName = "Blend"; // Blend Tree에서 사용하는 float 파라미터 이름
    public float duration = 0.1f;             // 0→1로 가는 데 걸리는 시간 (초)
    public Transform target;
    private float currentValue = 0f;
    private float elapsedTime = 0f;
    private float elapsedTime2 = 0f;
    public SingleCurrentMovement singleCurrentMovement;
    public SingleCurrentMovement NPC1;
    public SingleCurrentMovement NPC2;
    public Transform FFCam;
    bool switchedToDance = false;
    private void Start()
    {
        FFCam.SetParent(parant_SCORE);
    }
    void Update()
    {
        if(singleCurrentMovement.isReached == true)
        {
            if (!switchedToDance)
            {
                animator.runtimeAnimatorController = dance;

                switchedToDance = true;
            }

            if (elapsedTime2 < duration)
            {
                elapsedTime2 += Time.deltaTime;
                currentValue = Mathf.Clamp01(elapsedTime2 / duration); // 0→1로 선형 증가
                animator.SetFloat(blendParamName, currentValue);
            }

            Transform agent = singleCurrentMovement.transform;

            // Santa → Agent 방향 벡터 계산
            Vector3 direction = agent.position - transform.position;
            direction.y = 0; // y축 회전만 하려면 y를 고정

            // 목표 회전
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            // 보조 회전: 같은 방향인데 y축 180도 추가
            Quaternion oppositeRotation = targetRotation * Quaternion.Euler(0, 180f, 0);

            // 현재 → oppositeRotation → targetRotation 순으로 Lerp
            // 먼저 oppositeRotation 쪽으로 가게 해서 반대 경로로 회전 유도
            Santa.transform.rotation = Quaternion.RotateTowards(
                Santa.transform.rotation,
                targetRotation,
                60.0f * Time.deltaTime // 음수는 안 먹으니, opposite을 경유
            );

            // 목표 방향 계산
            Vector3 direction_cam = ((singleCurrentMovement.transform.position+Vector3.up*0.5f) - FFCam.position).normalized;

            // 현재 rotation에서 목표 rotation으로 부드럽게 보간
            Quaternion targetRot = Quaternion.LookRotation(direction_cam);

            FFCam.rotation = Quaternion.Slerp(
                FFCam.rotation,     // 현재 회전
                targetRot,          // 목표 회전
                Time.deltaTime * 4f // 회전 속도 (2f는 속도 계수, 값 키우면 더 빨라짐)
            );

            FFCam.transform.position += 0.7f * Time.deltaTime * FFCam.forward;
        }
        else
        {
            if (singleCurrentMovement.isActiveAndEnabled == false)
            {
                singleCurrentMovement.enabled = true;
                NPC1.enabled = true;
                NPC2.enabled = true;
            }
            if (target.localPosition.y >= 0)
            {
                target.localPosition -= Vector3.up * Time.deltaTime;
            }



            if (animator != agentController)
            {
                animator.runtimeAnimatorController = agentController;
            }
            if (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                currentValue = Mathf.Clamp01(elapsedTime / duration); // 0→1로 선형 증가
                animator.SetFloat(blendParamName, currentValue);
            }
            FFCam.transform.position += Vector3.forward * Time.deltaTime * 1.18f;

            if(singleCurrentMovement.currentWaypointIndex == 4)
            {
                if (Santa.gameObject.activeSelf == false)
                {
                    Santa.gameObject.SetActive(true);
                }
            }
            if (singleCurrentMovement.triggerId == 4)
            {

            }
        }
    }
}
