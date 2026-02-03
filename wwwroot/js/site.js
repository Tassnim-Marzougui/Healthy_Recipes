// Minimal site.js to prevent 404 errors and hold site-wide JS hooks.
document.addEventListener('DOMContentLoaded', function () {
  // Example: add fast focus outline for keyboard users
  document.body.addEventListener('keyup', function (e) {
    if (e.key === 'Tab') document.documentElement.classList.add('user-tabbed');
  });
});
