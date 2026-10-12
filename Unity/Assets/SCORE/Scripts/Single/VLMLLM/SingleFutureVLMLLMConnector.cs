using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using static UnityEngine.EventSystems.EventTrigger;
using Unity.VisualScripting.Antlr3.Runtime;

public class SingleFutureVLMLLMConnector : MonoBehaviour
{
    public bool isUseAllFast = true;
    public float VLMCooldown = 5f;

    [Header("Backend")]
    public LLMProvider provider = LLMProvider.Gemini;
    public string apiKey = "YOUR_GEMINI_API_KEY";  // 여기에 실제 API 키 입력 (Gemini only)
    public string ollamaURL = LLMClient.DefaultOllamaURL;

    // Gemini models (paper: Gemini 1.5 Pro for DPS; 1.5 was retired, 2.5 is the drop-in)
    public string geminiVLM = "gemini-2.5-pro";
    public string geminiFastVLM = "gemini-2.5-flash";
    public string geminiLLM = "gemini-2.5-pro";
    public string geminiFastLLM = "gemini-2.5-flash";

    // Ollama models (run on each user's own machine, e.g. `ollama pull qwen3-vl:8b`)
    public string ollamaVLM = "qwen3-vl:8b";
    public string ollamaLLM = "qwen3-vl:8b";

    string VLMModel => LLMClient.Resolve(provider) == LLMProvider.Ollama ? ollamaVLM : (isUseAllFast ? geminiFastVLM : geminiVLM);
    string LLMModel => LLMClient.Resolve(provider) == LLMProvider.Ollama ? ollamaLLM : (isUseAllFast ? geminiFastLLM : geminiLLM);

    //============
    [SerializeField] private SigneFutureMovement singlefutureMovement;
    [SerializeField] SingleFutureCameraVisionSensor sensor;
    [SerializeField] SingleFutureHeadController singleFutureHeadController;
    public SingleFutureCurrentAgentData agentData;
    //===========
    public string cognitive_goal = "Navigate to the destination safely while observing surroundings.";
    public string visibleObjectAgent = "";
    public string vlmMemory = "";
    public string visualSummary = "";
    public string futureActionHistoryLog = "";
    List<int> futureScores = new List<int>();
    string futureExplanation = "";
    string futureAction = "";
    //=========


    private void Start()
    {
        //singlefutureMovement = this.GetComponent<SigneFutureMovement>();
        //if(singlefutureMovement != null)
        //{
        //    print("singleFutureMovement");
        //}
        //sensor = this.transform.Find("ControlHeadOffset_Single_Future").Find("AgentCamera").GetComponent<SingleFutureCameraVisionSensor>();
        //if (sensor != null)
        //{
        //    print("Check the future head offset's sensor plz.");
        //}
        //singleFutureHeadController = this.GetComponent<SingleFutureHeadController>();
        //if( singleFutureHeadController != null)
        //{
        //    print("Check the singleFutureHeadController plz.");
        //}


    }

    // ============= VLM ===================
    public void SendVLMToGeminiSplit_Future(List<string> capturedImages)
    {
        print("Send VLM");
        StartCoroutine(SendToGeminiForVLM_Future(capturedImages));
    }

    private IEnumerator SendToGeminiForVLM_Future(List<string> capturedImages)
    {

        singlefutureMovement.flag = false;
        AgentManaver.Instance.OnAgentFlagSetting(false);
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        Animator animator = this.GetComponent<Animator>();

        if (animator != null)
            animator.speed = 0f;

        string parsedResponse = "";
        yield return LLMClient.Send(LLMClient.Resolve(provider), VLMModel, apiKey, ollamaURL,
                                    GetVLMPrompt_Future(), capturedImages, t => parsedResponse = t, "VLM");
        stopwatch.Stop(); //stopwatch.ElapsedMilliseconds
        print("VLM Future Latency: " + stopwatch.ElapsedMilliseconds);

        if (string.IsNullOrEmpty(parsedResponse))
        {
            // resume the trajectory instead of stalling the whole simulation
            ResumeMovement();
            yield break;
        }

        ParseVLMGeminiOutput_Future(parsedResponse);
        SendLLMMessageToGeminiSplit_Future();

        yield return null;
    }

    void LogRequestError(UnityWebRequest req, string tag)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{tag}] Result={req.result}, Code={(long)req.responseCode}");
        sb.AppendLine($"[{tag}] Error={req.error}");
        // 서버가 에러 바디를 줄 때 원인이 들어있음
        sb.AppendLine($"[{tag}] Body={req.downloadHandler?.text}");
        UnityEngine.Debug.LogError(sb.ToString());
    }

    public void ParseVLMGeminiOutput_Future(string input)
    {

        // MEMORY 블록 추출 (문자열 그대로 저장)
        var memoryMatch = Regex.Match(input, @"---MEMORY---(.*?)---SUMMARY---", RegexOptions.Singleline);
        if (memoryMatch.Success)
        {
            //csvSaver.objectMemory.Add(memoryMatch.Groups[1].Value.Trim());
            vlmMemory = memoryMatch.Groups[1].Value.Trim();
            //agentData.futureVLMMemory.Add(vlmMemory);
        }

        // summary 블록
        var summaryMatch = Regex.Match(input, @"---SUMMARY---\s*(.*)", RegexOptions.Singleline)
                            .Groups[1].Value.Trim();
        //csvSaver.~
        visualSummary = summaryMatch;

    }

    // ============= LLM ===================
    public void SendLLMMessageToGeminiSplit_Future()
    {
        print("Send LLM");
        StartCoroutine(SendToGeminiForLLM_Future());
    }

    private IEnumerator SendToGeminiForLLM_Future()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        string parsedResponse = "";
        yield return LLMClient.Send(LLMClient.Resolve(provider), LLMModel, apiKey, ollamaURL,
                                    GetLLMPrompt_Future(), null, t => parsedResponse = t, "LLM");

        if (!string.IsNullOrEmpty(parsedResponse))
        {
            //1. 정보 처리
            ParseLLMGeminiOutput_Future(parsedResponse);

            print("Future Action: " + futureAction);
            singleFutureHeadController.HandleFutureAction(futureAction);

            if (singlefutureMovement != null)
            {
                singlefutureMovement.StartVlmCooldown(VLMCooldown);
            }
        }
        yield return new WaitForSeconds(2.0f);
        ResumeMovement();
    }

    void ResumeMovement()
    {
        singlefutureMovement.flag = true;
        AgentManaver.Instance.OnAgentFlagSetting(true);
        Animator animator = this.GetComponent<Animator>();
        if (animator != null)
            animator.speed = 1f;
    }


    #region Prompt


    string GetVLMPrompt_Future()
    {
        visibleObjectAgent = sensor.GetVisibleImportantObjects() + ", " + sensor.GetVisibleImportantAgents();

        string promptText = $@"
You are a person navigating through an environment. You understand that vision plays a crucial role in interacting with appropriately with your surroundings-both to discover interesting things and to avoid potential dangers. And be careful-if your neck twists too far, you stop looking human.

Here is the information currently available to you:
-Cognitive goal: {cognitive_goal}
-Current Memory: {vlmMemory}
-Object List: {visibleObjectAgent}

#Stage 1: Memory Update
Your task is to match the given image input with the provided object list and generate updated descriptions for each object.

### Strategy:
0. Do not add any information about objects that are not in the provided object list. And Do not change the object name in list.
1. Update the memory with information about the top 10 objects from the object list that are most closely related to the cognitive goal.
2. If new information is observed about these objects (e.g., updated position, state, or attributes), incorporate it into the memory.
3. Retain any previously known information that remains valid and is not contradicted by new observations.
4. If an object is relevant to the Cognitive Goal, include that in the memory update.

# Stage 2: Visual Summary
- The images were captured every 0.2 seconds to reflect your changing visual perspective.
- Summarize the visual changes across the image sequence and provide a concise explanation of the current situation.
- Do you observe any moving objects? If so, include them in the memory update.

### STRICT NAMING POLICY
You must **never** introduce, reference, or invent any object or entity name that does **not** appear verbatim in the **“Object List”** above.

Output Format:

## Final Output Format:
---MEMORY---
[Name1 : explain1, 
Name2 : explain2, 
Name3 : explain3, 
...]

---SUMMARY---
[Your summary here]
";
        return promptText;
    }

    string GetLLMPrompt_Future()
    {
        Vector3 headEuler = singleFutureHeadController.futureHead.localEulerAngles;
        Vector3 bodyEuler = singleFutureHeadController.futureBody.localEulerAngles;

        //// 축별 각도 차이 계산 (0~360 -> -180~180 범위로 정규화)
        float xDiff = Mathf.DeltaAngle(headEuler.x, bodyEuler.x);
        float yDiff = Mathf.DeltaAngle(headEuler.y, bodyEuler.y);
        visibleObjectAgent = sensor.GetVisibleImportantObjects() + ", " + sensor.GetVisibleImportantAgents();
        string promptText = $@"
You are a person navigating through an environment. You understand that vision plays a crucial role in interacting with appropriately with your surroundings-both to discover interesting things and to avoid potential dangers. And be careful-if your neck twists too far, you stop looking human.

Here is the information currently available to you:

- Cognitive goal: {cognitive_goal}
- Current Memory: {vlmMemory}
- Object list: {visibleObjectAgent}
- Visual Information Summary: {visualSummary}
- History Decision Actions: {futureActionHistoryLog}
- Current Head Quaternion: ({singleFutureHeadController.futureHead.rotation.x}, {singleFutureHeadController.futureHead.rotation.y}, {singleFutureHeadController.futureHead.rotation.z}, {singleFutureHeadController.futureHead.rotation.w})
- Your head is turned(x: {xDiff}, y: {yDiff}) degrees away from your body.

# Decision Making

### Instructions:

## Step 1: Task Manager
1. **Cognitive Goal Analysis**: Break down the cognitive goal into specific and actionable subtasks.
2. **Subtask Objectives**: Create five subtasks, each focusing on one of the following dimensions:
   - **Safety**: Actions that minimize risk or ensure physical security.
   - **Interest**: Actions that fulfill curiosity or inquisitiveness.
   - **Social Schema**: Actions aligned with social norms.
   - **Information-Seeking**: Actions aimed at acquiring more details or clarifying uncertain aspects of the environment. If the object list is insufficient, consider exploring the environment to gather additional information.
   - **Habit**: Common human routines.

## Step 2: Decision and Action
1. **Decision Process**:
   - Check whether the cognitive goal can be addressed using any object from the Object List.
   - If not, or if exploration would better serve the goal, choose to explore based on the context.

2. **Action Types**:
   - For known objects: Output must be **""Looking at<object name>""**. Only include object names from the ""Object List"". And Do not add any explanations or descriptions after the action.
   - For exploration: Output must be **""Search <direction>""**, where direction ∈[left, right, up, down, forward]. Only one direction can be selected.

### Final Constraints: 
- You must only select from the ""Object List"" when focusing on an object.
- If an action has already occurred twice, clearly explain why a new action was selected this time.
- Include in Explanation why you chose the action
- Balanced exploration: Use exploration appropriately - don't over-rely on the same strategy.

# Final Scoring Instruction
After deciding the action, distribute a total of 10 points across the five subtasks (Safety, Interest, Social Schema, Information-Seeking, Habit) to indicate your focus. 

Output Format:

## Final Output Format:
---REASONING---
[Your reasoning process here]

---Scores---
[SafetyScore, InterestScore, SocialSchemaScore, InformationSeekingScore, HabitScore]"";

---Explanation---
[Your explanation here]

---Action---
[Your final action here - either ""Looking at <object name>"" or ""Search <direction>""]";

        return promptText;
    }

    #endregion

    #region UTILITYS
    //No need to touch this.
    //private string ParseVLMGeminiResponse_Future(string rawJson)
    //{
    //    try
    //    {
    //        // Parse the response stream
    //        JArray responseArray = JArray.Parse(rawJson);
    //        StringBuilder fullResponse = new StringBuilder();

    //        foreach (JObject response in responseArray)
    //        {
    //            var parts = response["candidates"]?[0]?["content"]?["parts"];
    //            if (parts != null)
    //            {
    //                foreach (var part in parts)
    //                {
    //                    fullResponse.Append(part["text"]?.ToString());
    //                }
    //            }
    //        }

    //        return fullResponse.ToString();
    //    }
    //    catch (System.Exception e)
    //    {
    //        UnityEngine.Debug.LogError("Error parsing Gemini response: " + e.Message);
    //        return "---MEMORY---\nError parsing response.\n---REASONING---\nError.\nExplanation: Error occurred.\nAction: Search forward";
    //    }
    //}


    private string ParseVLMGeminiResponse_Future(string raw)
    {
        try
        {
            var trimmed = raw?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return "";

            // 1) 우선 단일 객체(JSON object) 시도: generateContent / 에러 바디 모두 포함
            if (trimmed.StartsWith("{"))
            {
                var obj = JObject.Parse(trimmed);

                // 에러 바디 처리
                if (obj["error"] != null)
                {
                    var code = obj["error"]?["code"]?.ToString();
                    var msg = obj["error"]?["message"]?.ToString();
                    UnityEngine.Debug.LogError($"[VLM] API Error {code}: {msg}");
                    return "";
                }

                return ExtractTextFromCandidates(obj);
            }

            // 2) 스트리밍(SSE) 대응: streamGenerateContent -> "data: {json}\n\n..." 라인들
            // 일부 환경에서는 순수한 JSON 라인("{...}")이 섞여 들어오기도 함
            var sb = new StringBuilder();
            using (var reader = new System.IO.StringReader(trimmed))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    // SSE prefix 제거
                    if (line.StartsWith("data:")) line = line.Substring("data:".Length).Trim();
                    // heartbeat 등 무시
                    if (line == "[DONE]" || line == ":") continue;

                    // 각 라인을 개별 JSON 객체로 파싱
                    try
                    {
                        var obj = JObject.Parse(line);
                        // 에러 라인
                        if (obj["error"] != null)
                        {
                            var code = obj["error"]?["code"]?.ToString();
                            var msg = obj["error"]?["message"]?.ToString();
                            UnityEngine.Debug.LogError($"[VLM] Stream Error {code}: {msg}");
                            continue;
                        }
                        sb.Append(ExtractTextFromCandidates(obj));
                    }
                    catch
                    {
                        // 불완전 청크는 스킵
                    }
                }
            }
            return sb.ToString();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("VLM parse error: " + e.Message + "\n" + raw);
            return "";
        }
    }

    // candidates -> content.parts[].text 추출 헬퍼
    private string ExtractTextFromCandidates(JObject obj)
    {
        var sb = new StringBuilder();
        var candidates = obj["candidates"] as JArray;
        if (candidates == null || candidates.Count == 0) return "";

        var parts = candidates[0]?["content"]?["parts"] as JArray;
        if (parts == null) return "";

        foreach (var p in parts)
        {
            var t = p?["text"]?.ToString();
            if (!string.IsNullOrEmpty(t)) sb.Append(t);
        }
        return sb.ToString();
    }


    public void ParseLLMGeminiOutput_Future(string input)
    {

        // REASONING 블록
        var reasoningMatch = Regex.Match(input, @"---REASONING---(.*?)---Scores---", RegexOptions.Singleline)
                             .Groups[1].Value.Trim();
        // SCORES 블록 (대괄호 유무 관계없이 처리)
        var match = Regex.Match(input, @"---Scores---\s*\[?([0-9,\s]+)\]?");
        string scoresStr = match.Groups[1].Value;

        List<int> _score = new List<int>();
        foreach (var num in scoresStr.Split(','))
        {
            if (int.TryParse(num.Trim(), out int score))
            {
                _score.Add(score);
                futureScores.Add(score);
            }
        }

        //if (chart != null)
        //    chart.SetScoreForUpdateChart(_score);

        // EXPLANATION 블록
        var explanationMatch = Regex.Match(input, @"---Explanation---(.*?)---Action---", RegexOptions.Singleline)
                                .Groups[1].Value.Trim();
        futureExplanation = explanationMatch;

        // ACTION 블록
        var actionMatch = Regex.Match(input, @"---Action---\s*(.*)", RegexOptions.Singleline)
                            .Groups[1].Value.Trim();
        futureAction = CleanAction(actionMatch);

        futureActionHistoryLog += $"Explanation: {futureExplanation}\nAction: {futureAction}\n";

        Vector3 roundedPos = RoundedPosition(this.transform.position);
        agentData.futureActionsByPos[roundedPos] = futureAction;
        agentData.futureExplanationsByPos[roundedPos] = futureExplanation;
        agentData.futureActions.Add(futureAction);
        agentData.futureExplanations.Add(futureExplanation);

        // Time-stamped head-orientation plan (paper, DPS): the action is keyed by the
        // trajectory clock of the future agent, not by a trigger cube at its position.
        agentData.AddPlanEntry(singlefutureMovement.trajectoryTime, singlefutureMovement.futureSpeed, roundedPos,
                               futureAction, futureExplanation, reasoningMatch, _score, vlmMemory, visualSummary);
        if (agentData.scheduleMode == ScheduleMode.SpatialTrigger)
            agentData.SpawnTrigger(roundedPos, futureAction);
    }

    // Local models sometimes wrap the action in brackets, quotes or markdown.
    static string CleanAction(string raw)
    {
        string a = raw.Trim();
        int nl = a.IndexOf('\n');
        if (nl >= 0) a = a.Substring(0, nl);
        return a.Trim(' ', '[', ']', '*', '"', '\'', '`', '.').Trim();
    }

    //No need to change it.
    private string ParseLLMGeminiResponse_Future(string rawJson)
    {
        try
        {
            JObject responseObj = JObject.Parse(rawJson);

            var candidates = responseObj["candidates"] as JArray;
            if (candidates == null || candidates.Count == 0)
            {
                UnityEngine.Debug.LogWarning("No candidates found in Gemini response.");
                return "";
            }

            var parts = candidates[0]["content"]?["parts"] as JArray;
            if (parts == null)
            {
                UnityEngine.Debug.LogWarning("No parts found in Gemini response.");
                return "";
            }

            StringBuilder fullResponse = new StringBuilder();
            foreach (var part in parts)
            {
                fullResponse.Append(part["text"]?.ToString());
            }

            return fullResponse.ToString();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error parsing Gemini response: " + e.Message);
            return "---MEMORY---\nError parsing response.\n---REASONING---\nError.\nExplanation: Error occurred.\nAction: Search forward";
        }
    }

    Vector3 RoundedPosition(Vector3 pos, int decimals = 2)
    {
        float factor = Mathf.Pow(10, decimals);
        return new Vector3(
            Mathf.Round(pos.x * factor) / factor,
            1,
            Mathf.Round(pos.z * factor) / factor
        );
    }

    #endregion
}
