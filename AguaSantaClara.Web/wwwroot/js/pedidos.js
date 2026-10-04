(() => {
    const modalEl = document.getElementById("pedidoModal");
    if (!modalEl) return;

    // ✅ FIX 1: DOMContentLoaded ya se disparó cuando se carga este script,
    // así que leemos directo sin esperar el evento.
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
    const inpBuscar = $("pedidoBuscar");
    const msgBuscar = $("pedidoBuscarMensaje");
    const buscadorCliente = $("buscadorCliente");
    const clienteSeleccionado = $("clienteSeleccionado");
    const clienteNombre = $("clienteNombre");
    const clienteMeta = $("clienteMeta");
    const btnCambiarCliente = $("btnCambiarCliente");
    const contenedor = $("pedidoDirecciones");
    const wrapperAgregarDireccion = $("wrapperAgregarDireccion");
    const btnAgregarDireccion = $("btnAgregarDireccion");
    const contenedorVacio = $("pedidoVacio");
    const lblTotal = $("pedidoTotal");
    const alertaErrores = $("pedidoErrores");
    const alertaEnviado = $("pedidoEnviado");
    const formulario = $("pedidoFormulario");
    const btnGuardar = $("pedidoGuardarBtn");
    const btnEnviar = $("pedidoEnviarBtn");
    const dropdown = $("autocompleteDropdown");
    const token = modalEl.querySelector("input[name='__RequestVerificationToken']")?.value ?? "";

    const localesDisponibles = window.__pedidosData?.locales ?? [];
    const repartidoresDisponibles = window.__pedidosData?.repartidores ?? [];

    let clienteActual = null;
    let bloques = [];
    let pedidoRegistrado = false;
    let pedidoAEditar = null;
    let productosPorLocal = {};

    const moneda = v => "S/ " + Number(v || 0).toFixed(2);

    // ============ UTILIDADES DOM ============
    const h = (tag, attrs = {}, ...hijos) => {
        const nodo = document.createElement(tag);
        for (const [k, v] of Object.entries(attrs)) {
            if (k === "class") nodo.className = v;
            else if (k === "style") nodo.style.cssText = v;
            else if (k.startsWith("on")) nodo.addEventListener(k.slice(2), v);
            else if (v === true) nodo.setAttribute(k, "");
            else if (v !== false && v != null) nodo.setAttribute(k, v);
        }
        for (const hijo of hijos) {
            if (hijo == null) continue;
            if (typeof hijo === "string") nodo.append(document.createTextNode(hijo));
            else nodo.append(hijo);
        }
        return nodo;
    };

    const textoDireccion = d => d.ciudad ? `${d.direccion}, ${d.ciudad}` : d.direccion;

    // ============ CACHE DE PRODUCTOS ============
    const cargarProductosDeLocal = async (idLocal) => {
        if (!idLocal) return [];
        if (productosPorLocal[idLocal]) return productosPorLocal[idLocal];
        try {
            const resp = await fetch(`/Pedidos/ProductosPorLocal?idLocal=${encodeURIComponent(idLocal)}`, {
                credentials: "same-origin"
            });
            if (!resp.ok) return [];
            const data = await resp.json();
            productosPorLocal[idLocal] = data;
            return data;
        } catch {
            return [];
        }
    };

    // ============ ERRORES ============
    const limpiarErrores = () => {
        alertaErrores.classList.add("d-none");
        alertaErrores.replaceChildren();
        modalEl.querySelectorAll(".is-invalid").forEach(n => n.classList.remove("is-invalid"));
        msgBuscar.textContent = "";
    };

    const mostrarErrores = errores => {
        alertaErrores.replaceChildren(
            h("ul", { class: "mb-0" }, ...errores.map(e => h("li", {}, e.mensaje || e)))
        );
        alertaErrores.classList.remove("d-none");
    };

    // ============ TOTALES ============
    const actualizarTotales = () => {
        let total = 0;
        for (const b of bloques) {
            let subtotal = 0;
            for (const f of b.filas) {
                const prod = f.productoActual;
                const cant = Number(f.inputCantidad.value) || 0;
                const desc = Number(f.inputDescuento.value) || 0;
                if (prod && cant > 0) {
                    const bruto = prod.precio * cant;
                    const neto = Math.max(0, bruto - desc);
                    f.lblSubtotal.textContent = moneda(neto);
                    subtotal += neto;
                } else {
                    f.lblSubtotal.textContent = moneda(0);
                }
            }
            if (b.lblSubtotal) b.lblSubtotal.textContent = moneda(subtotal);
            total += subtotal;
        }
        lblTotal.textContent = moneda(total);
    };

    // ============ FILA DE PRODUCTO ============
    const crearFila = (bloque, detalleInicial = null) => {
        const fila = { productoActual: null };

        const selLocal = h("select", { class: "form-select", style: "font-size:12.5px;" },
            h("option", { value: "" }, "Local..."),
            ...localesDisponibles.map(l => h("option", { value: l.id }, l.nombre))
        );

        const selProducto = h("select", { class: "form-select", style: "font-size:12.5px;" },
            h("option", { value: "" }, "Producto...")
        );

        const inputCantidad = h("input", { type: "number", min: "1", step: "1", value: "1", class: "form-control", style: "font-size:12.5px;text-align:center;" });
        const inputDescuento = h("input", { type: "number", min: "0", step: "0.01", value: "0.00", class: "form-control", style: "font-size:12.5px;text-align:right;" });
        const lblSubtotal = h("span", { style: "font-size:12.5px;font-weight:600;color:#1d1d1f;display:block;text-align:right;" }, moneda(0));

        const btnEliminar = h("button", {
            type: "button",
            style: "width:30px;height:30px;border:none;background:transparent;color:#6e6e73;border-radius:8px;cursor:pointer;",
            onmouseover: function() { this.style.background = "#ffebea"; this.style.color = "#ff3b30"; },
            onmouseout: function() { this.style.background = "transparent"; this.style.color = "#6e6e73"; },
            onclick: () => {
                bloque.filas = bloque.filas.filter(x => x !== fila);
                fila.nodo.remove();
                actualizarTotales();
            }
        });
        btnEliminar.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg>';

        const actualizarProductosDelLocal = async () => {
            const idLocal = Number(selLocal.value);
            selProducto.replaceChildren(h("option", { value: "" }, "Cargando..."));
            selProducto.disabled = true;

            const productos = await cargarProductosDeLocal(idLocal);
            selProducto.replaceChildren(
                h("option", { value: "" }, "Producto..."),
                ...productos.map(p => h("option", { value: p.idProducto }, `${p.nombre} - ${moneda(p.precio)}`))
            );
            selProducto.disabled = !idLocal;
            fila.productoActual = null;
            actualizarTotales();
        };

        selLocal.addEventListener("change", () => {
            fila.productoActual = null;
            selProducto.value = "";
            actualizarProductosDelLocal();
        });

        selProducto.addEventListener("change", () => {
            const idProd = Number(selProducto.value);
            const productos = productosPorLocal[Number(selLocal.value)] || [];
            fila.productoActual = productos.find(p => p.idProducto === idProd) || null;
            actualizarTotales();
        });

        inputCantidad.addEventListener("input", actualizarTotales);
        inputDescuento.addEventListener("input", actualizarTotales);

        const nodo = h("div", { style: "display:grid;grid-template-columns:1fr 1.5fr 0.8fr 1fr 1fr auto;gap:8px;align-items:center;" },
            selLocal,
            selProducto,
            inputCantidad,
            inputDescuento,
            lblSubtotal,
            btnEliminar
        );

        fila.selLocal = selLocal;
        fila.selProducto = selProducto;
        fila.inputCantidad = inputCantidad;
        fila.inputDescuento = inputDescuento;
        fila.lblSubtotal = lblSubtotal;
        fila.nodo = nodo;

        if (detalleInicial) {
            selLocal.value = String(detalleInicial.idLocal || "");
            actualizarProductosDelLocal().then(() => {
                selProducto.value = String(detalleInicial.idProducto || "");
                inputCantidad.value = String(detalleInicial.cantidad || 1);
                inputDescuento.value = Number(detalleInicial.descuentoMonto || 0).toFixed(2);
                const productos = productosPorLocal[Number(selLocal.value)] || [];
                fila.productoActual = productos.find(p => p.idProducto === Number(detalleInicial.idProducto)) || null;
                actualizarTotales();
            });
        }

        bloque.filas.push(fila);
        bloque.contenedorFilas.append(nodo);
        actualizarTotales();
    };

    // ============ BLOQUE DE DIRECCIÓN ============
    const crearBloqueDireccion = (direccion, detallesIniciales = null) => {
        const bloque = { direccion, filas: [] };

        const contenedorFilas = h("div", { style: "display:grid;gap:8px;" });
        const lblSubtotal = h("strong", { style: "font-size:13.5px;color:#1d1d1f;" }, moneda(0));

        const btnAgregarFila = h("button", {
            type: "button",
            class: "btn-apple",
            style: "background:#f2f2f4;color:#1d1d1f;height:30px;font-size:12px;padding:0 12px;",
            onclick: () => crearFila(bloque)
        }, "+ Agregar producto");

        let btnQuitar = null;
        if (clienteActual && clienteActual.direcciones.length > 1) {
            btnQuitar = h("button", {
                type: "button",
                style: "background:transparent;border:none;color:#ff3b30;font-size:12px;font-weight:500;cursor:pointer;padding:4px 8px;border-radius:6px;",
                onmouseover: function() { this.style.background = "#ffebea"; },
                onmouseout: function() { this.style.background = "transparent"; },
                onclick: () => {
                    if (bloques.length <= 1) return;
                    bloques = bloques.filter(b => b !== bloque);
                    bloque.root.remove();
                    actualizarBotonAgregarDireccion();
                    actualizarTotales();
                }
            }, "Quitar dirección");
        }

        bloque.root = h("div", { style: "background:#ffffff;border:1px solid #e5e5ea;border-radius:14px;padding:16px;" },
            h("div", { style: "display:flex;justify-content:space-between;align-items:flex-start;gap:10px;margin-bottom:12px;flex-wrap:wrap;" },
                h("div", {},
                    h("div", { style: "font-size:11.5px;font-weight:600;color:#6e6e73;text-transform:uppercase;letter-spacing:0.05em;" }, "Dirección de entrega"),
                    h("div", { style: "font-size:14px;color:#1d1d1f;font-weight:600;margin-top:2px;" }, textoDireccion(direccion))
                ),
                btnQuitar
            ),
            h("div", { style: "display:grid;grid-template-columns:1fr 1.5fr 0.8fr 1fr 1fr auto;gap:8px;margin-bottom:6px;padding:0 2px;font-size:10.5px;font-weight:600;color:#6e6e73;text-transform:uppercase;letter-spacing:0.05em;" },
                h("div", {}, "Local"),
                h("div", {}, "Producto"),
                h("div", { style: "text-align:center;" }, "Cant."),
                h("div", { style: "text-align:right;" }, "Desc."),
                h("div", { style: "text-align:right;" }, "Subtotal"),
                h("div", {})
            ),
            contenedorFilas,
            h("div", { style: "display:flex;justify-content:space-between;align-items:center;margin-top:12px;" },
                btnAgregarFila,
                h("span", { style: "font-size:13px;color:#6e6e73;" }, "Subtotal: ", lblSubtotal)
            )
        );

        bloque.contenedorFilas = contenedorFilas;
        bloque.lblSubtotal = lblSubtotal;

        bloques.push(bloque);
        contenedor.append(bloque.root);

        if (detallesIniciales && detallesIniciales.length > 0) {
            detallesIniciales.forEach(d => crearFila(bloque, d));
        } else {
            crearFila(bloque);
        }
    };

    // ============ ACTUALIZAR BOTÓN AGREGAR DIRECCIÓN ============
    const actualizarBotonAgregarDireccion = () => {
        if (!clienteActual) {
            wrapperAgregarDireccion.style.display = "none";
            return;
        }

        const totalDirecciones = clienteActual.direcciones.length;
        const direccionesUsadas = bloques.map(b => b.direccion.id);
        const quedanDisponibles = clienteActual.direcciones.some(d => !direccionesUsadas.includes(d.id));

        if (totalDirecciones > 1 && quedanDisponibles) {
            wrapperAgregarDireccion.style.display = "block";
        } else {
            wrapperAgregarDireccion.style.display = "none";
        }
    };

    // ============ SELECCIONAR CLIENTE ============
    const seleccionarCliente = async (id) => {
        dropdown.style.display = "none";
        inpBuscar.value = "";
        msgBuscar.textContent = "";

        try {
            const resp = await fetch(`/Pedidos/ClientePorId?id=${id}`, { credentials: "same-origin" });
            if (!resp.ok) { msgBuscar.textContent = "No se pudo cargar el cliente."; return; }

            const cliente = await resp.json();
            if (!cliente.direcciones || cliente.direcciones.length === 0) {
                msgBuscar.textContent = "El cliente no tiene direcciones registradas.";
                return;
            }

            clienteActual = cliente;
            cliente.direcciones.sort((a, b) => (b.principal ? 1 : 0) - (a.principal ? 1 : 0));

            clienteNombre.textContent = cliente.nombre;
            clienteMeta.textContent = [cliente.telefono, cliente.dni ? `DNI ${cliente.dni}` : null]
                .filter(Boolean).join(" · ");

            buscadorCliente.style.display = "none";
            clienteSeleccionado.style.display = "block";
            contenedorVacio.style.display = "none";

            crearBloqueDireccion(cliente.direcciones[0]);
            actualizarBotonAgregarDireccion();
        } catch {
            msgBuscar.textContent = "No se pudo cargar el cliente.";
        }
    };

    // ============ CAMBIAR CLIENTE ============
    btnCambiarCliente.addEventListener("click", () => {
        clienteActual = null;
        bloques = [];
        contenedor.replaceChildren();
        clienteSeleccionado.style.display = "none";
        buscadorCliente.style.display = "block";
        wrapperAgregarDireccion.style.display = "none";
        contenedorVacio.style.display = "block";
        inpBuscar.value = "";
        msgBuscar.textContent = "";
        inpBuscar.focus();
        actualizarTotales();
    });

    // ============ AGREGAR OTRA DIRECCIÓN ============
    btnAgregarDireccion.addEventListener("click", () => {
        if (!clienteActual) return;

        const direccionesUsadas = bloques.map(b => b.direccion.id);
        const siguiente = clienteActual.direcciones.find(d => !direccionesUsadas.includes(d.id));

        if (!siguiente) return;

        crearBloqueDireccion(siguiente);
        actualizarBotonAgregarDireccion();
    });

    // ============ AUTOCOMPLETADO ============
    let debounceTimer = null;

    const buscarClientes = async (query) => {
        if (query.length < 2) {
            dropdown.style.display = "none";
            return;
        }

        try {
            const resp = await fetch(`/Pedidos/BuscarClientes?q=${encodeURIComponent(query)}&limite=8`, {
                credentials: "same-origin"
            });
            if (!resp.ok) { dropdown.style.display = "none"; return; }

            const clientes = await resp.json();
            if (!clientes.length) {
                dropdown.innerHTML = '<div style="padding:14px;font-size:12.5px;color:#6e6e73;text-align:center;">Sin resultados</div>';
                dropdown.style.display = "block";
                return;
            }

            dropdown.replaceChildren(
                ...clientes.map(c => h("button", {
                    type: "button",
                    style: "display:block;width:100%;padding:10px 14px;background:transparent;border:none;text-align:left;cursor:pointer;font-size:13px;border-bottom:1px solid #f0f0f2;",
                    onmouseover: function() { this.style.background = "#fbfcfd"; },
                    onmouseout: function() { this.style.background = "transparent"; },
                    onclick: () => seleccionarCliente(c.id)
                },
                    h("div", { style: "font-weight:600;color:#1d1d1f;" }, c.nombre),
                    h("div", { style: "font-size:11.5px;color:#6e6e73;margin-top:2px;" },
                        [c.telefono, c.dni ? `DNI ${c.dni}` : null].filter(Boolean).join(" · "))
                ))
            );
            dropdown.style.display = "block";
        } catch {
            dropdown.style.display = "none";
        }
    };

    inpBuscar.addEventListener("input", () => {
        clearTimeout(debounceTimer);
        const q = inpBuscar.value.trim();
        debounceTimer = setTimeout(() => buscarClientes(q), 300);
    });

    inpBuscar.addEventListener("keydown", e => {
        if (e.key === "Escape") dropdown.style.display = "none";
    });

    document.addEventListener("click", e => {
        if (!e.target.closest("#autocompleteDropdown") && e.target !== inpBuscar) {
            dropdown.style.display = "none";
        }
    });

    // ============ SERIALIZACIÓN ============
    // ✅ FIX 3: eliminada la función `serializar` que no se usaba.
    const serializarCompleto = () => ({
        idRepartidor: null,
        clientes: bloques.map(b => ({
            idCliente: clienteActual.id,
            idDireccion: b.direccion.id,
            detalles: b.filas.map(f => ({
                idLocal: Number(f.selLocal.value),
                idProducto: Number(f.selProducto.value),
                cantidad: Number(f.inputCantidad.value) || 0,
                descuentoMonto: Number(f.inputDescuento.value) || 0
            }))
        }))
    });

    // ============ GUARDAR ============
    // ✅ FIX 2: no hacemos reload aquí. Solo marcamos pedidoRegistrado
    // y cerramos el modal. El reload lo hace hidden.bs.modal.
    const registrar = async (url) => {
        limpiarErrores();

        if (!clienteActual) {
            mostrarErrores([{ mensaje: "Selecciona un cliente." }]);
            return;
        }

        const cuerpo = serializarCompleto();

        btnGuardar.disabled = true;
        if (btnEnviar) btnEnviar.disabled = true;

        try {
            const resp = await fetch(url, {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
                body: JSON.stringify(cuerpo)
            });
            const datos = await resp.json().catch(() => null);

            if (!resp.ok) {
                mostrarErrores(datos?.errores ?? [{ mensaje: "No se pudo guardar el pedido." }]);
                btnGuardar.disabled = false;
                if (btnEnviar) btnEnviar.disabled = false;
                return;
            }

            if (datos?.mensaje) {
                sessionStorage.setItem("pedidoToast", datos.mensaje);
            }

            pedidoRegistrado = true;
            bootstrap.Modal.getOrCreateInstance(modalEl).hide();
        } catch {
            mostrarErrores([{ mensaje: "No se pudo conectar con el servidor." }]);
            btnGuardar.disabled = false;
            if (btnEnviar) btnEnviar.disabled = false;
        }
    };

    // ============ ENVIAR ============
    const enviar = async () => {
        limpiarErrores();

        if (!clienteActual) {
            mostrarErrores([{ mensaje: "Selecciona un cliente." }]);
            return;
        }

        abrirModalSeleccionRepartidor(async (idRepartidor, nombreRepartidor) => {
            const cuerpo = serializarCompleto();
            cuerpo.idRepartidor = idRepartidor;

            btnEnviar.disabled = true;

            try {
                const respPreview = await fetch("/Pedidos/PreviewEnviar", {
                    method: "POST",
                    credentials: "same-origin",
                    headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
                    body: JSON.stringify(cuerpo)
                });
                const datosPreview = await respPreview.json().catch(() => null);

                if (!respPreview.ok) {
                    mostrarErrores(datosPreview?.errores ?? [{ mensaje: "No se pudo validar el pedido." }]);
                    btnEnviar.disabled = false;
                    return;
                }

                const previewModalEl = document.getElementById("previewEnvioModal");
                document.getElementById("previewEnvioMensaje").textContent = datosPreview.mensaje ?? "";
                document.getElementById("previewEnvioDestinatario").textContent = nombreRepartidor;

                const confirmBtn = document.getElementById("previewEnvioConfirmar");
                const nuevoBtn = confirmBtn.cloneNode(true);
                confirmBtn.parentNode.replaceChild(nuevoBtn, confirmBtn);

                nuevoBtn.addEventListener("click", async () => {
                    nuevoBtn.disabled = true;
                    try {
                        const resp2 = await fetch("/Pedidos/Enviar", {
                            method: "POST",
                            credentials: "same-origin",
                            headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
                            body: JSON.stringify(cuerpo)
                        });
                        const datos2 = await resp2.json().catch(() => null);
                        if (!resp2.ok) {
                            mostrarErrores(datos2?.errores ?? [{ mensaje: "No se pudo enviar el pedido." }]);
                            nuevoBtn.disabled = false;
                            return;
                        }

                        sessionStorage.setItem("pedidoToast", "PEDIDO ENVIADO CORRECTAMENTE");
                        pedidoRegistrado = true;
                        bootstrap.Modal.getOrCreateInstance(previewModalEl).hide();
                        bootstrap.Modal.getOrCreateInstance(modalEl).hide();
                        window.open(datos2.whatsappUrl, "_blank");
                    } catch {
                        mostrarErrores([{ mensaje: "No se pudo conectar con el servidor." }]);
                        nuevoBtn.disabled = false;
                    }
                });

                bootstrap.Modal.getOrCreateInstance(previewModalEl).show();
                btnEnviar.disabled = false;
            } catch {
                mostrarErrores([{ mensaje: "No se pudo conectar con el servidor." }]);
                btnEnviar.disabled = false;
            }
        });
    };

    // ============ MODAL SELECCIÓN REPARTIDOR ============
    const abrirModalSeleccionRepartidor = (callback) => {
        if (!repartidoresDisponibles.length) {
            mostrarErrores([{ mensaje: "No hay repartidores activos." }]);
            return;
        }

        const modal = document.getElementById("modalConfirm");
        document.getElementById("modalConfirmIcon").className = "modal-custom-icon info";
        document.getElementById("modalConfirmTitle").textContent = "Selecciona un repartidor";

        const textContainer = document.getElementById("modalConfirmText");
        textContainer.innerHTML = "";

        const lista = h("div", { style: "display:grid;gap:6px;margin-top:8px;" },
            ...repartidoresDisponibles.map(r => h("button", {
                type: "button",
                style: "padding:10px 14px;background:#fbfcfd;border:1px solid #e5e5ea;border-radius:9px;font-size:13px;color:#1d1d1f;cursor:pointer;text-align:left;font-weight:500;transition:all 0.15s;",
                onmouseover: function() { this.style.background = "#1d1d1f"; this.style.color = "#ffffff"; },
                onmouseout: function() { this.style.background = "#fbfcfd"; this.style.color = "#1d1d1f"; },
                onclick: () => {
                    cerrarModalConfirm();
                    callback(r.id, r.nombre);
                }
            }, r.nombre))
        );

        textContainer.appendChild(lista);

        const btnConfirm = document.getElementById("modalConfirmBtn");
        btnConfirm.style.display = "none";

        modal.classList.add("show");
    };

    // ============ MODAL CONFIRMACIÓN ============
    let accionPendiente = null;

    window.abrirModalConfirm = (titulo, texto, tipo, callback, textoBoton) => {
        const modal = document.getElementById("modalConfirm");
        const icon = document.getElementById("modalConfirmIcon");
        const title = document.getElementById("modalConfirmTitle");
        const text = document.getElementById("modalConfirmText");
        const btn = document.getElementById("modalConfirmBtn");

        icon.className = "modal-custom-icon " + (tipo === "success" ? "success" : tipo === "info" ? "info" : "danger");
        title.textContent = titulo;
        text.innerHTML = texto;
        btn.style.display = "";
        btn.textContent = textoBoton || "Confirmar";
        btn.className = "modal-custom-btn " + (tipo === "danger" ? "danger" : "primary");

        accionPendiente = callback;
        modal.classList.add("show");
    };

    window.cerrarModalConfirm = () => {
        document.getElementById("modalConfirm").classList.remove("show");
        accionPendiente = null;
    };

    document.getElementById("modalConfirmBtn")?.addEventListener("click", () => {
        if (accionPendiente) { accionPendiente(); accionPendiente = null; }
        cerrarModalConfirm();
    });

    document.getElementById("modalConfirm")?.addEventListener("click", function (e) {
        if (e.target === this) cerrarModalConfirm();
    });

    // ============ CARGAR PEDIDO PARA EDITAR ============
    const cargarPedidoParaEditar = async (idPedido) => {
        clienteActual = null;
        bloques = [];
        contenedor.replaceChildren();
        contenedorVacio.style.display = "block";
        clienteSeleccionado.style.display = "none";
        buscadorCliente.style.display = "block";
        wrapperAgregarDireccion.style.display = "none";
        productosPorLocal = {};
        actualizarTotales();

        try {
            const resp = await fetch(`/Pedidos/ObtenerParaEditar?idPedido=${idPedido}`, {
                credentials: "same-origin"
            });
            if (!resp.ok) throw new Error();
            const pedido = await resp.json();

            $("pedidoModalLabel").textContent = `Editar pedido N° ${pedido.idPedido}`;

            if (pedido.clientes && pedido.clientes.length > 0) {
                const primerCliente = pedido.clientes[0].cliente;
                clienteActual = primerCliente;
                primerCliente.direcciones.sort((a, b) => (b.principal ? 1 : 0) - (a.principal ? 1 : 0));

                clienteNombre.textContent = primerCliente.nombre;
                clienteMeta.textContent = [primerCliente.telefono, primerCliente.dni ? `DNI ${primerCliente.dni}` : null]
                    .filter(Boolean).join(" · ");

                buscadorCliente.style.display = "none";
                clienteSeleccionado.style.display = "block";
                contenedorVacio.style.display = "none";

                for (const linea of pedido.clientes) {
                    const dir = primerCliente.direcciones.find(d => d.id === linea.idDireccion)
                        || { id: linea.idDireccion, direccion: "(sin dirección)", ciudad: null };
                    crearBloqueDireccion(dir, linea.detalles);
                }

                actualizarBotonAgregarDireccion();
            }

            actualizarTotales();
        } catch {
            mostrarErrores([{ mensaje: "No se pudo cargar el pedido para editar." }]);
        }
    };

    // ============ REINICIAR ============
    const reiniciar = () => {
        limpiarErrores();
        alertaEnviado.classList.add("d-none");
        formulario.classList.remove("d-none");
        inpBuscar.value = "";
        dropdown.style.display = "none";
        clienteActual = null;
        bloques = [];
        contenedor.replaceChildren();
        contenedorVacio.style.display = "block";
        clienteSeleccionado.style.display = "none";
        buscadorCliente.style.display = "block";
        wrapperAgregarDireccion.style.display = "none";
        productosPorLocal = {};
        actualizarTotales();
        btnGuardar.disabled = false;
        if (btnEnviar) btnEnviar.disabled = false;
        $("pedidoModalLabel").textContent = "Crear pedido";
        pedidoAEditar = null;
    };

    // ============ EVENTOS DEL MODAL ============
    modalEl.addEventListener("show.bs.modal", async (evento) => {
        const trigger = evento.relatedTarget;
        const esEdicion = trigger && trigger.classList?.contains("pedido-editar");

        reiniciar();

        if (esEdicion) {
            pedidoAEditar = trigger.dataset.pedidoId;
            await cargarPedidoParaEditar(pedidoAEditar);
        }
    });

    modalEl.addEventListener("hidden.bs.modal", () => {
        if (pedidoRegistrado) {
            window.location.reload();
            return;
        }
        reiniciar();
    });

    // ============ BOTONES ============
    $("pedidoGuardarBtn").addEventListener("click", () => registrar(
        pedidoAEditar === null ? "/Pedidos/Guardar" : `/Pedidos/Actualizar?idPedido=${pedidoAEditar}`
    ));
    $("pedidoEnviarBtn").addEventListener("click", enviar);

    document.getElementById("btnNuevoPedido")?.addEventListener("click", () => {
        bootstrap.Modal.getOrCreateInstance(modalEl).show();
    });
})();