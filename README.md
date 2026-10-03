# Knowledge Sharing Sessions

Materials for the knowledge sharing sessions I run, mostly for my team at work. Each folder is one **Topic**: a self-contained mini project with demo code, slides and a presenter's Script. Clone the repo, open a Topic and follow its README.

## Topics

| Topic | Stack | What it's about |
|-------|-------|-----------------|
| [AssertObjectPattern](./AssertObjectPattern) | F#, .NET 8 | Wrapping technical assertions in business-readable assert objects. |
| [McpServerDotNet](./McpServerDotNet) | F#, .NET 10, Aspire, Keycloak | An MCP server over a logistics REST API, used from GitHub Copilot, with auth switchable between none, API key and OAuth. |

## Conventions

Terms (Topic, Session, Script, Speaker notes) are defined in [GLOSSARY.md](./GLOSSARY.md). Design decisions live in [docs/adr/](./docs/adr).

### Topic layout

```
<TopicName>/              # PascalCase, no date
  README.md               # for attendees: what, why, how to run, Sessions
  .gitignore              # stack-specific ignores
  <code + build file>     # .sln / package.json / pyproject.toml / …
  materials/
    slides.html           # reveal.js slides with Speaker notes, work offline
    reveal.js/            # vendored copy, one per Topic
    script.md             # presenter's Script, in the Session's language
```

Every Topic is self-contained: own build, own `.gitignore`, own copy of reveal.js. Nothing is shared from the repo root ([ADR-0001](./docs/adr/0001-self-contained-topics.md)). Don't use company names in new Topics; describe Session audiences generically (e.g. `Internal team`).

### Issues and branches

- One GitHub issue per Topic, labelled `topic:idea` → `topic:in-prep`. Close it when the Topic is merged to `master`.
- Branches: `topic/<TopicName>` for Topic work, `chore/<description>` for repo-level changes.

## Adding a new Topic

1. Open (or pick up) the Topic's issue and move it to `topic:in-prep`.
2. Create branch `topic/<TopicName>`.
3. Copy `_template/` to `<TopicName>/`.
4. Replace `.gitignore` with the matching template from [github/gitignore](https://github.com/github/gitignore).
5. Add the demo code. Make sure it builds and runs locally.
6. Write `materials/slides.html` (with Speaker notes) and `materials/script.md`.
7. Fill in the Topic's `README.md`.
8. Add a row to the Topics table above.
9. Open a PR, merge, close the issue.
10. After each Session, add a row to the Topic's Sessions table.
