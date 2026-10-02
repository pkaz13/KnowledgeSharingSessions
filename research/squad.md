# Research: how Squad works

Ticket: #7 (map #4, Topic SquadAgentTeams). Researched 2026-10-02 against Squad `v0.13.1` (latest release, 2026-08-26) and the `bradygaster/squad` default branch as of that date.

Squad is **alpha**. The README says commands and APIs "may change between releases". Re-check the facts below before recording the demo.

## 1. What it is

- Squad gives you "a human-directed AI development team through GitHub Copilot". You describe the project, and Squad proposes specialists (lead, frontend, backend, tester...) that "live in your repo as files", persist across sessions and share decisions. [README]
- Each team member "runs in its own context, reads only its own knowledge, and writes back what it learned". The README's phrase: "not a chatbot wearing hats". [README]
- Brady Gaster's framing on the GitHub Blog: a thin coordinator that "doesn't do the work; it spawns specialists". Specialists are separate inference calls with their own context windows, and memory is "versioned right alongside your code". It is "not autopilot... You still review and merge every pull request." [GH blog 2026-03-20]
- Design slogan from the Microsoft Command Line article by Gaster and Dresher: "Don't preserve the agent. Preserve the work." Agents are disposable, and each new spawn rebuilds continuity from the `.squad/` files. [CommandLine 2026-05-28]
- License: MIT. TypeScript monorepo with `@bradygaster/squad-cli` and `@bradygaster/squad-sdk`, plus a preview .NET package `Squad.Agents.AI` (an adapter for Microsoft Agent Framework). Maintainers: Brady Gaster and Tamir Dresher. About 3.2k stars. [README, repo metadata, MS devblog 2026-07-30]

## 2. Architecture

### Flow
User request → **Coordinator** (routing engine, defined in `.github/agents/squad.agent.md`) → spawns agents in parallel → each agent reads `.squad/` memory, works, then writes results → **Scribe** merges decisions and **Ralph** tracks issues → labelled results come back to the user. [docs/concepts/architecture.md]

- Agents run as independent sub-agents with their own context windows. "Agents never see each other's conversations — the coordinator orchestrates coordination." [architecture.md]
- Saying "team, ..." triggers a parallel fan-out. Naming an agent ("Dallas, fix X") routes the work to that agent alone. [first-session.md]

### Roles
- Squad has 20 built-in base roles. 13 are engineering roles (lead, frontend, backend, fullstack, reviewer, tester, devops, security, data, docs, ai, designer, fact-checker) and 8 are business roles. The charter content is adapted from `msitarzewski/agency-agents`. [features/built-in-roles.md]
- **Scribe** (silent) and **Ralph** (work monitor) are on every roster. Ralph watches GitHub/GitLab issues; `squad watch|triage|loop` runs it as a polling loop and can auto-dispatch Copilot sessions with `--execute`. [architecture.md, README]
- Agents get names from a persistent "casting" universe: Alien (Ripley, Dallas, Lambert, Hicks), Ocean's Eleven (Danny, Rusty, Linus, Basher), The Usual Suspects (Keaton, McManus, Fenster). Names are kept in `.squad/casting/registry.json`. [first-session.md, new-project.md, README]
- **Reviewer lockout:** if the Lead or Tester rejects an agent's work, the original agent may not revise it. The coordinator must reassign the work or escalate to the human. [features/reviewer-protocol.md] Note: open issue #1828 asks for this lockout to be *enforced* in the gh-aw workflow path, so it is not uniformly hard-enforced today.

### Memory (all plain Markdown, committed to git)
| Layer | File | Read by |
|---|---|---|
| Shared decisions ("shared brain") | `.squad/decisions.md` | every agent before every spawn |
| Parallel write drop-box | `.squad/decisions/inbox/{agent}-{slug}.md` → merged into `decisions.md` by Scribe | Scribe |
| Personal history | `.squad/agents/{name}/history.md` (archived at ~12 KB) | owning agent only |
| Skills | `.copilot/skills/{name}/SKILL.md` (starter + "earned", with confidence low/med/high) | all agents |
| Directives | Captured when you say "always / never / from now on..."; stored in `decisions.md` | all agents |

Sources: [docs/concepts/memory-and-knowledge.md]. Skills moved from `.squad/skills/` to `.copilot/skills/`, and bundled skills now install to `.github/skills/` (v0.13.x notes, PRs #1260, #1304). The docs are not fully consistent on these paths.

- The drop-box pattern exists to avoid merge conflicts when agents write in parallel. `squad init` also adds `.gitattributes` `merge=union` rules. [GH blog, new-project.md]
- On top of this, the CommandLine article describes "governed memory" classes. PR #1145 reports this cut agent context by about 55% while keeping recall at 1.0. [CommandLine]

## 3. Requirements

| Item | Requirement | Source |
|---|---|---|
| Runtime UI | **GitHub Copilot CLI** (`copilot`), installed separately; recommended interface. VS Code Copilot Chat also works (agent picker → Squad), with limits: session model only, no SQL tool | Squad installation.md, choose-your-interface.md |
| Copilot subscription | "An active GitHub Copilot subscription". Business/Enterprise admins can disable Copilot CLI by policy | GitHub Docs: install Copilot CLI |
| Squad CLI | npm: **Node.js ≥ 22.5**, npm ≥ 10. Homebrew cask, WinGet, install script and direct archives bundle Node | README, installation.md |
| Copilot CLI | npm install needs Node 22+; also brew/winget/script | GitHub Docs |
| OS | macOS, Linux, Windows. Copilot CLI on Windows needs PowerShell 6+ or WSL | GitHub Docs, installation.md |
| Other | Git; `gh` CLI (only for Issues/PRs/Ralph); ideally a GitHub remote | README |

Windows has the most open bugs: #2106 (EPERM fsync on init, dev branch), #2070 (`watch --execute` truncates the prompt in cmd.exe), and #1803/#1806 (flaky Windows test suite). Record on macOS.

## 4. Install and setup (no global install was done for this research)

```bash
mkdir demo && cd demo && git init
npm install -g @bradygaster/squad-cli      # or: brew install --cask bradygaster/squad/squad
squad init                                  # or: squad init --preset default (pre-made team)
gh auth login                               # only for Issues/PRs/Ralph
copilot --agent squad --yolo                # --yolo: Squad makes many tool calls
```

- `squad init` is idempotent. It creates `.github/agents/squad.agent.md` (the coordinator prompt), `.squad/` templates, workflows and `.gitattributes`. [installation.md, faq.md]
- `squad doctor` checks the setup. `squad upgrade` refreshes Squad-owned files and never touches team state. [README]
- To try Squad without global install or repo pollution: run `npx @bradygaster/squad-cli` in a side repo and symlink it in through `.git/info/exclude`. [Tamir Dresher blog 2026-02-17] For the demo, use a throwaway repo, `npx`, or the Homebrew cask on the recording machine.

## 5. Typical run on a from-scratch project

Based on [docs/get-started/first-session.md] and [docs/scenarios/new-project.md]:

1. `squad init`, then `copilot --agent squad`. Squad greets you: "Hey Brady, what are you building?"
2. Describe the project (stack plus features). The coordinator proposes a roster, e.g. `Hicks — Lead, Ripley — Frontend, Dallas — Backend, Lambert — Tester, Scribe — (silent)`. Answer "yes", or adjust the team.
3. Give a first targeted task ("Dallas, set up the Express server"). `.squad/` is populated: team.md, routing.md, charters, histories, casting.
4. Run a fan-out: "Team, build the recipe listing page..." The Lead defines the API contract while Frontend, Backend and Tester work in parallel. The Tester writes tests *from requirements* while the code is still being built.
5. Results come back labelled per agent. "Where are we?" gives a status built from the logs. "Show me the decisions" shows the accumulated `decisions.md`.
6. Add a directive: "Always use Zod for API input validation" gets the reply "📌 Captured".
7. Optional: `squad export` produces a portable JSON of the team.

The docs say: "First session is slowest. Agents have no history yet. After 2–3 sessions, they know your conventions."

## 6. Artifacts produced

```
.github/agents/squad.agent.md   # coordinator prompt (~78 KB in v0.13.x)
.github/workflows/ ...          # Squad workflows (triage, labels, copilot auto-assign...)
.gitattributes                  # merge=union rules
.squad/team.md  routing.md  decisions.md  decisions/inbox/  ceremonies.md
.squad/casting/{policy,registry,history}.json
.squad/agents/{name}/{charter,history}.md
.squad/identity/{now,wisdom}.md
.squad/log/  .squad/orchestration-log/      # session + spawn logs
.squad/config.json              # model prefs, economy mode
.copilot/skills/  (.github/skills/)          # skills
```

Plus whatever code the agents write. Sources: [README "What Gets Created"], [memory-and-knowledge.md], [first-session.md]. All of this is meant to be committed, so it can serve as the "artifacts in the repo" for the Topic.

## 7. Duration and cost

### Duration
- There are **no official timings** for a full run. The docs only say "full team in under a minute" for setup [new-project.md] and that the first session is the slowest.
- The CommandLine article's stress test ("Squad Places") closed a feedback → commit → deploy loop "within roughly two hours". That is a long autonomous loop, not a demo run.
- **Estimate (not sourced):** a from-scratch demo of casting plus 2–3 fan-out tasks takes tens of minutes of wall time. Plan to record and time-lapse it.

### Cost and billing
- Since **2026-06-01**, Copilot uses **GitHub AI Credits** instead of premium requests. Credits are "consumed based on token usage, including input, output, and cached tokens, according to the published API rates for each model". Included monthly credits match the plan price: Pro $10, Pro+ $39, Business $19/user, Enterprise $39/user. [GitHub blog: usage-based billing; changelog 2026-06-01]
  - The legacy GitHub Docs page still says "each prompt to Copilot CLI uses one premium request"; the about-Copilot-CLI page already says AI credits. Treat premium requests as legacy.
- The old premium-request model charged only user prompts, not autonomous tool calls. Token billing instead charges for **every sub-agent spawn's full context**. Squad's design (parallel specialists, each re-reading charter, decisions and history, plus a ~78 KB coordinator prompt) is token-heavy. Issue #1439 measured `squad.agent.md` at ~18K tokens "injected every turn", about $0.27 of input per turn on Opus.
- Squad's own cost controls:
  - Per-agent model tiers: code work goes to the standard tier, docs/Scribe to the fast tier, and the fallback chain never moves *up* a tier.
  - "economy mode".
  - Rate-limit circuit breaker.
  - `squad cost` / orchestration-log token rows.
  [features/model-selection.md, rate-limiting.md, cost-tracking.md]
- Caveats from open issues:
  - `defineBudget()` limits are validated but **never enforced** (#1715).
  - `squad cost` parses agent-written Markdown with regex (#1716).
  - The coordinator may bypass per-agent model selection (#1720).
  - Economy mode savings were unproven at v0.9.1 (#585).
- **Recommendation:** run the recording once on a budgeted account, read the AI-credit usage in GitHub billing before and after, and quote that number in the Session.

## 8. Known limitations and issues

- Alpha status, fast churn (v0.8 → v0.13 in about 6 months) and docs drift:
  - `whatsnew.md` still shows v0.9.1 as current (#2071).
  - `new-project.md` still shows the old `.ai-team/` paths.
  - The skills path differs across pages.
- Directives and charters are **prompt-level guidance, not hard constraints**: "Directives are context-aware guidelines, not hard constraints" [memory-and-knowledge.md]. The authors list as unresolved: role drift, coordination complexity, compaction losing rationale, and prompt saturation [CommandLine]. #1836: Scribe archival can delete history because its safety rules are prompts, not gates.
- Heavy prompt makes each turn expensive (#1439). Token cost scales with the number of agents.
- VS Code has reduced capability (session model only, no SQL tool), and `tools: *` causes problems in newer VS Code (#1785).
- Needs `--yolo` (auto-approve all tools) to be usable, so run it only in a throwaway repo or container.
- Bot assignment to `@copilot` via `gh` does not work; it needs a label workflow plus a classic PAT secret [faq.md].
- Windows bugs (see §3).
- The interactive `squad` shell is deprecated in favour of `copilot --agent squad`.

## 9. NDC Oslo 2026 talk

- **"Building Multi-Agent AI Teams That Live in Your Repo"**: Tamir Dresher and Brady Gaster, Thursday 17 Sep 2026, 09:00–10:00, Room 2. NDC Oslo ran 14–18 Sep 2026. [ndcoslo.com agenda]
- Abstract points:
  - Lead triages, developer writes code, tester writes tests, reviewer gates quality, "all coordinated through a single prompt".
  - Persistent memory across sessions.
  - Conflict-free parallel execution (drop-box).
  - **Reviewer lockout** (no self-approval).
  - Team state in git.
  - Security gates for trustworthy output.
  - **Live demo** with agents spawned in real time.
- **Slides/recording:** none found on 2026-10-02. NDC usually publishes to its YouTube channel weeks to months later. Check `youtube.com/@NDC` later.
- Related primary material by the authors:
  - GitHub Blog, Brady Gaster, 2026-03-20: "How Squad runs coordinated AI agents inside your repository".
  - Microsoft Command Line, Gaster and Dresher, 2026-05-28: "Squad: Human-led agentic teams..." (architecture, governed memory, limitations).
  - MS Agent Framework devblog, 2026-07-30 (Shawn Henry, Tamir Dresher): Squad.Agents.AI for .NET.
  - David Giard "Technology and Friends" ep. 901, 2026-05-04 (Brady interview, video).
  - Tamir Dresher blog series (tamirdresher.com, 2026).

## 10. Sample project ideas for the recorded demo (F# team)

Squad's examples are JS/Go, but nothing in it is stack-specific. The Lead and devs follow whatever stack you describe, and a `"Always use F#..."` directive pins it.

1. **F# shipment-tracking CLI or minimal API**: reuse the logistics domain from McpServerDotNet/ValueObjects. Prompt: "an F# (.NET 9) minimal API for tracking parcels: create shipment, add scan event, get status; xUnit/Expecto tests". It shows the Lead defining a contract, the backend dev and tester working in parallel, and decisions such as "use DU for status". Small, familiar, and it links the Topics.
2. **F# Markdown-to-slides converter (console tool)**: parse Markdown, emit reveal.js HTML. Clear split for Lead, Core dev (parser), Tester (property tests with FsCheck) and Docs (README). It is self-referential to this repo (reveal.js slides) and has no infrastructure.
3. **Todo/kata with a twist: bowling or bank-account kata, then a second session that adds a feature**: very short, so cost and duration stay low. The second session shows memory: agents recall decisions and history, and "Always use Result instead of exceptions" becomes a directive applied in the second session. Best for the short **live** fragment.

Recording tips:
- Use a fresh repo on macOS with `--yolo` and the CLI rather than VS Code (full feature set).
- Commit `.squad/` after each session so the audience can browse the artifacts.
- Note the credits consumed.

## Sources

- Repo and README: https://github.com/bradygaster/squad (v0.13.1 release: https://github.com/bradygaster/squad/releases/tag/v0.13.1)
- Docs site: https://bradygaster.github.io/squad/. Files read from `docs/src/content/docs/`: concepts/architecture.md, concepts/memory-and-knowledge.md, get-started/installation.md, get-started/first-session.md, get-started/choose-your-interface.md, scenarios/new-project.md, features/{built-in-roles,model-selection,cost-tracking,rate-limiting,reviewer-protocol}.md, guide/faq.md, whatsnew.md
- Squad issues: #585, #1080, #1439, #1715, #1716, #1720, #1785, #1803, #1828, #1836, #2070, #2071, #2106
- GitHub Blog, B. Gaster, 2026-03-20: https://github.blog/2026-03-20-how-squad-runs-coordinated-ai-agents-inside-your-repository/
- Microsoft Command Line, Gaster and Dresher, 2026-05-28: https://commandline.microsoft.com/squad-github-copilot-agent-teams-architecture-durable-memory/
- MS Agent Framework devblog, 2026-07-30: https://devblogs.microsoft.com/agent-framework/building-agent-teams-with-agent-framework-github-copilot-cli-and-squad
- Tamir Dresher, 2026-02-17: https://www.tamirdresher.com/blog/2026/02/17/trying-squad-without-touching-your-repo
- David Giard ep. 901: https://davidgiard.com/brady-gaster-on-squad-and-a-multi-agent-ai
- NDC Oslo talk: https://ndcoslo.com/agenda/building-multi-agent-ai-teams-that-live-in-your-repo (day: https://ndcoslo.com/agenda/thursday)
- Copilot CLI install/prereqs: https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli. About CLI: https://docs.github.com/en/copilot/concepts/agents/about-copilot-cli
- Billing: https://github.blog/news-insights/company-news/github-copilot-is-moving-to-usage-based-billing/, https://github.blog/changelog/2026-06-01-updates-to-github-copilot-billing-and-plans/, legacy: https://docs.github.com/en/copilot/concepts/billing/copilot-requests
