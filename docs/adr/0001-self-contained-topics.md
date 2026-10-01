# Each Topic is self-contained

Topics use different stacks (.NET, TypeScript, Python, …) and attendees often take a single Topic in isolation, so every Topic folder carries everything it needs: its own build files, its own stack-specific `.gitignore`, and its own vendored copy of reveal.js for the slides. Nothing in the repo root is shared at build or presentation time.

## Consequences

- reveal.js is duplicated in every Topic (~1–2 MB each). This is deliberate: do not extract it into a shared root folder, because that breaks a Topic copied out of the repo.
- reveal.js is vendored rather than loaded from a CDN so slides work offline and on corporate networks that block CDNs.
- reveal.js versions may drift between Topics. Each Topic is upgraded independently, if ever.
