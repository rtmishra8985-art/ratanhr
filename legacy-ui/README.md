# Legacy static UI (archived — NOT served)

These pre-SPA HTML/JS/CSS pages were previously committed under
`HRMS.API/wwwroot/` and therefore served by `app.UseStaticFiles()`.

Many of them assigned `element.innerHTML = <API data>`, which is a stored-XSS
sink (audit finding HIGH-1). They have been moved out of the web root so the
API no longer serves them. The React SPA in `HRMS.SPA.Source/` is the only
supported frontend; the backend Dockerfile builds it into `wwwroot`.

Do not copy these files back into `HRMS.API/wwwroot/`. If a page here is still
needed, port it to the SPA using React rendering (no `innerHTML`).
