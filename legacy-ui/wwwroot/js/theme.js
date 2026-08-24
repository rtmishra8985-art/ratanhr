/* ── RatanHR Theme Toggle ──────────────────────────────────────────────── */
(function () {
    // Apply saved theme immediately to prevent flash of wrong theme
    var saved = localStorage.getItem('hrms_theme') || 'light';
    document.documentElement.setAttribute('data-theme', saved);
})();

function toggleTheme() {
    var current = document.documentElement.getAttribute('data-theme') || 'light';
    var next = current === 'light' ? 'dark' : 'light';
    document.documentElement.setAttribute('data-theme', next);
    localStorage.setItem('hrms_theme', next);
    _updateThemeBtn();
}

function _updateThemeBtn() {
    var btn = document.getElementById('theme-toggle-btn');
    if (!btn) return;
    var isDark = document.documentElement.getAttribute('data-theme') === 'dark';
    btn.textContent = isDark ? '☀️' : '🌙';
    btn.title = isDark ? 'Switch to Light Mode' : 'Switch to Dark Mode';
}

document.addEventListener('DOMContentLoaded', function () {
    _updateThemeBtn();
});
