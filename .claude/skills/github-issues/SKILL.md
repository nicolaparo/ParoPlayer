---
name: github-issues
description: Use when the user asks to track work, missing features, bugs or follow-ups for ParoPlayer as GitHub issues, to list or triage them, or to close them. Uses the gh CLI against nicolaparo/ParoPlayer.
---

# GitHub issues for ParoPlayer (gh CLI)

Repo: `nicolaparo/ParoPlayer` (private). `gh` is authenticated as `nicolaparo`. Run from the repo root so `gh` picks the repo, or pass `-R nicolaparo/ParoPlayer`.

## Rules
- Create issues only when the user asks. An issue is published text on the user's account: no secrets, tokens, personal paths, device ports or e-mail addresses in titles or bodies.
- Search first to avoid duplicates: `gh issue list --state all --search "<keywords>" --json number,title,state`.
- One issue = one deliverable that can be finished and verified. Split bigger items, and link them (`Part of #12`, `Blocked by #7`).
- Title: imperative, under 70 characters, no prefix like "Feature:" (labels carry that). Example: `Add album, artist and genre drill-down in Library`.
- Body (English), in this order: **Why** (user-visible problem, 1-2 sentences), **Scope** (checklist of concrete steps), **Done when** (how to verify, usually a test or an on-device check), **Notes** (links to `docs/...`, constraints such as offline-first, no tables, Tailwind literal classes).
- Pass the body with `--body-file` or a quoted heredoc so markdown and `$` survive. Never build the body with double-quoted shell strings.
- Labels: one type (`enhancement` or `bug`, plus `documentation` when it is docs only) and one or more area labels: `playback`, `library`, `playlists`, `download`, `metadata`, `ui`, `android-auto`, `wifi-transfer`, `verification`. Create a missing label with `gh label create <name> --color <hex> --description "<text>"`.
- Mention the commit or PR that closes an issue with `Closes #N` in the commit message or PR body, or close it by hand with `gh issue close N --comment "<what was done>"`.

## Commands
```bash
gh issue list --state open --limit 50 --json number,title,labels
gh issue view 12
cat > /tmp/body.md <<'BODY'      # use the scratchpad dir, not /tmp, in Claude sessions
**Why** ...
BODY
gh issue create --title "..." --body-file body.md --label enhancement --label library
gh issue edit 12 --add-label playback
gh issue comment 12 --body "..."
gh issue close 12 --reason completed --comment "..."
```

## Source of truth for "what is missing"
`docs/action-plan.md` (numbered plan), `docs/resume.md` (known UI gaps), `docs/download.md` ("Left out"), `docs/ui-implementation.md`, `docs/wifi-transfer.md`. When an issue is created, keep the plan item and the issue number side by side in `docs/action-plan.md` only if the user asks.
