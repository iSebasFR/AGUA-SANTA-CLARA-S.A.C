(() => {
    const modalEl = document.getElementById("autoventaModal");
    if (!modalEl) return;

    // ============ ABRIR MODAL ============
    document.getElementById("btnAutoventa")?.addEventListener("click", () => {
        bootstrap.Modal.getOrCreateInstance(modalEl).show();
    });
})();