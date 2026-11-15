(() => {
    const sidebar = document.getElementById("sidebar");
    const toggle = document.getElementById("menuToggle");
    const scrim = document.getElementById("sidebarScrim");

    const setOpen = (open) => {
        sidebar?.classList.toggle("is-open", open);
        scrim?.classList.toggle("is-visible", open);
        toggle?.setAttribute("aria-expanded", String(open));
    };

    toggle?.addEventListener("click", () => setOpen(!sidebar?.classList.contains("is-open")));
    scrim?.addEventListener("click", () => setOpen(false));
    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") setOpen(false);
    });
})();
