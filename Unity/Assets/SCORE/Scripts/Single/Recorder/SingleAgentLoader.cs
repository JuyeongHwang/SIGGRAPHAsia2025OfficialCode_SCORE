using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class SingleAgentLoader : MonoBehaviour
{
    public List<SingleCurrentMovement> agents;

    [Header("Targets")]
    public Transform headTransform;     // 머리 회전을 적용할 트랜스폼 (예: 카메라 리그의 Head)
    public Transform bodyTransform;     // 몸 회전을 적용할 트랜스폼
    
    [Header("Source CSV")]
    public string csvOutName = "";      // 저장 때 사용한 이름 (확장자 제외)
    public float recordInterval = 1f;   // 저장 시 간격과 동일하게 설정

    [Header("Playback")]
    public bool autoPlayOnStart = true;
    public bool loop = false;
    public bool interpolate = false;    // 두 프레임 사이 보간 (간격이 길 때만 추천)

    // 내부 상태
    private readonly List<Frame> _frames = new List<Frame>();
    private int _idx = 0;
    private bool _isPlaying = false;
    private Coroutine _playRoutine;

    struct Frame
    {
        public Quaternion headQ;
        public Vector3 pos;
        public Quaternion bodyQ;
        public float diffPitch; // 사용하지 않아도 되지만 CSV 컬럼 보존
        public float diffYaw;
    }

    private void Start()
    {
        for (int i = 0; i < agents.Count; i++) 
        {
            agents[i].HandleMovementEnd();
        }
        if (autoPlayOnStart)
        {
            LoadFromCsv();
            Play();
        }
    }

    // --- Public Controls ---
    public void LoadFromCsv()
    {
        _frames.Clear();
        _idx = 0;

        if (string.IsNullOrEmpty(csvOutName))
        {
            Debug.LogError("[SingleAgentPlayer] csvOutName 비어 있음.");
            return;
        }

        string path = Path.Combine(Application.dataPath, "SCOREResult", csvOutName + ".csv");
        if (!File.Exists(path))
        {
            Debug.LogError($"[SingleAgentPlayer] CSV 파일을 찾을 수 없음: {path}");
            return;
        }

        var inv = CultureInfo.InvariantCulture;

        using (var reader = new StreamReader(path))
        {
            // 헤더 스킵
            if (!reader.EndOfStream) reader.ReadLine();

            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // 저장 포맷:
                // HeadRotx,HeadRoty,HeadRotz,HeadRotw,HeadBodyposx,HeadBodyposy,HeadBodyposz,BodyRotx,BodyRoty,BodyRotz,BodyRotw,HeadDiffPitch,HeadDiffYaw
                // 공백이 있어도 되도록 Trim
                string[] parts = line.Split(',');
                if (parts.Length < 13)
                {
                    Debug.LogWarning($"[SingleAgentPlayer] 잘못된 라인(컬럼 {parts.Length}): {line}");
                    continue;
                }

                try
                {
                    var qx = float.Parse(parts[0].Trim(), inv);
                    var qy = float.Parse(parts[1].Trim(), inv);
                    var qz = float.Parse(parts[2].Trim(), inv);
                    var qw = float.Parse(parts[3].Trim(), inv);

                    var px = float.Parse(parts[4].Trim(), inv);
                    var py = float.Parse(parts[5].Trim(), inv);
                    var pz = float.Parse(parts[6].Trim(), inv);

                    var bqx = float.Parse(parts[7].Trim(), inv);
                    var bqy = float.Parse(parts[8].Trim(), inv);
                    var bqz = float.Parse(parts[9].Trim(), inv);
                    var bqw = float.Parse(parts[10].Trim(), inv);

                    var dp = float.Parse(parts[11].Trim(), inv);
                    var dy = float.Parse(parts[12].Trim(), inv);

                    _frames.Add(new Frame
                    {
                        headQ = new Quaternion(qx, qy, qz, qw),
                        pos = new Vector3(px, py, pz),
                        bodyQ = new Quaternion(bqx, bqy, bqz, bqw),
                        diffPitch = dp,
                        diffYaw = dy
                    });
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SingleAgentPlayer] 파싱 실패: {e.Message}\n라인: {line}");
                }
            }
        }

        Debug.Log($"[SingleAgentPlayer] 로드 완료. 프레임 수: {_frames.Count}");
    }

    public void Play()
    {
        if (_frames.Count == 0)
        {
            Debug.LogWarning("[SingleAgentPlayer] 재생할 프레임이 없습니다. 먼저 LoadFromCsv() 호출하세요.");
            return;
        }
        if (_isPlaying) return;

        _isPlaying = true;
        _playRoutine = StartCoroutine(PlayRoutine());
    }

    public void Pause()
    {
        if (!_isPlaying) return;
        _isPlaying = false;
        if (_playRoutine != null) StopCoroutine(_playRoutine);
        _playRoutine = null;
    }

    public void StopAndRewind()
    {
        Pause();
        _idx = 0;
        if (_frames.Count > 0)
            ApplyFrame(_frames[0]); // 처음 상태로
    }

    public void StepForward()
    {
        if (_frames.Count == 0) return;
        _idx = Mathf.Min(_idx + 1, _frames.Count - 1);
        ApplyFrame(_frames[_idx]);
    }

    public void StepBackward()
    {
        if (_frames.Count == 0) return;
        _idx = Mathf.Max(_idx - 1, 0);
        ApplyFrame(_frames[_idx]);
    }

    // --- Core Playback ---
    private IEnumerator PlayRoutine()
    {
        // 시작 프레임 적용
        if (_idx < 0 || _idx >= _frames.Count) _idx = 0;
        ApplyFrame(_frames[_idx]);

        var wait = new WaitForSeconds(recordInterval);

        while (_isPlaying)
        {
            int next = _idx + 1;

            if (next >= _frames.Count)
            {
                if (loop)
                {
                    next = 0;
                }
                else
                {
                    _isPlaying = false;
                    this.gameObject.SetActive(false);
                    yield break;
                }
            }

            if (interpolate)
            {
                // recordInterval 동안 선형 보간
                float t = 0f;
                Frame a = _frames[_idx];
                Frame b = _frames[next];

                while (t < recordInterval)
                {
                    float u = Mathf.Clamp01(t / recordInterval);
                    // 위치/회전 보간
                    //if (agentRoot) agentRoot.position = Vector3.Lerp(a.pos, b.pos, u);
                    if (bodyTransform) bodyTransform.rotation = Quaternion.Slerp(a.bodyQ, b.bodyQ, u); bodyTransform.position = Vector3.Lerp(a.pos, b.pos, u) -Vector3.up * 1.605f;
                    if (headTransform) headTransform.rotation = Quaternion.Slerp(a.headQ, b.headQ, u); headTransform.position = Vector3.Lerp(a.pos, b.pos, u);
                    t += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                yield return wait;
            }

            _idx = next;
            ApplyFrame(_frames[_idx]);
        }
    }

    private void ApplyFrame(Frame f)
    {
        //if (agentRoot) agentRoot.position = f.pos;
        if (bodyTransform) bodyTransform.rotation = f.bodyQ; bodyTransform.position = f.pos;
        if (headTransform) headTransform.rotation = f.headQ; headTransform.position = f.pos;
        // 필요시 diffPitch/diffYaw를 이용해 head를 재구성하려면 아래처럼 사용할 수 있음:
        // headTransform.rotation = Quaternion.Euler(f.diffPitch, f.diffYaw, 0f) * bodyTransform.rotation;
    }

    // --- Optional: 간단 키 컨트롤 ---
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_isPlaying) Pause(); else Play();
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            StopAndRewind();
        }
        if (Input.GetKeyDown(KeyCode.Period)) // >
        {
            StepForward();
        }
        if (Input.GetKeyDown(KeyCode.Comma)) // <
        {
            StepBackward();
        }
    }
}
