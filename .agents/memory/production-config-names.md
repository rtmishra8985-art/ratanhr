---
name: Production configuration names
description: Durable rule for keeping deployment environment aliases aligned with application configuration consumers.
---

The application must normalize legacy all-caps deployment environment names into
the hierarchical configuration keys consumed by runtime services, while keeping
secret values out of logs and source. Validation should check the keys the
services actually read and may accept explicit legacy aliases for compatibility.

**Why:** A deployment can provide a secret successfully while the application
still fails at startup if validation and consumers use different configuration
names.

**How to apply:** When adding or renaming a secret/configuration value, update
the runtime consumer, startup validation, deployment templates, and regression
tests together. Never print the resolved value.