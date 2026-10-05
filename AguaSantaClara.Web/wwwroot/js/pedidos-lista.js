(() => {
    const panel = document.getElementById("pedidosAcciones");
    if (!panel) return;

    // ✅ FIX 1: leer toast pendiente de sessionStorage (sin DOMContentLoaded)
    const msgPendiente = sessionStorage.getItem("pedidoToast");
    if (msgPendiente) {
        sessionStorage.removeItem("pedidoToast");
        setTimeout(() => {
            if (typeof window.mostrarToastPedidos === "function") {
                window.mostrarToastPedidos(msgPendiente, "success");
            }
        }, 300);
    }

    const $ = id => document.getElementById(id);
    const checks = () => [...document.querySelectorAll(".pedido-check:not(:disabled)")];
    const marcados = () => checks().filter(c => c.checked);
    const todos = $("pedidosSeleccionarTodos");
    const btnEnviar = $("pedidosEnviarBtn");
    const btnToggle = $("pedidosEnviarToggle");
    const menuRepartidores = $("repartidoresMenu");
    const token = panel.querySelector("input[name='__RequestVerificationToken']")?.value ?? "";

    let repartidorSeleccionado = null;

    const actualizar = () => {
        const n = marcados().length;
        $("pedidosSeleccionados").textContent = n;
        btnEnviar.disabled = n === 0 || !repartidorSeleccionado;
        btnToggle.disabled = n === 0;
        todos.checked = n > 0 && n === checks().length;
    };

    // ============ SPLIT BUTTON ============
    btnToggle.addEventListener("click", (e) => {
        e.stopPropagation();
        menuRepartidores.classList.toggle("show");
    });

    document.addEventListener("click", (e) => {
        if (!e.target.closest("#splitBtnWrapper")) {
            menuRepartidores.classList.remove("show");
        }
    });

    menuRepartidores.querySelectorAll(".split-btn-item").forEach(item => {
        item.addEventListener("click", () => {
            repartidorSeleccionado = {
                id: Number(item.dataset.repartidorId),
                nombre: item.dataset.repartidorNombre
            };
            btnEnviar.innerHTML = `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 2L11 13M22 2l-7 20-4-9-9-4 20-7z"/></svg> Enviar a ${repartidorSeleccionado.nombre}`;
            menuRepartidores.classList.remove("show");
            actualizar();
        });
    });

    // ============ SELECCIONAR TODOS ============
    todos.addEventListener("change", () => {
        checks().forEach(c => c.checked = todos.checked);
        actualizar();
    });

    document.addEventListener("change", e => {
        if (e.target.classList?.contains("pedido-check")) actualizar();
    });

    // ============ CAMBIAR ESTADO ============
    document.querySelectorAll(".pedido-estado").forEach(select => {
        select.addEventListener("change", async () => {
            const estadoAnterior = select.dataset.estadoOriginal;
            select.disabled = true;

            try {
                const resp = await fetch(`/Pedidos/CambiarEstado?idPedido=${encodeURIComponent(select.dataset.pedidoId)}`, {
                    method: "POST",
                    credentials: "same-origin",
                    headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
                    body: JSON.stringify({ estado: select.value })
                });
                const datos = await resp.json().catch(() => null);

                if (!resp.ok) {
                    mostrarToast((datos?.errores?.[0]?.mensaje) || "No se pudo actualizar el estado.", "error");
                    select.value = estadoAnterior;
                    select.disabled = false;
                    return;
                }

                mostrarToast(`Pedido: estado actualizado a ${select.value}.`, "success");

                // ✅ Recargar para reflejar el cambio visual (badge vs dropdown)
                setTimeout(() => window.location.reload(), 700);
            } catch {
                mostrarToast("No se pudo conectar con el servidor.", "error");
                select.value = estadoAnterior;
                select.disabled = false;
            }
        });
    });

    // ============ REGISTRAR INCIDENCIA ============
    document.querySelectorAll(".pedido-incidencia").forEach(btn => {
        btn.addEventListener("click", () => {
            const modalEl = document.getElementById("incidenciaModal");
            const pedidoIdInput = document.getElementById("incidenciaPedidoId");
            const motivoSelect = document.getElementById("incidenciaMotivo");

            pedidoIdInput.value = btn.dataset.pedidoId;
            motivoSelect.value = "";

            bootstrap.Modal.getOrCreateInstance(modalEl).show();
        });
    });

    // ============ ELIMINAR ============
    document.querySelectorAll(".pedido-eliminar").forEach(btn => {
        btn.addEventListener("click", () => {
            window.abrirModalConfirm(
                "Eliminar pedido",
                `¿Seguro que deseas eliminar este pedido?`,
                "danger",
                async () => {
                    try {
                        const resp = await fetch(`/Pedidos/Eliminar?idPedido=${encodeURIComponent(btn.dataset.pedidoId)}`, {
                            method: "POST",
                            credentials: "same-origin",
                            headers: { "RequestVerificationToken": token }
                        });
                        const datos = await resp.json().catch(() => null);
                        if (!resp.ok) {
                            mostrarToast("No se pudo eliminar el pedido.", "error");
                            return;
                        }

                        // ✅ FIX 2: guardar mensaje para mostrarlo tras recargar
                        if (datos?.mensaje) {
                            sessionStorage.setItem("pedidoToast", datos.mensaje);
                        }

                        // ✅ FIX 3: recargar la página completa (no remover solo la fila)
                        window.location.reload();
                    } catch {
                        mostrarToast("No se pudo conectar con el servidor.", "error");
                    }
                },
                "Eliminar"
            );
        });
    });

    // ============ ENVIAR SELECCIONADOS ============
    btnEnviar.addEventListener("click", async () => {
        const seleccionados = marcados();
        if (!seleccionados.length || !repartidorSeleccionado) {
            mostrarToast("Selecciona al menos un pedido y un repartidor.", "error");
            return;
        }

        btnEnviar.disabled = true;
        const cuerpo = {
            idsPedidos: seleccionados.map(c => Number(c.value)),
            idRepartidor: repartidorSeleccionado.id
        };

        try {
            const resp = await fetch("/Pedidos/PreviewEnviarSeleccionados", {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
                body: JSON.stringify(cuerpo)
            });
            const datos = await resp.json().catch(() => null);

            if (!resp.ok) {
                mostrarToast((datos?.errores?.[0]?.mensaje) || "No se pudo validar los pedidos.", "error");
                btnEnviar.disabled = false;
                return;
            }

            const previewModalEl = document.getElementById("previewEnvioModal");
            document.getElementById("previewEnvioMensaje").textContent = datos.mensaje ?? "";
            document.getElementById("previewEnvioDestinatario").textContent = repartidorSeleccionado.nombre;

            const confirmBtn = document.getElementById("previewEnvioConfirmar");
            const nuevoBtn = confirmBtn.cloneNode(true);
            confirmBtn.parentNode.replaceChild(nuevoBtn, confirmBtn);

            nuevoBtn.addEventListener("click", async () => {
                nuevoBtn.disabled = true;
                try {
                    const resp2 = await fetch("/Pedidos/EnviarSeleccionados", {
                        method: "POST",
                        credentials: "same-origin",
                        headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
                        body: JSON.stringify(cuerpo)
                    });
                    const datos2 = await resp2.json().catch(() => null);
                    if (!resp2.ok) {
                        mostrarToast((datos2?.errores?.[0]?.mensaje) || "No se pudo enviar.", "error");
                        nuevoBtn.disabled = false;
                        return;
                    }

                    // ✅ FIX: guardar mensaje para mostrarlo tras recargar
                    sessionStorage.setItem("pedidoToast", "PEDIDOS ENVIADOS CORRECTAMENTE");

                    bootstrap.Modal.getOrCreateInstance(previewModalEl).hide();
                    window.open(datos2.whatsappUrl, "_blank");
                    setTimeout(() => window.location.reload(), 800);
                } catch {
                    mostrarToast("No se pudo conectar con el servidor.", "error");
                    nuevoBtn.disabled = false;
                }
            });

            bootstrap.Modal.getOrCreateInstance(previewModalEl).show();
            btnEnviar.disabled = false;
        } catch {
            mostrarToast("No se pudo conectar con el servidor.", "error");
            btnEnviar.disabled = false;
        }
    });

    // ============ TOAST ============
    function mostrarToast(mensaje, tipo) {
        const container = document.getElementById("toastContainer");
        if (!container) return;
        const toast = document.createElement("div");
        toast.className = "toast-custom " + (tipo || "success");
        const icono = tipo === "error" ? "!" : "✓";
        const titulo = tipo === "error" ? "Error" : "Listo";
        toast.innerHTML = `
            <div class="toast-icon">${icono}</div>
            <div class="toast-content">
                <div class="toast-title">${titulo}</div>
                <div class="toast-msg">${mensaje}</div>
            </div>
            <div class="toast-progress"></div>
        `;
        container.appendChild(toast);
        setTimeout(() => {
            toast.style.opacity = "0";
            toast.style.transform = "translateX(24px) scale(0.96)";
            toast.style.transition = "all 0.3s cubic-bezier(0.4, 0, 0.2, 1)";
            setTimeout(() => toast.remove(), 300);
        }, 3000);
    }

    window.mostrarToastPedidos = mostrarToast;

    actualizar();
})();