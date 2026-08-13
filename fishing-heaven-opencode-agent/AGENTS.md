# Fishing Heaven OpenCode Agent

## Purpose

This repository provides a guarded OpenCode workflow for deploying an already-reviewed C# file into a Tuanjie Engine project.

## C# Source Contract

The single approved input file is:

```text
CSharp_Upload/FH_simple.cs
```

The usual deployed game script is:

```text
Assets/Scripts/FishingHeavenDemo.cs
```

Do not edit either file through the coding agent.

When C# needs changes, obtain a newly reviewed complete `FH_simple.cs` and overwrite the upload file.

## Agent Behavior

- Never auto-fix gameplay C#.
- Never refactor gameplay C#.
- Never remove version comments.
- Never run `opencode web`.
- Never create `.fishdev`.
- Never start a Node web dashboard.
- Prefer read-only inspection.
- Use the supplied deployment script for byte-for-byte copying.
- Require matching SHA256 after deployment.
- Report compile errors instead of fixing them.
