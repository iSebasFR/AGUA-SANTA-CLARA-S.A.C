(() => {
    const widget = document.querySelector(".notification-widget");
    if (!widget) return;

    const toggle = widget.querySelector("[data-bs-toggle='dropdown']");
    const list = widget.querySelector(".notification-list");
    const count = widget.querySelector(".notification-count");
    const endpoint = widget.dataset.api;

    const render = notifications => {
        count.textContent = notifications.length > 99 ? "99+" : String(notifications.length);
        count.classList.toggle("d-none", notifications.length === 0);
        list.replaceChildren();

        if (notifications.length === 0) {
            const empty = document.createElement("p");
            empty.className = "small text-secondary px-3 py-3 mb-0";
            empty.textContent = "No hay notificaciones pendientes.";
            list.append(empty);
            return;
        }

        notifications.forEach(notification => {
            const item = document.createElement("div");
            item.className = "notification-item border-bottom px-3 py-2";

            const type = document.createElement("div");
            type.className = "fw-semibold small";
            type.textContent = notification.tipo;
            const entity = document.createElement("div");
            entity.className = "small";
            entity.textContent = notification.entidadAfectada;
            const message = document.createElement("div");
            message.className = "small text-secondary";
            message.textContent = notification.mensaje;
            const details = document.createElement("div");
            details.className = "small text-secondary";

            const detected = new Date(notification.fechaDeteccion);
            details.textContent = `Prioridad ${notification.prioridad} · Detectada ${detected.toLocaleString("es-PE")}`;
            if (notification.stockActual !== null && notification.stockActual !== undefined) {
                details.textContent += ` · Stock ${notification.stockActual}/${notification.stockMinimo}`;
            }
            if (notification.fechaVencimiento) {
                details.textContent += ` · Vence ${new Date(notification.fechaVencimiento).toLocaleDateString("es-PE")}`;
            }

            item.append(type, entity, message, details);
            list.append(item);
        });
    };

    const load = async () => {
        try {
            const response = await fetch(endpoint, {
                credentials: "same-origin",
                headers: { Accept: "application/json" }
            });
            if (!response.ok)
                throw new Error(`No se pudieron cargar las notificaciones (HTTP ${response.status}).`);

            render(await response.json());
        } catch (error) {
            console.error("Error al cargar notificaciones:", error);
            count.classList.add("d-none");
            list.replaceChildren();
            const failure = document.createElement("p");
            failure.className = "small text-danger px-3 py-3 mb-0";
            failure.textContent = "No se pudieron cargar las notificaciones.";
            list.append(failure);
        }
    };

    toggle.addEventListener("show.bs.dropdown", load);
    load();
})();
