# Gotchas

A running log of everything the documentation did not warn about. Each entry records what
was expected, what actually happened, and what to do about it.

Written against **Microsoft.Agents.AI.Harness 1.15.0** on .NET 10. Several of these are
version-specific and will stop being true.

---

## 1. `DisableFileAccess` does not exist

**Expected:** a `DisableFileAccess` flag on `HarnessAgentOptions`, matching `DisableWebSearch`.

**Actual:** there isn't one. `FileAccessProvider` is the only provider in the harness that is
**opt-in**: it appears solely when you set `FileAccessStore` to an `AgentFileStore`. Leave that
null and the agent has no file tools at all.

**Do:** nothing. The absence of a setting is the setting. Worth knowing so you don't spend
twenty minutes looking for the off switch on something already off.

---

## 2. `FileMemoryProvider` is on by default and writes to your working directory

**Expected:** memory is something you opt into.

**Actual:** `DisableFileMemory` defaults to `false`, and when no `FileMemoryStore` is supplied
the harness builds a `FileSystemAgentFileStore` rooted at:

```
{cwd}/agent-file-memory/{timestamp}_{guid}
```

Run the console a few times and the repo root fills up with timestamped directories.

**Do:** set `DisableFileMemory = true` until you have somewhere deliberate to put it. Milestone 4
re-enables it against a per-voyage store. `ShipAgentFactoryTests` has a regression test that
constructs the agent inside a temp directory and asserts it stays empty.

---

## 3. `AgentSkillsProvider` is also on by default, and scans the working directory

**Expected:** skills load when you point the agent at some.

**Actual:** `DisableAgentSkillsProvider` defaults to `false`, and with no `AgentSkillsSource`
the provider falls back to file-based skill discovery from the current working directory.
For a repo with no skills in it, that is a filesystem walk that can only ever find nothing.

**Do:** `DisableAgentSkillsProvider = true` until there are skills worth loading.

---

## 4. There is no console host in any NuGet package

**Expected:** `HarnessConsole.RunAgentAsync(agent, userPrompt: ...)`, per the samples.

**Actual:** `HarnessConsole` lives in the framework's sample sources, not in a shipped package.
Searching nuget.org for `Harness.Shared.Console`, `Microsoft.Agents.AI.Harness.Console`, and
`Microsoft.Agents.AI.Console` returns nothing.

**Do:** write your own loop. It is about eighty lines: read a line, call
`agent.RunStreamingAsync(input, session)`, print `update.Text`, and pick tool-call announcements
out of `update.Contents.OfType<FunctionCallContent>()`. `src/ShipAI.Console` is the whole thing.

---

## 5. `[Experimental]` is a build error, not a warning

**Expected:** experimental APIs produce warnings you can choose to heed.

**Actual:** `ExperimentalAttribute` produces a compile **error** by default. In 1.15.0 only the
compaction properties carry one:

```
error MAAI001: 'HarnessAgentOptions.MaxContextWindowTokens' is for evaluation purposes only
```

**Do:** add the specific diagnostic ID to `NoWarn` (yes, `NoWarn` suppresses it even though it
presents as an error). Suppress the exact IDs rather than blanket-suppressing, so the next
experimental API you touch still stops the build and gets a decision:

```xml
<NoWarn>$(NoWarn);MAAI001</NoWarn>
```

---

## 6. Compaction is silently disabled unless you set *both* token limits

**Expected:** setting `MaxContextWindowTokens` enables compaction.

**Actual:** the `CompactionProvider` is added to the chat-client pipeline only when
`MaxContextWindowTokens` **and** `MaxOutputTokens` are both non-null (and no custom
`CompactionStrategy` is set). Supply one and you get no compaction, no warning, and no error —
you find out on the turn the context window overflows.

**Do:** set both, together, and treat them as a single decision.

---

## 7. `Azure.AI.Projects` has no chat-client entry point

**Expected:** `AIProjectClient` yields an `IChatClient`, per most "get started with Foundry"
material.

**Actual:** in `Azure.AI.Projects` 2.0.0, `AIProjectClient` exposes connections, datasets,
deployments, indexes, memory stores, evaluations, and red teams. There is no `GetChatClient`,
no `GetAzureOpenAIChatClient`, and no chat surface of any kind.

**Do:** reach a Foundry deployment through `AzureOpenAIClient` pointed at the project's OpenAI
endpoint, then adapt it:

```csharp
new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deployment)
    .AsIChatClient();
```

`AsIChatClient()` comes from `Microsoft.Extensions.AI.OpenAI`, which is a separate package from
`Microsoft.Extensions.AI` and needs to be version-matched to it.

---

## 8. `AIContextProvider.StateKeys` is plural

**Expected:** `StateKey`, singular, as shown in most context-provider examples.

**Actual:** the property on `AIContextProvider` is `StateKeys` (plural). `ProviderSessionState<T>`
does expose a singular `StateKey`, which is where the confusion comes from — one provider can own
several session-state slots.

**Do:** override `StateKeys` and return the keys of every `ProviderSessionState<T>` the provider
owns. Detail lands properly in milestone 2.

---

## 9. Records plus immutable collections do not give you structural equality

Not a harness issue, but it bites immediately when writing a determinism test.

**Expected:** `ShipState` is a record, so two structurally identical states compare equal.

**Actual:** the compiler-generated `Equals` calls `EqualityComparer<T>.Default` on each member,
and `ImmutableDictionary<K,V>` / `ImmutableArray<T>` compare by reference. Two identical replays
report as different.

**Do:** compare a canonical serialisation instead. `tests/ShipAI.Simulation.Tests/StateDigest.cs`
is four lines and does the job.

---

## 10. `Console.WindowWidth` throws the moment you pipe the app anywhere

Console hosts for agents get piped constantly — asciinema recordings for the post, CI smoke
runs, `| tee transcript.txt` while capturing material. `Console.WindowWidth` throws
`IOException: The handle is invalid` whenever stdout is not a terminal, and it takes the whole
process down with exit code `-532462766`.

Found by piping an empty line into the console to smoke-test the render path without a model
call — which is a cheap habit worth keeping.

**Do:** check `Console.IsOutputRedirected` and fall back to a fixed width, with a `catch (IOException)`
behind it for the platforms where the check alone is not enough. See
`ShipConsole.Width` in `src/ShipAI.Console/Rendering/ShipConsole.cs`.

---

## 11. A simulation that kills itself makes the agent irrelevant

Not a framework issue at all — a design one, caught by a failing test, and worth recording
because it will happen to anyone building a world for an agent to act in.

The first version heated the reactor whenever total power allocation exceeded 80%. The scenario's
opening allocation is 95%, so the reactor saturated at maximum heat by turn 12 and started taking
hull with it, on every run, regardless of what the agent did. The determinism test caught it
sideways: two different seeds produced *identical* end states, because both had bottomed out.

**Do:** make the default posture survivable and let pressure come from scripted events and the
agent's own choices. Then write the test that says so — `DefaultPowerPostureDoesNotCookTheReactorOnItsOwn`.
An agent demo where the outcome is fixed before the model speaks is not a demo of the model.
