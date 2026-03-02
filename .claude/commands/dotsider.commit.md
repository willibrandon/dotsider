---
description: Create a git commit for staged changes with a clear, natural commit message
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Outline

You are creating a git commit for staged changes in the repository. Your job is to write a clear, natural commit message that describes the changes without sounding artificial or using unnecessary embellishments.

Follow this execution flow:

1. Check git status to see what files are staged:
   ```bash
   git status
   ```

2. Review the staged changes to understand what was modified:
   ```bash
   git diff --cached
   ```

3. Analyze the changes and categorize them by type:
   - feat: New feature or functionality
   - fix: Bug fix
   - docs: Documentation changes
   - refactor: Code restructuring without behavior change
   - test: Test additions or modifications
   - chore: Maintenance tasks, dependency updates
   - perf: Performance improvements

4. Draft a commit message following these strict rules:
   - Use conventional commit format: `type(scope): message`
   - Keep the subject line under 72 characters
   - Use imperative mood ("add feature" not "added feature")
   - Be specific and direct about what changed
   - Focus on WHAT changed and WHY, not HOW
   - Write naturally - avoid formal or academic language
   - **NEVER use these words**: comprehensive, robust, clarity, enhance, streamline, leverage, utilize, facilitate, optimal, scalable, maintainable
   - **NEVER use emojis** in commit messages
   - **NEVER add "Co-Authored-By: Claude"** or any AI attribution
   - **NEVER add "Generated with Claude Code"** or similar footers
   - Be concise - remove filler words and redundancy

5. If user provided specific instructions in $ARGUMENTS, incorporate them into the commit message while following all rules above.

6. Create the commit using:
   ```bash
   git commit -m "type(scope): message"
   ```

7. Confirm the commit was created successfully:
   ```bash
   git log -1 --oneline
   ```

## Examples of Good vs Bad Commit Messages

**GOOD:**
- `feat(api): add user authentication endpoint`
- `fix(parser): handle null values in JSON response`
- `docs(readme): update installation steps for Windows`
- `refactor(db): extract query logic into separate module`

**BAD (avoid these patterns):**
- `feat: implement comprehensive user authentication system` ❌ (uses "comprehensive")
- `fix: enhance error handling for improved clarity` ❌ (uses "enhance" and "clarity")
- `docs: update documentation to be more robust` ❌ (uses "robust")
- `refactor: streamline codebase architecture` ❌ (uses "streamline")
- `feat: add feature 🎉` ❌ (uses emoji)

## Commit Message Body (Optional)

If changes require additional context, add a body after a blank line:
- Explain WHY the change was necessary
- Reference issue numbers if applicable
- Note breaking changes with "BREAKING CHANGE:" prefix
- Keep lines under 72 characters

Format:
```
type(scope): subject line

Additional context about the change. Explain the reasoning
behind the implementation decision.

Fixes #123
```

## Validation Before Committing

Before executing `git commit`, verify:
- [ ] No AI-sounding words in message
- [ ] No emojis
- [ ] No Claude attribution or co-author lines
- [ ] Message is clear and specific
- [ ] Uses conventional commit format
- [ ] Subject line under 72 characters
- [ ] Uses imperative mood

If any validation fails, revise the message and revalidate.

## Output to User

After creating the commit, provide:
- The commit hash and message
- Files that were committed
- Next suggested action (e.g., push changes, create PR, continue development)

Do NOT output lengthy summaries or praise. Keep it brief and actionable.
