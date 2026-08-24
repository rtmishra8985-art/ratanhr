# Legacy UI disposition

Recorded: 2026-08-10

## Approved decision

- **RETAIN temporarily until ported to the SPA**
  - Super-admin console:
    - `superadmin-login.html`
    - `superadmin-dashboard.html`
    - `superadmin-companies.html`
    - `superadmin-manage-admins.html`
    - `superadmin-superadmins.html`
    - `superadmin-permissions.html`
  - Company management:
    - `add-company.html`
    - `edit-company.html`
    - `view-company.html`
    - `company-docs.html`
    - `upload-logo.html`

- **DEPRECATE the remaining archived legacy pages**
  - Deprecation means the legacy HTML pages are not the target for new work; their supported capabilities should be ported to the SPA before the legacy archive is removed.

## Close-out boundary

This decision records product direction only. No legacy page was deleted, restored to the served web root, disabled, redirected, or otherwise changed during the audit.

The retained groups remain an implementation gap because their archived pages are not currently served. The broken SPA sidebar link to `webhooks.html` remains a separate porting item under the deprecated legacy group.