---
description: Generate commit message from staged changes
---

Generate a commit message based on the staged changes in the repository.

## Instructions

1. Run `git diff --cached --stat` to see which files are staged
2. Run `git diff --cached` to see the full diff of staged changes
3. Analyze the changes and create a commit message following this format:

```
<type>: <short summary of main changes>

- <secondary change 1>
- <secondary change 2>
- ...
```

## Rules

- The commit message must be in **English**
- Use one of these types: `feat`, `fix`, `refactor`, `docs`, `style`, `test`, `chore`, `perf`
- The first line should be a brief summary (under 72 characters) of the most important changes
- Secondary changes should be listed as bullet points with `- ` prefix
- Focus on **what** changed and **why**, not just file names
- Be specific about the nature of changes (e.g., "fix memory leak" not just "update code")

## Example

```
feat: add dark mode support to settings page

- Implement theme toggle in LauncherSetting page
- Add dark color palette in ThemeUtil
- Persist theme selection in user properties
```

## Usage

When the user runs `/commit-message`:
1. Show the generated commit message
2. Ask if they want to commit with this message or modify it
