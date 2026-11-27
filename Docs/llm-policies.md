# LLM action policies

Arms B and C of the master's thesis experiment replace the action-selection strategy while the
state machine, point economy and safety rules stay unchanged. Both use the same catalog, prompt,
validator and telemetry; only the reaction to an invalid proposal differs.

| Type | Arm | Behaviour |
| --- | --- | --- |
| `RulePolicy` | A | Weighted random draw over the eligible actions |
| `LlmFreePolicy` | B | The model may propose any id; an invalid proposal is recorded and nothing is executed |
| `LlmBoundedPolicy` | C | The validator is binding; a rejected proposal falls back to `RulePolicy` and the reason is logged |

## Prompt contract

The prompt is built from two parts:

1. **Static prefix first** — instructions, the action catalog (id, type, cost, cooldown,
   description) and the rules. It never depends on the game state, so the provider can cache it.
2. **Game state last** — JSON with the state name, action type, available points, session time,
   ratio, HP ratio, kill rate, damage rate and living enemies.

The response must be a single JSON object with one string property:

```json
{"action": "SpawnHealthPack"}
```

No rationale is requested. `DirectorGameState` is built by the Director and attached to
`ActionSelectionContext`, so policies never read `DirectorData` themselves.

## Decision cadence and deadline

- At most **one call per 4 s**, and the Director only asks for an action outside `Maintain`.
- Calls are asynchronous: the frame never blocks. The policy returns `null` while a call is in
  flight and consumes the result on a later selection.
- The decision deadline is **500 ms**, measured wall-clock around the request (including retries).
  A late decision is still executed and logged as `deadline_miss`.
- **2 retries** per decision. A failed decision is recorded; sessions with more than **5 % failed
  calls** are flagged in the session summary.

`Director.Policy` is the integration point; the experiment runner (#25) constructs the policy and
assigns it after the Director node is ready. `Director.Actions` exposes the action nodes used to
build the catalog.

## Validator

`LlmActionValidator` checks, in order:

1. the response is valid JSON with exactly one string property `action`,
2. the id exists in the catalog,
3. the id is one of the current candidates for the requested state,
4. the action can execute now (point budget and cooldown).

Rejection reasons: `malformed`, `unknownaction`, `noteligible`, `cost`, `cooldown`. Arm B records
the reason and does nothing; arm C records it and returns the rule policy's pick.

## Telemetry

One JSONL file per session, configured through `LlmPolicyOptions.TelemetryPath`.

Decision record:

```json
{"type":"decision","session":"...","arm":"LlmBounded","game_time":123.4,"state":"Reduce",
 "proposed":"SpawnHealthPack","executed":"SpawnHealthPack","legal":true,"rejection":null,
 "latency_ms":412.7,"deadline_miss":false,"prompt_tokens":812,"completion_tokens":9,
 "retries":0,"failed":false,"cost_usd":0.000021}
```

Agreement sample (at most 50 per session): `state_sample` with the canonical prompt and state for
the offline five-repetition agreement measurement.

Session summary (written by the runner):

```json
{"type":"session_summary","legal_rate":1.0,"failure_rate":0.0,"deadline_misses":2,
 "prompt_tokens":... ,"completion_tokens":...,"cost_usd":...,"flagged":false}
```

`LlmSessionSummary` exposes `Calls`, `Failures`, `Proposed`, `Legal`, `DeadlineMisses`,
`PromptTokens`, `CompletionTokens`, `CostUsd`, `LegalRate`, `FailureRate` and `IsFlagged`.

## DeepSeek configuration

`DeepSeekLlmClient` speaks the OpenAI-compatible endpoint (`POST /chat/completions`) with
temperature 0, a bounded `max_tokens`, JSON response format and no semantic cache.

| Variable | Purpose |
| --- | --- |
| `DEEPSEEK_API_KEY` | Bearer token; never committed |
| `DEEPSEEK_MODEL` | Pinned model id (default `deepseek-chat`) |
| `DEEPSEEK_BASE_URL` | Endpoint base (default `https://api.deepseek.com`) |
| `DEEPSEEK_INPUT_USD_PER_MTOK` / `DEEPSEEK_OUTPUT_USD_PER_MTOK` | Prices used for cost accounting |
| `DEEPSEEK_MAX_OUTPUT_TOKENS` | Response cap (default 64) |

`.env.example` lists the variables; the real `.env` stays local. Nothing in the game constructs a
live client: the runner does, and only when a key is present.

## Usage

```csharp
LlmPolicyOptions options = LlmPolicyOptions.FromEnvironment("TestResults/llm/session.jsonl");
var telemetry = new LlmTelemetry(options, sessionId);
var catalog = new LlmActionCatalog(director.Actions);
director.Policy = new LlmBoundedPolicy(
    DeepSeekLlmClient.FromEnvironment(),
    options,
    catalog,
    telemetry);
```

## Tests and CI

`LlmValidatorTest`, `LlmPolicyTest` and `LlmTelemetryTest` use `FakeLlmClient` and a fake clock.
They cover the prompt contract, cadence, deadline metric, retries, failure flagging, token/cost
accounting and the difference between arms B and C. CI never calls a provider and needs no key.

## Limitations

- Provider latency is measured in wall-clock time; a faster-than-real-time session cannot infer
  from it whether the game "had time" to wait.
- The agreement measurement is offline: samples are recorded, not replayed during a session.
- One pinned model and one prompt version per experiment; changing either requires re-running the
  whole matrix.
