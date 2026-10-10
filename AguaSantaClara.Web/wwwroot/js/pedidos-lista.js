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

    // ============ REGISTRAR DEUDA ============
    const registrarDeudaModal = $("registrarDeudaModal");
    const registrarDeudaPedido = $("registrarDeudaPedido");
    const registrarDeudaIdPedido = $("registrarDeudaIdPedido");
    const registrarDeudaIdCliente = $("registrarDeudaIdCliente");
    const registrarDeudaMonto = $("registrarDeudaMonto");
    const registrarDeudaFechaVencimiento = $("registrarDeudaFechaVencimiento");
    const registrarDeudaError = $("registrarDeudaError");
    const registrarDeudaGuardar = $("registrarDeudaGuardar");
    const montoEnCentavos = valor => {
        const coincidencia = String(valor).trim().match(/^(\d+)(?:[.,](\d{0,2}))?$/);
        if (!coincidencia)
            return null;

        return Number(coincidencia[1]) * 100
            + Number((coincidencia[2] ?? "").padEnd(2, "0"));
    };
    const actualizarFormularioDeuda = () => {
        const montoPendiente = montoEnCentavos(
            registrarDeudaIdCliente.selectedOptions[0]?.dataset.montoPendiente ?? "");
        const monto = montoEnCentavos(registrarDeudaMonto.value);
        registrarDeudaMonto.max = montoPendiente !== null
            ? (montoPendiente / 100).toFixed(2)
            : "";
        registrarDeudaGuardar.disabled = montoPendiente === null
            || monto === null
            || monto <= 0
            || monto !== montoPendiente
            || !registrarDeudaMonto.validity.valid
            || !registrarDeudaFechaVencimiento.value;
    };

    document.querySelectorAll(".pedido-registrar-deuda").forEach(btn => {
        btn.addEventListener("click", () => {
            const clientes = JSON.parse(btn.dataset.clientes);
            registrarDeudaIdPedido.value = btn.dataset.pedidoId;
            registrarDeudaPedido.textContent = `Pedido N° ${btn.dataset.pedidoId}`;
            registrarDeudaIdCliente.replaceChildren();
            registrarDeudaError.classList.add("d-none");
            registrarDeudaMonto.value = "";
            registrarDeudaFechaVencimiento.value = "";

            clientes.forEach(cliente => {
                const opcion = document.createElement("option");
                opcion.value = cliente.idCliente;
                opcion.textContent = cliente.nombre;
                opcion.dataset.montoPendiente = Number(cliente.montoPendiente).toFixed(2);
                registrarDeudaIdCliente.append(opcion);
            });

            registrarDeudaMonto.value = registrarDeudaIdCliente.selectedOptions[0]?.dataset.montoPendiente ?? "";
            actualizarFormularioDeuda();
            bootstrap.Modal.getOrCreateInstance(registrarDeudaModal).show();
        });
    });
    registrarDeudaIdCliente.addEventListener("change", () => {
        const montoPendiente = registrarDeudaIdCliente.selectedOptions[0]?.dataset.montoPendiente;
        registrarDeudaMonto.value = montoPendiente ?? "";
        actualizarFormularioDeuda();
    });
    ["input", "change"].forEach(evento => {
        registrarDeudaMonto.addEventListener(evento, actualizarFormularioDeuda);
        registrarDeudaFechaVencimiento.addEventListener(evento, actualizarFormularioDeuda);
    });
    registrarDeudaGuardar.addEventListener("click", async () => {
        if (registrarDeudaGuardar.disabled)
            return;

        registrarDeudaGuardar.disabled = true;
        registrarDeudaError.classList.add("d-none");
        try {
            const respuesta = await fetch(
                `/Pedidos/RegistrarDeuda?idPedido=${encodeURIComponent(registrarDeudaIdPedido.value)}`,
                {
                    method: "POST",
                    credentials: "same-origin",
                    headers: {
                        "Content-Type": "application/json",
                        "RequestVerificationToken": token
                    },
                    body: JSON.stringify({
                        idCliente: Number(registrarDeudaIdCliente.value),
                        monto: Number(registrarDeudaMonto.value),
                        fechaVencimiento: registrarDeudaFechaVencimiento.value
                    })
                });
            const datos = await respuesta.json();
            if (!respuesta.ok) {
                const mensaje = datos?.errores?.map(error => error.mensaje).filter(Boolean).join(" ")
                    || `No se pudo registrar la deuda (HTTP ${respuesta.status}).`;
                registrarDeudaError.textContent = mensaje;
                registrarDeudaError.classList.remove("d-none");
                registrarDeudaGuardar.disabled = false;
                return;
            }

            sessionStorage.setItem("pedidoToast", datos?.mensaje || "Deuda registrada correctamente.");
            bootstrap.Modal.getOrCreateInstance(registrarDeudaModal).hide();
            window.location.reload();
        } catch {
            registrarDeudaError.textContent = "Ocurrió un error al registrar la deuda. Inténtalo nuevamente.";
            registrarDeudaError.classList.remove("d-none");
            registrarDeudaGuardar.disabled = false;
        }
    });

    // ============ DETALLE DE COBRO ============
    const cobrarPagoModal = $("cobrarPagoModal");
    const cobrarPagoTitulo = $("cobrarPagoTitulo");
    const cobrarPagoClientes = $("cobrarPagoClientes");
    const cobrarPagoGuardar = $("cobrarPagoGuardar");
    const cobrarPagoSinMetodos = $("cobrarPagoSinMetodos");
    const formatoMoneda = new Intl.NumberFormat("es-PE", {
        style: "currency",
        currency: "PEN"
    });
    let pedidoCobroActual = null;

    const crearFilaPago = (cliente, monto, metodosPago) => {
        const fila = document.createElement("div");
        fila.className = "row g-2 align-items-end mb-2";
        fila.dataset.pagoFila = "";
        fila.dataset.pagoCliente = cliente.idCliente;

        const montoColumna = document.createElement("div");
        montoColumna.className = "col-sm-5";
        const montoLabel = document.createElement("label");
        montoLabel.className = "form-label small";
        montoLabel.textContent = "Monto a pagar";
        const montoInput = document.createElement("input");
        montoInput.className = "form-control";
        montoInput.type = "number";
        montoInput.min = "0.01";
        montoInput.max = monto.toFixed(2);
        montoInput.step = "0.01";
        montoInput.value = monto > 0 ? monto.toFixed(2) : "";
        montoInput.dataset.pagoMonto = "";
        montoInput.setAttribute("aria-label", `Monto de pago de ${cliente.nombre}`);
        montoColumna.append(montoLabel, montoInput);

        const metodoColumna = document.createElement("div");
        metodoColumna.className = "col-sm-7";
        const metodoLabel = document.createElement("label");
        metodoLabel.className = "form-label small";
        metodoLabel.textContent = "Método de pago";
        const metodoSelect = document.createElement("select");
        metodoSelect.className = "form-select";
        metodoSelect.dataset.pagoMetodo = "";
        metodoSelect.setAttribute("aria-label", `Método de pago de ${cliente.nombre}`);

        const opcionInicial = document.createElement("option");
        opcionInicial.value = "";
        opcionInicial.textContent = "Seleccionar método";
        metodoSelect.append(opcionInicial);
        metodosPago.forEach(metodo => {
            const opcion = document.createElement("option");
            opcion.value = metodo.id;
            opcion.textContent = metodo.nombre;
            metodoSelect.append(opcion);
        });

        metodoColumna.append(metodoLabel, metodoSelect);
        fila.append(montoColumna, metodoColumna);
        return fila;
    };

    document.querySelectorAll(".pedido-cobrar-pago").forEach(btn => {
        btn.addEventListener("click", () => {
            const clientes = JSON.parse(btn.dataset.clientes);
            const metodosPago = window.__pedidosData?.metodosPago ?? [];
            pedidoCobroActual = btn.dataset.pedidoId;
            cobrarPagoTitulo.textContent = `Cobrar pago — Pedido N° ${btn.dataset.pedidoId}`;
            cobrarPagoClientes.replaceChildren();
            cobrarPagoSinMetodos.classList.toggle("d-none", metodosPago.length > 0);
            let tieneSaldoPendiente = false;

            clientes.forEach(cliente => {
                const montoPendiente = Math.max(0, Number(cliente.monto) - Number(cliente.pagado));
                const tarjeta = document.createElement("div");
                tarjeta.className = "p-3 border rounded-3";

                const nombre = document.createElement("span");
                nombre.className = "fw-semibold";
                nombre.textContent = cliente.nombre;

                const saldo = document.createElement("div");
                saldo.className = "small text-secondary mt-1";
                saldo.textContent = `Pendiente: ${formatoMoneda.format(montoPendiente)} · Pagado: ${formatoMoneda.format(Number(cliente.pagado))}`;
                tarjeta.append(nombre, saldo);

                if (montoPendiente > 0 && metodosPago.length > 0) {
                    tieneSaldoPendiente = true;
                    const filasPago = document.createElement("div");
                    filasPago.className = "mt-2";
                    filasPago.append(crearFilaPago(cliente, montoPendiente, metodosPago));

                    const agregarMetodo = document.createElement("button");
                    agregarMetodo.type = "button";
                    agregarMetodo.className = "btn btn-sm btn-outline-secondary mt-1";
                    agregarMetodo.textContent = "Agregar otro método";
                    agregarMetodo.addEventListener("click", () => {
                        filasPago.append(crearFilaPago(cliente, 0, metodosPago));
                    });

                    tarjeta.append(filasPago, agregarMetodo);
                } else if (montoPendiente <= 0) {
                    const pagado = document.createElement("div");
                    pagado.className = "small text-success mt-2";
                    pagado.textContent = "Este cliente ya no tiene saldo pendiente.";
                    tarjeta.append(pagado);
                }

                cobrarPagoClientes.append(tarjeta);
            });

            cobrarPagoGuardar.disabled = metodosPago.length === 0 || !tieneSaldoPendiente;
            cobrarPagoGuardar.textContent = tieneSaldoPendiente
                ? "Guardar"
                : "Sin saldo pendiente";

            bootstrap.Modal.getOrCreateInstance(cobrarPagoModal).show();
        });
    });

    cobrarPagoGuardar.addEventListener("click", async () => {
        const filasPago = [...cobrarPagoClientes.querySelectorAll("[data-pago-fila]")];
        const filasConDatos = filasPago.filter(fila =>
            fila.querySelector("[data-pago-monto]").value.trim() !== "" ||
            fila.querySelector("[data-pago-metodo]").value !== ""
        );
        const pagos = filasConDatos.map(fila => ({
            idCliente: Number(fila.dataset.pagoCliente),
            idMetodoPago: Number(fila.querySelector("[data-pago-metodo]").value),
            monto: Number(fila.querySelector("[data-pago-monto]").value)
        }));

        if (!pedidoCobroActual || pagos.length === 0 || filasConDatos.some(fila =>
            fila.querySelector("[data-pago-monto]").value.trim() === "" ||
            fila.querySelector("[data-pago-metodo]").value === ""
        ) || pagos.some(pago =>
            !Number.isSafeInteger(pago.idCliente) ||
            !Number.isSafeInteger(pago.idMetodoPago) ||
            !Number.isFinite(pago.monto) ||
            pago.monto <= 0
        )) {
            mostrarToast("Ingresa un monto válido y selecciona el método de pago para cada cliente.", "error");
            return;
        }

        cobrarPagoGuardar.disabled = true;
        try {
            const respuesta = await fetch(`/Pedidos/RegistrarPagos?idPedido=${encodeURIComponent(pedidoCobroActual)}`, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": token
                },
                body: JSON.stringify({ pagos })
            });
            const datos = await respuesta.json().catch(() => null);

            if (!respuesta.ok) {
                const mensajeError = datos?.errores?.[0]?.mensaje
                    || datos?.mensaje
                    || datos?.Mensaje
                    || `No se pudieron registrar los pagos (HTTP ${respuesta.status}).`;
                mostrarToast(mensajeError, "error");
                cobrarPagoGuardar.disabled = false;
                return;
            }

            sessionStorage.setItem("pedidoToast", datos?.mensaje || "Pagos registrados correctamente.");
            bootstrap.Modal.getOrCreateInstance(cobrarPagoModal).hide();
            window.location.reload();
        } catch {
            mostrarToast("No se pudo conectar con el servidor.", "error");
            cobrarPagoGuardar.disabled = false;
        }
    });

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
    const incidenciaMotivo = document.getElementById("incidenciaMotivo");
    const incidenciaOtrosContainer = document.getElementById("incidenciaOtrosContainer");
    const incidenciaOtros = document.getElementById("incidenciaOtros");
    const incidenciaValidarBtn = document.getElementById("incidenciaValidarBtn");

    document.querySelectorAll(".pedido-incidencia").forEach(btn => {
        btn.addEventListener("click", () => {
            const modalEl = document.getElementById("incidenciaModal");
            const pedidoIdInput = document.getElementById("incidenciaPedidoId");

            pedidoIdInput.value = btn.dataset.pedidoId;

            incidenciaMotivo.value = "";
            incidenciaOtros.value = "";
            incidenciaOtrosContainer.classList.add("d-none");

            incidenciaMotivo.classList.remove("is-invalid");
            incidenciaOtros.classList.remove("is-invalid");

            bootstrap.Modal.getOrCreateInstance(modalEl).show();
        });
    });

    incidenciaMotivo?.addEventListener("change", () => {
        const esOtros = incidenciaMotivo.value === "Otros";

        incidenciaOtrosContainer.classList.toggle("d-none", !esOtros);

        incidenciaMotivo.classList.remove("is-invalid");
        incidenciaOtros.classList.remove("is-invalid");

        if (!esOtros) {
            incidenciaOtros.value = "";
        }
    });

    incidenciaValidarBtn?.addEventListener("click", async () => {
        let valido = true;

        incidenciaMotivo.classList.remove("is-invalid");
        incidenciaOtros.classList.remove("is-invalid");

        if (!incidenciaMotivo.value) {
            incidenciaMotivo.classList.add("is-invalid");
            valido = false;
        }

        if (
            incidenciaMotivo.value === "Otros" &&
            !incidenciaOtros.value.trim()
        ) {
            incidenciaOtros.classList.add("is-invalid");
            valido = false;
        }

        if (!valido) return;

        incidenciaValidarBtn.disabled = true;

        const pedidoId = $("incidenciaPedidoId").value;

        const cuerpo = {
            motivo: incidenciaMotivo.value,
            detalle: incidenciaMotivo.value === "Otros"
                ? incidenciaOtros.value.trim()
                : null
        };

        try {
            const resp = await fetch(
                `/Pedidos/RegistrarIncidencia?idPedido=${encodeURIComponent(pedidoId)}`,
                {
                    method: "POST",
                    credentials: "same-origin",
                    headers: {
                        "Content-Type": "application/json",
                        "RequestVerificationToken": token
                    },
                    body: JSON.stringify(cuerpo)
                }
            );

            const datos = await resp.json().catch(() => null);

            if (!resp.ok) {
                mostrarToast(
                    datos?.errores?.[0]?.mensaje ||
                    "No se pudo registrar la incidencia.",
                    "error"
                );

                incidenciaValidarBtn.disabled = false;
                return;
            }

            sessionStorage.setItem(
                "pedidoToast",
                datos?.mensaje || "INCIDENCIA REGISTRADA CORRECTAMENTE"
            );

            bootstrap.Modal
                .getOrCreateInstance($("incidenciaModal"))
                .hide();

            window.location.reload();

        } catch {
            mostrarToast(
                "No se pudo conectar con el servidor.",
                "error"
            );

            incidenciaValidarBtn.disabled = false;
        }
    });

    // ============ REINTENTAR ENTREGA ============
    const reintentarPedidoId = $("reintentarPedidoId");
    const reintentarRepartidor = $("reintentarRepartidor");
    const reintentarContinuarBtn = $("reintentarContinuarBtn");

    document.querySelectorAll(".pedido-reintentar").forEach(btn => {
        btn.addEventListener("click", () => {
            reintentarPedidoId.value = btn.dataset.pedidoId;
            reintentarRepartidor.value = "";
            reintentarRepartidor.classList.remove("is-invalid");

            bootstrap.Modal
                .getOrCreateInstance($("reintentarEntregaModal"))
                .show();
        });
    });

    reintentarContinuarBtn?.addEventListener("click", async () => {
        reintentarRepartidor.classList.remove("is-invalid");

        if (!reintentarRepartidor.value) {
            reintentarRepartidor.classList.add("is-invalid");
            return;
        }

        const pedidoId = reintentarPedidoId.value;

        const cuerpo = {
            idRepartidor: Number(reintentarRepartidor.value)
        };

        reintentarContinuarBtn.disabled = true;

        try {
            const resp = await fetch(
                `/Pedidos/PreviewReintentarEntrega?idPedido=${encodeURIComponent(pedidoId)}`,
                {
                    method: "POST",
                    credentials: "same-origin",
                    headers: {
                        "Content-Type": "application/json",
                        "RequestVerificationToken": token
                    },
                    body: JSON.stringify(cuerpo)
                }
            );

            const datos = await resp.json().catch(() => null);

            if (!resp.ok) {
                mostrarToast(
                    datos?.errores?.[0]?.mensaje ||
                    "No se pudo preparar el reenvío.",
                    "error"
                );

                reintentarContinuarBtn.disabled = false;
                return;
            }

            const previewModalEl = $("previewEnvioModal");

            $("previewEnvioMensaje").textContent =
                datos.mensaje ?? "";

            $("previewEnvioDestinatario").textContent =
                reintentarRepartidor.options[
                    reintentarRepartidor.selectedIndex
                ].text.trim();

            bootstrap.Modal
                .getOrCreateInstance($("reintentarEntregaModal"))
                .hide();

            const confirmBtn = $("previewEnvioConfirmar");
            const nuevoBtn = confirmBtn.cloneNode(true);

            confirmBtn.parentNode.replaceChild(
                nuevoBtn,
                confirmBtn
            );

            nuevoBtn.addEventListener("click", async () => {
                nuevoBtn.disabled = true;

                try {
                    const resp2 = await fetch(
                        `/Pedidos/ReintentarEntrega?idPedido=${encodeURIComponent(pedidoId)}`,
                        {
                            method: "POST",
                            credentials: "same-origin",
                            headers: {
                                "Content-Type": "application/json",
                                "RequestVerificationToken": token
                            },
                            body: JSON.stringify(cuerpo)
                        }
                    );

                    const datos2 =
                        await resp2.json().catch(() => null);

                    if (!resp2.ok) {
                        mostrarToast(
                            datos2?.errores?.[0]?.mensaje ||
                            "No se pudo reenviar el pedido.",
                            "error"
                        );

                        nuevoBtn.disabled = false;
                        return;
                    }

                    sessionStorage.setItem(
                        "pedidoToast",
                        datos2?.mensaje ||
                        "PEDIDO REENVIADO CORRECTAMENTE"
                    );

                    bootstrap.Modal
                        .getOrCreateInstance(previewModalEl)
                        .hide();

                    if (datos2?.whatsappUrl) {
                        window.open(
                            datos2.whatsappUrl,
                            "_blank"
                        );
                    }

                    window.location.reload();

                } catch {
                    mostrarToast(
                        "No se pudo conectar con el servidor.",
                        "error"
                    );

                    nuevoBtn.disabled = false;
                }
            });

            bootstrap.Modal
                .getOrCreateInstance(previewModalEl)
                .show();

            reintentarContinuarBtn.disabled = false;

        } catch {
            mostrarToast(
                "No se pudo conectar con el servidor.",
                "error"
            );

            reintentarContinuarBtn.disabled = false;
        }
    });

    // ============ CANCELAR PEDIDO CON INCIDENCIA ============
    document.querySelectorAll(".pedido-cancelar").forEach(btn => {
        btn.addEventListener("click", () => {
            window.abrirModalConfirm(
                "Cancelar pedido",
                "¿Seguro que deseas cancelar este pedido?",
                "danger",
                async () => {
                    try {
                        const resp = await fetch(
                            `/Pedidos/CancelarConIncidencia?idPedido=${encodeURIComponent(btn.dataset.pedidoId)}`,
                            {
                                method: "POST",
                                credentials: "same-origin",
                                headers: {
                                    "RequestVerificationToken": token
                                }
                            }
                        );

                        const datos =
                            await resp.json().catch(() => null);

                        if (!resp.ok) {
                            mostrarToast(
                                datos?.errores?.[0]?.mensaje ||
                                "No se pudo cancelar el pedido.",
                                "error"
                            );
                            return;
                        }

                        sessionStorage.setItem(
                            "pedidoToast",
                            datos?.mensaje ||
                            "PEDIDO CANCELADO CORRECTAMENTE"
                        );

                        window.location.reload();

                    } catch {
                        mostrarToast(
                            "No se pudo conectar con el servidor.",
                            "error"
                        );
                    }
                },
                "Cancelar pedido"
            );
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