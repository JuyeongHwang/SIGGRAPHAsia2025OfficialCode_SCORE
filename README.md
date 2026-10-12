# [SIGGRAPH Asia 2025 Official Code] SCORE

**How Does a Virtual Agent Decide Where to Look? Symbolic Cognitive Reasoning for Embodied Head Rotation**
Juyeong Hwang\*, Seong-Eun Hong\*, JaeYoung Seon, HyeongYeop Kang (\* equal contribution)
SIGGRAPH Asia 2025 Conference Papers · [Project page](https://juyeonghwang.github.io/SCORE.github.io/)


## Contents

```
Unity/Assets/SCORE/Scripts/Single/   SCORE (Unity 2022.3, C#)
  Movement/SigneFutureMovement.cs      DPS agent: walks the path, 0.2 s FOV capture, SSIM novelty test
  VLMLLM/SingleFutureVLMLLMConnector.cs  DPS: VLM perception memory -> LLM action-reason + driver scores
  SingleFutureCurrentAgentData.cs      time-stamped plan (PlanEntry) + JSON export
  Movement/SingleCurrentMovement.cs    RES agent: replays the path, executes plan entries at their time stamp
  VLMLLM/SingleCurrentVLMLLMConnector.cs RES: FastVLM validation (keep / override), 2 s look-ahead
  VLMLLM/LLMClient.cs                  VLM / LLM requests (Gemini API)
  Head/, Sensor/, Recorder/            head control, ray-cast entity lists, CSV recording / playback
Unity/Assets/Baselines/TRACK/        playback of the Track baseline output
Unity/Assets/Human/                  playback of recorded human head trajectories, NPC movement
Unity/ProjectSettings, Packages/     tags, input, physics, package manifest
results/SCORE/                       SCORE head trajectories for the ten user-study scenarios
```

## Setup

1. Create a Unity **2022.3** project (3D, built-in render pipeline) and copy `Unity/Assets`,
   `Unity/Packages/manifest.json` and `Unity/ProjectSettings` into it. Required packages:
   Newtonsoft Json, Animation Rigging, TextMeshPro.
2. The study environments use third-party assets that cannot be redistributed:
   Synty POLYGON City / Town / Shops packs and Mixamo character animations. Any scene works as long as
   it provides, per agent: a `Trajectory` (waypoints), a DPS body with `SigneFutureMovement` +
   `SingleFutureVLMLLMConnector`, an RES body with `SingleCurrentMovement` + `SingleCurrentVLMLLMConnector`,
   head pivots `ControlHeadOffset_Single_Future/Current` with an `AgentCamera`, and the parents
   `importantObjects` / `importantAgents` that list the entities the agent may look at
   (object names are what the LLM refers to).
3. Set the API key (below) and `cognitive_goal`, and press Play. The DPS agent walks first;
   the RES agent then replays the path and records to `Assets/SCOREResult/`.

## VLM / LLM

Set `apiKey` on both connectors (do not commit it). Defaults are `gemini-2.5-pro` (DPS) and
`gemini-2.5-flash` (RES); the paper used Gemini 1.5 Pro / Flash, which have been retired.

## Scheduling

`SingleFutureCurrentAgentData.scheduleMode`

* `TimeStep` (default, as in the paper): the DPS agent keeps a trajectory clock that only advances
  while it walks (it pauses during VLM/LLM calls) and stores every action–reason pair with that time
  stamp. The RES agent replays the trajectory with its own clock, queries the FastVLM `resLookAhead`
  (2 s) before each entry, and executes the entry, or the RES override, when its clock reaches the
  time stamp. If the agents walk at different speeds, time stamps are rescaled to the same arc length.
* `SpatialTrigger` (legacy): a trigger cube is spawned at the decision position and the action fires
  when the RES agent enters it.

## Outputs (`Assets/SCOREResult/`)

* `<csvOutName>.csv` — head and body quaternions and positions every `recordInterval`, plus `Time`.
* `<csvOutName>_plan.json` — the time-stamped plan: time, position, action, explanation, reasoning,
  driver scores (Safety, Interest, Social Schema, Information Seeking, Habit), memory, RES answer,
  executed action and time.

`results/SCORE/<Scene>_<k>.csv` are SCORE runs in the user-study scenes (Normal = MDC, Abnormal = APC)
in the CSV format above (without the `Time` column; sampled every 0.05 s).

## Known differences from the paper text

* The head is not returned to the walking direction after the N(1.5, 0.25²) s hold.
* RES is queried once per plan entry (2 s ahead) rather than every 0.2 s.
* The FMM is not capped at 20 entries.
* The RES head turn uses 60°/s (DPS uses 36°/s).
* The novelty threshold is `diffPercentCoef` on (1 − SSIM)·100, set per scene.
* `SingleCurrentCameraSensor` ray-casts against a layer named `Current`. Add that layer (and put the
  agents on it) if you want RES to see other agents; without it, the RES entity list is empty.

## License

The code in this repository is released under the [MIT License](LICENSE). Third-party assets used in the
study scenes (Synty POLYGON packs, Mixamo animations) are not part of this repository and remain under
their own licenses.

## Citation

```bibtex
@inproceedings{hwang2025score,
  title     = {How Does a Virtual Agent Decide Where to Look? Symbolic Cognitive Reasoning for Embodied Head Rotation},
  author    = {Hwang, Juyeong and Hong, Seong-Eun and Seon, JaeYoung and Kang, HyeongYeop},
  booktitle = {SIGGRAPH Asia 2025 Conference Papers},
  year      = {2025}
}
```
