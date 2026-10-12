using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AgentManaver : MonoBehaviour
{

    public static AgentManaver Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // 이미 있으면 파괴
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // 씬 전환 유지 (필요 없으면 제거 가능)
    }

    public List<SigneFutureMovement> futureAgents = new List<SigneFutureMovement>();
    //public List<SingleCurrentMovement> currentAgents = new List<SingleCurrentMovement>();
    public int endFutureMovement = 0;
    public bool startGame = false;

    private void Update()
    {
        if (!startGame && futureAgents.Count == endFutureMovement)
        {
            for (int i = 0; i < futureAgents.Count; i++)
            {
                //futureAgents[i].onMovementEnd?.Invoke();
                //futureAgents[i].currentAvatar.HandleMovementEnd();
                futureAgents[i].currentAvatar.gameObject.SetActive(true);
                print(futureAgents[i].currentAvatar.name);
            }
            startGame = true;
        }

    }

    public void OnAgentFlagSetting(bool _flag)
    {
        for (int i = 0; i < futureAgents.Count; i++) 
        {
            futureAgents[i].flag = _flag;
        }
    }
}
