using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

public class SingleCurrentVLMLLMConnector : MonoBehaviour
{
    public bool isUseAllFast = true;
    [Header("Backend")]
    public LLMProvider provider = LLMProvider.Gemini;
    public string apiKey = "YOUR_GEMINI_API_KEY";  // 여기에 실제 API 키 입력 (Gemini only)
    public string ollamaURL = LLMClient.DefaultOllamaURL;
    // RES uses a lightweight FastVLM (paper: Gemini 1.5 Flash; retired -> 2.5 Flash)
    public string geminiVLM = "gemini-2.5-pro";
    public string geminiFastVLM = "gemini-2.5-flash";
    public string ollamaFastVLM = "qwen3-vl:8b";

    string FastVLMModel => LLMClient.Resolve(provider) == LLMProvider.Ollama ? ollamaFastVLM : (isUseAllFast ? geminiFastVLM : geminiVLM);

    //================
    public SingleCurrentMovement currentMovement;
    public SingleCurrentCameraSensor sensor;
    public SingleFutureCurrentAgentData agentData;

    //================
    public string currentAction = "";
    public string visibleObjectAgent = "";
    public string currentActionHistoryLog = "";
    #region FutureVLM
    public void SendVLMToGeminiSplit(List<string> capturedImages, string _futureAction)
    {
        print("Current VLM");
        //currentHeadController.HandleFutureAction(agentData.futureActions[currentMovement.currentWaypointIndex + 1]);
        StartCoroutine(SendToGeminiForVLM(capturedImages, currentMovement.currentWaypointIndex+1, _futureAction));
    }


    private IEnumerator SendToGeminiForVLM(List<string> capturedImages, int receivedIndex, string _futureAction)
    {
        string scheduled = agentData.GetClosestAction(this.transform.position);
        string parsedResponse = "";
        yield return LLMClient.Send(LLMClient.Resolve(provider), FastVLMModel, apiKey, ollamaURL,
                                    GetVLMPrompt_Current(scheduled), capturedImages, t => parsedResponse = t, "RES");
        if (string.IsNullOrEmpty(parsedResponse)) yield break;

        ParseVLMGeminiOutput_Current(parsedResponse, receivedIndex);

        if(currentAction == "" || currentAction == "keep")
        {
            currentMovement.SetHeadAction(_futureAction);
        }
        else
        {
            currentMovement.SetHeadAction(currentAction);
        }
    }

    // ---- Time-step scheduling: RES validates one plan entry ahead of its display time ----
    public void RequestValidation(List<string> capturedImages, PlanEntry entry)
    {
        StartCoroutine(ValidateEntry(capturedImages, entry));
    }

    private IEnumerator ValidateEntry(List<string> capturedImages, PlanEntry entry)
    {
        string parsedResponse = "";
        yield return LLMClient.Send(LLMClient.Resolve(provider), FastVLMModel, apiKey, ollamaURL,
                                    GetVLMPrompt_Current(entry.action), capturedImages, t => parsedResponse = t, "RES");
        if (string.IsNullOrEmpty(parsedResponse))
        {
            entry.resAction = "keep";
            yield break;
        }

        currentAction = "";
        ParseVLMGeminiOutput_Current(parsedResponse, -1);
        entry.resAction = string.IsNullOrEmpty(currentAction) ? "keep" : currentAction;
        currentActionHistoryLog += $"Scheduled: {entry.action}\nAction: {entry.resAction}\n";
        currentMovement.OnLateValidation(entry);
    }
    #endregion


    public void ParseVLMGeminiOutput_Current(string input, int receivedIndex)
    {

        // MEMORY 블록 추출 (문자열 그대로 저장)
        var actionMatch = Regex.Match(input, @"---Action---\s*(.*)", RegexOptions.Singleline);

        if (actionMatch.Success)
        {
            //csvSaver.objectMemory.Add(memoryMatch.Groups[1].Value.Trim());
            currentAction = actionMatch.Groups[1].Value.Trim();
            int nl = currentAction.IndexOf('\n');
            if (nl >= 0) currentAction = currentAction.Substring(0, nl);
            currentAction = currentAction.Trim(' ', '[', ']', '*', '"', '\'', '`', '.').Trim();
            if (currentAction.ToLower() == "keep") currentAction = "keep";
            currentMovement.curAction = currentAction;
        }

        // summary 블록
        //var explanationMatch = Regex.Match(input, @"---Explanation---\s*(.*)", RegexOptions.Singleline)
        //                    .Groups[1].Value.Trim();
        ////csvSaver.~
        //currentExplanation = explanationMatch;
        //print("Hereee");
        //agentData.currentActions[receivedIndex] = currentAction;
        //agentData.currentExplanations[receivedIndex] = currentExplanation;

        //currentActionHistoryLog += $"Explanation: {currentExplanation}\nAction: {currentAction}\n";
        //agentData.currentHistories[receivedIndex] = currentActionHistoryLog;
        //agentData.vlmMemory[moveSplit.futureWaypointIndex] = vlmMemory;
        //agentData.visualSummaryList[moveSplit.futureWaypointIndex] = visualSummary;

    }



    string GetVLMPrompt_Current(string scheduledAction)
    {
        visibleObjectAgent = sensor.GetVisibleImportantAgents();
        //print(visibleObjectAgent);


        //-2. For exploration: Output must be **\""Search <direction>\""**, where direction ∈ [left, right, up, down, forward]. Only one direction may be selected.
        string promptText = $@"
You are a person navigating through an environment. You understand that vision plays a crucial role in interacting appropriately with your surroundings-both to discover interesting things and to avoid potential dangers. And be careful-if you twist your neck too far, you stop looking human.

Here is the information currently available to you:
- Agent List: {visibleObjectAgent}\
- Scheduled action: {scheduledAction} 
- History Decision Actions : {currentActionHistoryLog}
- Your head is turned(x: {currentMovement.GetDiffValue().x}, y: {currentMovement.GetDiffValue().y}) degrees away from your body.
- Directions you are moving: {this.transform.forward}

Please decide:
- Have you detected any movement from other agents in the image list that might pose a threat or very interesting?
- If it is true, adjust your head movement. The actions you can take are:

1. For known agents: Output must be **\""Looking at <Agent name>\""**. Only use names from the \""Agent List\"". Do not add any explanations or descriptions after the action.
2. If no adjustment is needed: **keep**

Also, respond appropriately to unexpected entities that may appear in the environment (e.g., \""Santa\"").

Output Format:
---Action---
[Your final action here-either \""Looking at <Agent name>\"" or keep]";
        return promptText;
    }



    #region UTILITYS


    //No need to touch this.
    private string ParseVLMGeminiResponse_Future(string rawJson)
    {
        try
        {
            // Parse the response stream
            JArray responseArray = JArray.Parse(rawJson);
            StringBuilder fullResponse = new StringBuilder();

            foreach (JObject response in responseArray)
            {
                var parts = response["candidates"]?[0]?["content"]?["parts"];
                if (parts != null)
                {
                    foreach (var part in parts)
                    {
                        fullResponse.Append(part["text"]?.ToString());
                    }
                }
            }

            return fullResponse.ToString();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error parsing Gemini response: " + e.Message);
            return "---MEMORY---\nError parsing response.\n---REASONING---\nError.\nExplanation: Error occurred.\nAction: Search forward";
        }
    }

    #endregion

}
