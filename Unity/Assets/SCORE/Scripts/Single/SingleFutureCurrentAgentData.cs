using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

// TimeStep       : paper setting. DPS stores a time-stamped plan; RES/current agent executes
//                  each entry when its own trajectory clock reaches the time stamp.
// SpatialTrigger : legacy implementation (trigger cube spawned at the decision position).
public enum ScheduleMode { TimeStep, SpatialTrigger }

[System.Serializable]
public class PlanEntry
{
    public float time;            // trajectory clock of the DPS (future) agent [s]
    public float arcLength;       // distance travelled along the trajectory [m]
    public Vector3 position;
    public string action;         // pre-planned action (DPS)
    public string explanation;
    public string reasoning;
    public List<int> scores = new List<int>(); // Safety, Interest, SocialSchema, InformationSeeking, Habit
    public string memory;
    public string summary;

    // filled at run time by RES
    public string resAction = "";        // FastVLM answer ("keep" or an override)
    public float resQueryTime = -1f;     // execution clock when RES was queried
    public string executedAction = "";
    public float executedTime = -1f;
    [JsonIgnore] public bool resRequested;
    [JsonIgnore] public bool executed;
}

public class SingleFutureCurrentAgentData : MonoBehaviour
{
    public ScheduleMode scheduleMode = ScheduleMode.TimeStep;

    public Transform Triggers;
    public GameObject trigger;

    public List<string> futureActions = new List<string>();
    public List<string> futureExplanations = new List<string>();
    public List<string> futureVLMMemory = new List<string>();
    public List<string> currentExplanations = new List<string>();
    public List<string> currentAction = new List<string>();

    // time-stamped head-orientation plan, sorted by time
    public List<PlanEntry> plan = new List<PlanEntry>();

    public Dictionary<Vector3, string> futureActionsByPos = new();
    public Dictionary<Vector3, string> futureExplanationsByPos = new();
    public Dictionary<Vector3, string> currentActionsByPos = new();
    public Dictionary<Vector3, string> currentExplanationsByPos = new();
    public Dictionary<Vector3, string> currentHistoriesByPos = new();
    public Dictionary<Vector3, Quaternion> predictIntendedQuatsByPos = new();
    public Dictionary<Vector3, Vector2> predictIntendedDiffsByPos = new();
    public Dictionary<Vector3, Quaternion> finalHeadQuatsByPos = new();
    public Dictionary<Vector3, Vector2> finalDiffsByPos = new();


    public void AddPlanEntry(float time, float speed, Vector3 pos, string action, string explanation,
                             string reasoning, List<int> scores, string memory, string summary)
    {
        plan.Add(new PlanEntry
        {
            time = time,
            arcLength = time * speed,
            position = pos,
            action = action,
            explanation = explanation,
            reasoning = reasoning,
            scores = new List<int>(scores),
            memory = memory,
            summary = summary
        });
        plan.Sort((a, b) => a.time.CompareTo(b.time));
    }

    // Most recent planned action at or before the given DPS time (used as "Scheduled action" for RES).
    public PlanEntry GetEntryAtTime(float planTime)
    {
        PlanEntry found = null;
        foreach (var e in plan)
        {
            if (e.time <= planTime) found = e;
            else break;
        }
        return found;
    }

    public void ExportPlan(string csvOutName)
    {
        if (plan.Count == 0 || string.IsNullOrEmpty(csvOutName)) return;
        string dir = Path.Combine(Application.dataPath, "SCOREResult");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, csvOutName + "_plan.json");
        var settings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
        var rows = new List<object>();
        foreach (var e in plan)
        {
            rows.Add(new
            {
                e.time, e.arcLength,
                position = new[] { e.position.x, e.position.y, e.position.z },
                e.action, e.explanation, e.reasoning, e.scores, e.memory, e.summary,
                e.resAction, e.resQueryTime, e.executedAction, e.executedTime
            });
        }
        File.WriteAllText(path, JsonConvert.SerializeObject(new { agent = name, plan = rows }, Formatting.Indented, settings));
        Debug.Log($"[SCORE] Plan with {plan.Count} entries saved to {path}");
    }


    public void SpawnTrigger(Vector3 pos, string _futureAction)
    {
        GameObject g = Instantiate(trigger, pos, Quaternion.identity);
        g.GetComponent<futureData>().futureAction = _futureAction;
        g.transform.SetParent(Triggers);
    }
    public string GetClosestAction(Vector3 targetPos)
    {
        Vector3 closestKey = Vector3.zero;
        float minDist = float.MaxValue;
        string closestValue = null;
        Dictionary<Vector3, string> dict = futureActionsByPos;
        foreach (var kvp in dict)
        {
            float dist = Vector3.Distance(targetPos, kvp.Key);
            if (dist < minDist)
            {
                minDist = dist;
                closestKey = kvp.Key;
                closestValue = kvp.Value;
            }
        }

        return closestValue;
    }

    // SingleFutureCurrentAgentData.cs
    public bool TryGetClosestAction(Vector3 targetPos, float maxDist, out string action, out float minDist)
    {
        Vector3 closestKey = Vector3.zero;
        minDist = float.MaxValue;
        action = null;

        foreach (var kvp in futureActionsByPos) // 딕셔너리 그대로 사용
        {
            float d = Vector3.Distance(targetPos, kvp.Key);
            if (d < minDist)
            {
                minDist = d;
                action = kvp.Value;
            }
        }
        return action != null && minDist <= maxDist;
    }


}
