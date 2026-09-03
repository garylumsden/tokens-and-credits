"use strict";

(() => {
    const savedTheme = localStorage.getItem("tokens-and-credits-theme");
    const systemTheme = window.matchMedia("(prefers-color-scheme: light)").matches ? "light" : "dark";
    const theme = savedTheme === "light" || savedTheme === "dark" ? savedTheme : systemTheme;
    document.documentElement.dataset.theme = theme;
    document.documentElement.style.colorScheme = theme;
})();
