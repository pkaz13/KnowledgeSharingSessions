# Research: "10 tips to level up your AI-assisted coding" and interactive HTML code review

Ticket: #8 (map #4, Topic **AgentCodeReview**). Researched 2026-10-02.

Legend: **[talk]** = confirmed from a recording of the talk. **[source]** = confirmed from the named primary source. **[inferred]** = my inference, not stated by the source.

## TL;DR

- Speaker: **Aleksander Stensby** (founder of GritAI, Anthropic community ambassador in Norway, runs the Claude Code meetup in Oslo). [source: NDC Oslo agenda page, talk]
- The talk is a moving target: he re-cuts it per conference ("maybe they're not exactly tips, they may be categories of tips"). No public slides, repo, or blog post found. Recordings on the NDC YouTube channel are the primary source. [talk]
- **He did not propose HTML specifically for code review.** He proposed asking the agent for an "interactive or engaging HTML file" instead of a long Markdown **plan**, as a way to stay the human in the loop on the *input* side of the bottleneck. For the *output* side (review/verification), his advice was: let the agent verify its own work (browser tools, a second agent checks), give fast feedback (Agentation, DOM annotation), and "be the human in the loop … but figure out how you can make it easier to check". [talk]
- The HTML-for-code-review idea comes from the trend he references ("a bit of a trend online recently"): **Thariq Shihipar (Claude Code team, Anthropic), "Using Claude Code: The unreasonable effectiveness of HTML"**, May 2026, with a gallery of example pages, including an annotated PR review, a PR write-up for reviewers, a module map, and a "quiz me before I merge" report. [source] The link between the two is my inference from timing and wording. [inferred]
- With GitHub Copilot this works today: a reusable **prompt file** (`.github/prompts/*.prompt.md`) or **agent skill** (`.github/skills/<name>/SKILL.md`, also read by Copilot CLI), run in agent mode or in `copilot -p`, writes a self-contained `review.html` that you open in VS Code's integrated browser. [source: VS Code and GitHub docs]

## 1. The talk

### Identity

- NDC Oslo agenda page: "10 tips to level up your AI-assisted coding", Aleksander Stensby, Wednesday 11:40–12:40, Room 2, category AI / AI-Assisted Development. The abstract lists context engineering, planning, verification, model selection, guardrails through subagents and hooks, and moving from chat prompting to autonomous agent loops, and says: "your review becomes the bottleneck, and staying the human in the loop is a skill in itself." No slides or links on the page. [source: https://ndcoslo.com/agenda/10-tips-to-level-up-your-ai-assisted-coding]
- Speaker profile: https://ndcoslo.com/speakers/aleksander-stensby, LinkedIn https://no.linkedin.com/in/aleksanderstensby. He ends talks with "follow me on LinkedIn … check out my YouTube channel". [talk]

### Recordings used (NDC YouTube channel)

| Delivery | Video | Uploaded |
|---|---|---|
| NDC AI 2026 (Oslo) | https://www.youtube.com/watch?v=K7dBRuSDWTw | 2026-07-01 (62 min) |
| NDC Copenhagen 2026 | https://www.youtube.com/watch?v=JpXlTDQHEoo | 2026-06-22 |
| NDC Sydney 2026 | https://www.youtube.com/watch?v=wZxwqr-W1Jw | 2026-05-21 |
| NDC London 2026 | https://www.youtube.com/watch?v=NbenxkeJkEA | 2026-02-25 |
| NDC AI 2025 / Manchester 2025 | HHiecFMk48o / z8XWvBpL_EA | older versions, not used |

No recording titled "NDC Oslo 2026" was found on YouTube as of 2026-10-02. NDC AI 2026 is also held in Oslo and its content matches the NDC Oslo abstract, so it is the best proxy. I read the auto-generated transcripts of the first three. [source]

### The tips (NDC AI 2026 delivery, in order)

He numbers loosely; only "tip zero" and "tip number nine" are said out loud in this delivery. The grouping below is mine, the content is his. [talk, grouping inferred]

0. **Mindset**: pair-programming mindset, not "lazy" acceptance. Compound engineering (the agent should learn from every mistake). "Just ask Claude": re-check what is possible with each new model.
1. **Context is king**: the context window is a finite "glass", quality drops at ~70–80% full; watch it (`/context`, status line), guide `/compact`, or summarise to a file and `/clear`. Prefer CLI/scripts so data does not pass through inference.
2. **Rules and memory**: CLAUDE.md / AGENTS.md (point one at the other so all harnesses share them), memory files; prune them. Rules are scaffolding that can hold newer models back: after each model release, ask the model what to remove.
3. **Skills**: "the number one thing to invest in in 2026"; portable across harnesses; two kinds (your preferences/workflows, new capabilities); scripts for deterministic steps; `disable-model-invocation`, `user-invocable`; build skills from a finished piece of work with the skill-creator skill; don't bulk-download skills (attack surface).
4. **Always start with a plan**: plan mode (also a safe read-only mode for exploration); persist the plan to a file so the agent can verify against it later. Then "chasing bottlenecks": coding is no longer the bottleneck, so the bottleneck moves to (a) describing intent and (b) checking/verifying. Rethink processes, including "why do we do code reviews the way we've done code reviews?"
   - Input side: ask for an **interactive HTML** instead of a long Markdown plan; superpowers' brainstorming skill (spins up a local web server with clickable options).
   - Output side: give the agent the same tools you use (browser, Chrome DevTools MCP) so it verifies its own work; Agentation / DOM annotation for fast UI feedback; quote from the Claude Code team: "We used to verify that Claude did the work right. Now, we verify that it's doing the right work."; `/goal` loops where a *different* agent judges whether the goal is met.
5. **Use the best model**: the time spent correcting a weaker model costs more than tokens; use `/usage` to find what burns tokens.
6. **Subagents**: separate context windows ("infinite context"), nesting, `context: fork`; don't over-specify predefined subagents.
7. **Hooks**: deterministic guardrails (e.g. block `rm` in PreToolUse), lint on file change, a Stop hook as the simplest "Ralph Wiggum" loop.
8. **Compound engineering / `/insights`**: reflect after each session, turn lessons into rules or skills; `/insights` analyses 30 days of sessions into an HTML report with copy-paste suggestions.
9. **Tools / MCP vs CLI** and **work in parallel**: worktrees, GitHub issues assigned to the agent, remote control.
10. **Loop engineering**: prompts → context → harness → loops; give a goal and a stop condition, persist state; UltraCode. Close: ask the agent to build tools, remind it that it can call itself headless, and **"remember to be the human in the loop … we still need to check, but figure out how you can make it easier to check."**

Variations in other deliveries [talk]: Sydney 2026 numbers subagents as "tip number six" and ends with "Last tip, be the human in the loop … we need to still check the PRs. Don't let everything just fly by. But be smart about it. You can have the AI check the AI before you invest your time. My favorite prompt is *make it better* … or *are you sure?*" London 2026 and Sydney 2026 have no HTML segment; it first appears in the June/July 2026 deliveries, after Thariq's May 2026 post.

A secondary summary on daily.dev (https://daily.dev/posts/10-tips-to-level-up-your-ai-assisted-coding---aleksander-stensby---ndc-ai-2026-kjyhgwek6) lists a slightly different 10. Don't rely on it; it is a machine summary.

### What he said about HTML, verbatim [talk]

NDC AI 2026, ~33:00:

> "AI loves markdown. And over time, it becomes challenging for me to stay engaged in a very long markdown plan. So, I sort of end up converging towards, 'Yeah, that looks good. Let's just go on' … that's very, very dangerous. … One way to stay in the loop … something that's become a bit of a trend online recently … simply to say to your AI, 'Instead of giving me this markdown file, can you provide it to me in a visually appealing or interactive or engaging HTML file?' … you can then give it styles, and you just turn it into a skill, so you never have to say that ever again."

NDC Copenhagen 2026, ~32:20:

> "You can even say, give me ways to provide you feedback. And all of a sudden, we move from reading a long text document to actually having an interactive experience where we can look at architecture visually … design proposals … directions, choices that maybe we became lazy about over the past sort of 6 months. Take back control and use HTML as one means. … HTML is super powerful. I think we underestimate the things we can do with something as simple as HTML."

So the speaker's case for HTML is about **engagement** (the human actually reads it) and **two-way feedback**, applied to plans and design choices. Applying it to reviewing agent-produced diffs is the natural mirror of his "other side of the bottleneck" argument, but he did not demo it. [inferred]

## 2. The primary source of the HTML pattern: Thariq Shihipar (Anthropic)

- Blog post: "Using Claude Code: The unreasonable effectiveness of HTML", Thariq Shihipar, published May 20, 2026 (originally an X post in early May). https://claude.com/blog/using-claude-code-the-unreasonable-effectiveness-of-html (redirects to claude.dev/blog/...). [source]
- Gallery of 20 example pages + "Know your unknowns" set (11 pages, each shows its prompt): https://thariqs.github.io/html-effectiveness/ (repo https://github.com/ThariqS/html-effectiveness). [source]
- Coverage: Simon Willison, 2026-05-08, https://simonwillison.net/2026/May/8/unreasonable-effectiveness-of-html/; InfoQ, 2026-06-24, https://www.infoq.com/news/2026/06/anthropic-html-markdown-agent/. [source]

Key claims from the post [source]:

- Why: information density (tables, SVG, CSS, JS interactions); readability ("I tend to not actually read more than a 100-line Markdown file"); easy to share as a link; **two-way interaction**, e.g. "let you copy these changes into a prompt to paste back into Claude Code"; the agent can pull context from the repo, git history, and MCPs.
- Getting started: just prompt "make an HTML file"/"make an HTML artifact"; build a skill once the pattern recurs.
- **Code review and understanding** section: "with HTML, we can render diffs, annotations, flowcharts, and modules. Use HTML to understand code that the agent has written, to review code, or to explain a PR to someone reviewing your code." Example prompt, verbatim:
  > "Help me review this PR by creating an HTML artifact that describes it. I'm not very familiar with the streaming/backpressure logic, so focus on that. Render the actual diff with inline margin annotations, color-code findings by severity and whatever else might be needed to convey the concept well."
- Custom editing interfaces: always end with an export ("copy as prompt"/"copy as JSON"); listed uses include "Annotating a document, transcript, or diff and exporting the annotations".
- Closing argument is the same as Stensby's: "As Claude takes on more, I'd noticed I was reading plans less closely … HTML turned out to be exactly that. I feel more in the loop now."

### Review-relevant examples in the gallery (what they actually contain) [source]

| Page | Contents |
|---|---|
| `03-code-review-pr.html` "Annotated pull request" | PR header (branch, +/−, files); "What this PR does" (3 bullets); **risk map** colouring each file *safe / worth a look / needs attention*; per-file rendered diff with line numbers; **margin annotations tagged Blocking / Nit** pinned to lines; one-line summaries for trivial files; "Suggested next steps". |
| `17-pr-writeup.html` "PR writeup for reviewers" (author side) | The prompt shown at top; TL;DR; Why; **Before/After** comparison; **file-by-file tour "ordered for reading, not alphabetically"** with the *why* per file and key snippets; **"Where to focus your review"** (numbered, with file:line and the failure mode if wrong); "What I deliberately did not do"; test plan; rollout plan; side nav. |
| `04-code-understanding.html` "Module map" | Request-path diagram (boxes and arrows); numbered call-stack walkthrough with file:line and collapsible source; key-files list; trust boundary called out. |
| `unknowns/11-change-quiz.html` "Quiz me before I merge" | Prompt: "I want to make sure I understand everything that happened in this change before I merge. Give me an HTML report on the export-feature diff — context, intuition, what was done — with a quiz at the bottom that I must pass." Contains a before/after mental-model diagram, "three non-obvious behaviors" (What / Why / Where file:line), and a quiz whose wrong answers link back to the section you skimmed. |
| `unknowns/09-implementation-notes.html` | Agent keeps a running log during the build: plan-confirmed steps, discoveries, **deviations from the plan** (what the plan said / what the code revealed / conservative choice / revisit), filter "needs your judgment". Useful input for the reviewer. |

## 3. Related practices and tools

| Practice / tool | What it does | Source |
|---|---|---|
| Simon Willison, *Agentic Engineering Patterns*: **Linear walkthroughs** (Feb 2026) | Ask the agent to "plan a linear walkthrough of the code that explains how it all works in detail" and build it with **Showboat** (`showboat note` for commentary, `showboat exec` to embed real snippets/command output, so the doc cannot invent code). Motivation: **cognitive debt**. | https://simonwillison.net/guides/agentic-engineering-patterns/linear-walkthroughs/, https://github.com/simonw/showboat |
| Same guide: **Interactive explanations** | Agent builds an animated/interactive HTML page that shows how an algorithm works. "When we lose track of how code written by our agents works we take on cognitive debt." | https://simonwillison.net/guides/agentic-engineering-patterns/interactive-explanations/ |
| **superpowers** (obra) brainstorming *visual companion* | Local server watches a dir for HTML the agent writes; user clicks options, and the selections are written to an events file the agent reads next turn. Stensby demoed this. The plugin also ships `requesting-code-review`, `verification-before-completion`. | https://github.com/obra/superpowers (skills/brainstorming/visual-companion.md) |
| Anthropic `/insights` | Analyses past sessions, outputs an HTML report with copy-paste suggestions. Stensby demoed this. | talk |
| **CodeRabbit** walkthrough / Change Stack | PR comment summarising changes, sequence diagrams, review effort; "Change Stack reorganizes a pull request from a flat file list into a structured, layer-by-layer walkthrough with range-specific summaries and diagrams". Closest SaaS analogue to "diff grouped by intent". | https://docs.coderabbit.ai/guides/configuration-overview |
| **GitHub Copilot code review** | Native PR reviewer; reads `.github/copilot-instructions.md`, `.github/instructions/*.instructions.md`, AGENTS.md, and repo **agent skills** from the head branch; overview comment includes an approval assessment (Copilot approvals in public preview). Produces PR comments, not an HTML page. | https://docs.github.com/en/copilot/concepts/agents/code-review |
| Copilot CLI `/review`, `/diff`, built-in `code-review` agent | `/review [PROMPT]` runs the code-review agent ("high signal-to-noise … bugs, security issues, and logic errors. Will not modify code"); `/diff` is an interactive diff viewer with comments. | https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-command-reference |

Position for the Session [inferred]: the bots (Copilot code review, CodeRabbit, `/review`) answer "is anything wrong?". The HTML review guide answers "do *I* understand what changed, why, and where to look?". It is a reading aid for a human reviewer, not a replacement for automated review. The two combine: feed the bot's findings into the HTML page as annotations.

## 4. Doing it with GitHub Copilot

All mechanisms below are documented. [source] How they fit together is my proposal. [inferred]

| Mechanism | Location | Use for HTML review |
|---|---|---|
| Prompt file | `.github/prompts/review-html.prompt.md`; frontmatter `description`, `agent` (`agent`/`plan`/custom), `model`, `tools`, `argument-hint`; variables `${input:base}`, `${selection}`; invoked as `/review-html` in Chat | Simplest: one checked-in prompt the team runs in **agent mode**. [VS Code docs: prompt files] |
| Agent skill | `.github/skills/review-html/SKILL.md` (also `.claude/skills/`, `.agents/skills/`; personal `~/.copilot/skills/`); frontmatter `name`, `description`, optional `argument-hint`, `user-invocable`, `disable-model-invocation`, `context: fork`; can bundle an HTML template/CSS and scripts | Better long-term: bundle a fixed `template.html` + a script that emits the diff as JSON, so output is consistent. The same skill works in VS Code, Copilot CLI, the cloud agent and Copilot code review. [VS Code docs: agent skills; CLI reference] |
| Custom instructions | `.github/copilot-instructions.md`, `.github/instructions/*.instructions.md` (`applyTo`), AGENTS.md | Team-wide review conventions (F# idioms, what counts as "needs attention"). |
| Custom agent | `.github/agents/*.agent.md` | Optional "reviewer" persona with read-only tools. |
| Copilot CLI | `copilot -p "<prompt>" --allow-all-tools` (non-interactive, `--allow-all-tools` required programmatically); `/review`, `/diff`, `/skills`, `--agent=` | Batch-generate `review.html` for a branch from a script or CI. |
| Viewing | VS Code **Integrated Browser** ("Open in Integrated Browser"; agents can read/screenshot it, v1.110+) | Open the generated page next to the code during the demo. [https://code.visualstudio.com/docs/debugtest/integrated-browser] |

Sketch of a prompt file (illustrative, untested) [inferred]:

```markdown
---
description: Generate an interactive HTML review guide for the current branch
agent: agent
argument-hint: base branch (default main)
---
Run `git diff ${input:base:main}...HEAD` and `git log ${input:base:main}..HEAD`.
Read the changed files and the linked issue/plan if present.
Write ONE self-contained file `review/review.html` (inline CSS/JS, no CDN) that helps me,
a human reviewer, understand and judge this change. Include: TL;DR, intent groups
(not file order), risk map, rendered diff with margin notes tagged
blocking/question/nit, before/after diagram (inline SVG), plan-vs-implementation check,
tests added/missing, "where to focus" list, and a short quiz.
Every claim must cite file:line. Do not invent code: quote it from the diff.
Add per-hunk "approve / question" toggles and a "Copy feedback as prompt" button.
```

## 5. What the HTML page should contain (concrete ideas)

Columns: where the idea comes from. (T) = Thariq gallery, (S) = Stensby talk, (W) = Willison patterns, (I) = my inference.

1. **Header**: branch → base, commits, +/−, files, link to issue/plan. (T)
2. **TL;DR + intent** in 3 bullets: what and *why*. (T)
3. **Change groups by intent, not alphabetically**: e.g. "domain model change", "new use case", "wiring", "tests", "mechanical renames (collapsed)". Order for reading. (T "ordered for reading"; CodeRabbit Change Stack; grouping by intent is (I))
4. **Risk map**: each file/group tagged safe / worth a look / needs attention, with the reason. (T)
5. **Rendered diff with margin annotations**: severity tags (blocking / question / nit), each pinned to a line; trivial hunks collapsed. (T)
6. **Before/after diagram** (inline SVG): data flow, call path, or type relationships. (T, W)
7. **Plan vs implementation**: each acceptance criterion from the persisted plan, marked met / partially / not, with evidence; list of **deviations** the agent made and why. (S "persist the plan so it can verify against it"; T implementation notes)
8. **Non-obvious behaviours**: What / Why / Where (file:line). (T quiz page)
9. **Tests and verification evidence**: tests added, what they cover, command output actually run (Showboat-style, so the page cannot invent it). (W, S "give it a way to verify its own work")
10. **"Where to focus your review"**: numbered, each with the failure mode if wrong; plus "what I deliberately did not do". (T)
11. **Self-check / quiz before merge**: 4–6 questions; wrong answers link back to the section. (T)
12. **Two-way feedback**: per-hunk approve/question toggles + comment boxes, then a **"Copy feedback as prompt"** button to paste back into Copilot Chat. (T export pattern, S "give me ways to provide you feedback", superpowers events)
13. **Reviewer checklist**: team-specific items. (I)

F#-specific additions for this team [inferred]:

- Show changes in **compile order** (`.fsproj` `<Compile Include>` order) since F# files are order-dependent; flag any `.fsproj` reordering.
- Highlight **public surface changes**: signatures, `.fsi` files, record fields, and **discriminated-union cases added/removed**, and list the `match` expressions that are affected (incomplete-match warnings).
- Flag new `mutable`, `failwith`/exceptions in domain code, `Option.get`/`List.head`, and C#-style nulls crossing into F# code.
- Type-level diagram: which types/modules changed and who depends on them.

## 6. Caveats

- No slides, repo, or written tip list from Stensby exist publicly as far as I could find. The tip list above is reconstructed from transcripts (auto-captions, so small wording errors are possible).
- The NDC Oslo 2026 delivery itself has no published recording that I found; the NDC AI 2026 recording (also Oslo) is used as a proxy.
- "HTML for code review" is attributed to Thariq Shihipar / Anthropic, not to Stensby. Present it on slides as "the trend Stensby pointed to".
- Copilot docs move fast (CLI `/diff` is marked experimental; Copilot approvals are in public preview). Re-check before the Session.
