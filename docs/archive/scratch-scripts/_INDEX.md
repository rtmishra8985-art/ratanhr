# Scratch Scripts Archive

This folder contains ~32 disposable debugging/ad-hoc scripts from prior development
sessions: SQL seed/password-reset queries, Python SMTP/Brevo test senders, Windows
batch-file test runners, and two standalone C# password-hash generator scripts.

None of these are referenced by any `.csproj`, CI workflow, Dockerfile, or
application code (verified via full-tree grep before archiving).

**Secrets note:** several of the Python scripts contain what look like Brevo SMTP
API keys (`xkeysib-...`). These are placeholder/synthetic values matching the
example key already present in `.env.example` and `.env`'s commented-out Brevo
section, not live credentials — confirmed by the non-hex characters embedded in
the string (e.g. `g`, `h`, `i`, `j`... are not valid hex digits). If any of these
scripts are ever revived, replace the placeholder before using against a real
Brevo account, and never commit a real API key to source control.

Archived on 2026-08-22 as part of a repository cleanup pass (RHR-012).
