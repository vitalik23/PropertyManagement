// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener('DOMContentLoaded', function () {
    var toggle = document.getElementById('user-menu-toggle');
    if (!toggle) {
        return;
    }

    var dropdownEl = toggle.closest('.dropdown');
    var dropdown = bootstrap.Dropdown.getOrCreateInstance(toggle);
    var hideTimer;

    dropdownEl.addEventListener('mouseenter', function () {
        clearTimeout(hideTimer);
        dropdown.show();
    });

    dropdownEl.addEventListener('mouseleave', function () {
        hideTimer = setTimeout(function () {
            dropdown.hide();
        }, 150);
    });
});
