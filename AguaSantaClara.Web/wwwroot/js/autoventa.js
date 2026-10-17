(() => {
    const modalEl = document.getElementById("autoventaModal");
    if (!modalEl) return;

    // ============ ABRIR MODAL (T01) ============
    document.getElementById("btnAutoventa")?.addEventListener("click", () => {
        bootstrap.Modal.getOrCreateInstance(modalEl).show();
    });

    // ============ REFERENCIAS DEL FORMULARIO (T02) ============
    const $ = id => document.getElementById(id);
    const inpBuscarCliente = $("autoventaBuscarCliente");
    const btnNuevo = $("btnAutoventaNuevo");
    const btnNuevoCerrar = $("btnAutoventaNuevoCerrar");
    const bloqueClienteNuevo = $("autoventaClienteNuevo");
    const contenedorFilas = $("autoventaFilas");
    const plantillaFila = $("autoventaFilaTemplate");

    const PLACEHOLDER_BUSCADOR = "Buscar por nombre, DNI o teléfono...";
    const PLACEHOLDER_NUEVO = "Oculto: registrando cliente nuevo";
    const PLACEHOLDER_SELECCIONADO = "Cliente seleccionado";

    // ============ REFERENCIAS DE LA BÚSQUEDA DE CLIENTE (T03) ============
    const dropdown = $("autoventaDropdown");
    const msgBuscar = $("autoventaBuscarMensaje");
    const tarjetaCliente = $("autoventaClienteSel");
    const lblClienteNombre = $("autoventaClienteNombre");
    const lblClienteMeta = $("autoventaClienteMeta");
    const btnCambiarCliente = $("btnAutoventaCambiarCliente");

    // Cliente existente elegido en el buscador (null si no hay ninguno)
    let clienteActual = null;

    // ============ CLIENTE EXISTENTE: BUSCAR Y SELECCIONAR (T03) ============
    const cerrarDropdown = () => {
        dropdown.classList.remove("show");
        dropdown.replaceChildren();
    };

    const crearItemCliente = cliente => {
        const item = document.createElement("button");
        item.type = "button";
        item.className = "av-dropdown-item";

        const nombre = document.createElement("div");
        nombre.style.cssText = "font-weight:600;color:#1d1d1f;";
        nombre.textContent = cliente.nombre;

        const meta = document.createElement("div");
        meta.style.cssText = "font-size:11.5px;color:#6e6e73;margin-top:2px;";
        meta.textContent = [cliente.telefono, cliente.dni ? `DNI ${cliente.dni}` : null]
            .filter(Boolean).join(" · ");

        item.append(nombre, meta);
        item.addEventListener("click", () => seleccionarCliente(cliente));
        return item;
    };

    const mostrarResultados = clientes => {
        if (!clientes.length) {
            const vacio = document.createElement("div");
            vacio.className = "av-dropdown-vacio";
            vacio.textContent = "Sin resultados";
            dropdown.replaceChildren(vacio);
        } else {
            dropdown.replaceChildren(...clientes.map(crearItemCliente));
        }
        dropdown.classList.add("show");
    };

    let temporizadorBusqueda = null;
    let numeroBusqueda = 0; // descarta respuestas viejas si el usuario siguió escribiendo

    const buscarClientes = async consulta => {
        if (consulta.length < 2) {
            cerrarDropdown();
            return;
        }

        const miBusqueda = ++numeroBusqueda;

        try {
            const resp = await fetch(
                `/Pedidos/BuscarClientes?q=${encodeURIComponent(consulta)}&limite=8`,
                { credentials: "same-origin" });

            if (miBusqueda !== numeroBusqueda) return;

            if (!resp.ok) {
                cerrarDropdown();
                msgBuscar.textContent = "No se pudo buscar clientes.";
                return;
            }

            mostrarResultados(await resp.json());
        } catch {
            if (miBusqueda !== numeroBusqueda) return;
            cerrarDropdown();
            msgBuscar.textContent = "No se pudo conectar con el servidor.";
        }
    };

    const seleccionarCliente = cliente => {
        clearTimeout(temporizadorBusqueda);
        numeroBusqueda++;
        clienteActual = cliente;

        cerrarDropdown();
        msgBuscar.textContent = "";

        inpBuscarCliente.value = "";
        inpBuscarCliente.disabled = true;
        inpBuscarCliente.placeholder = PLACEHOLDER_SELECCIONADO;

        lblClienteNombre.textContent = cliente.nombre;
        lblClienteMeta.textContent = [cliente.telefono, cliente.dni ? `DNI ${cliente.dni}` : null]
            .filter(Boolean).join(" · ");
        tarjetaCliente.classList.remove("d-none");
    };

    // Quita el cliente elegido y deja el buscador listo (sin tocar el foco)
    const limpiarClienteSeleccionado = () => {
        clearTimeout(temporizadorBusqueda);
        numeroBusqueda++;
        clienteActual = null;

        cerrarDropdown();
        msgBuscar.textContent = "";
        tarjetaCliente.classList.add("d-none");
        lblClienteNombre.textContent = "";
        lblClienteMeta.textContent = "";

        inpBuscarCliente.value = "";
        inpBuscarCliente.disabled = false;
        inpBuscarCliente.placeholder = PLACEHOLDER_BUSCADOR;
    };

    inpBuscarCliente?.addEventListener("input", () => {
        clearTimeout(temporizadorBusqueda);
        msgBuscar.textContent = "";
        const consulta = inpBuscarCliente.value.trim();
        temporizadorBusqueda = setTimeout(() => buscarClientes(consulta), 300);
    });

    inpBuscarCliente?.addEventListener("keydown", e => {
        // Si el desplegable está abierto, Escape solo lo cierra (no cierra el modal)
        if (e.key === "Escape" && dropdown.classList.contains("show")) {
            e.stopPropagation();
            cerrarDropdown();
        }
    });

    modalEl.addEventListener("click", e => {
        if (!e.target.closest("#autoventaDropdown") && e.target !== inpBuscarCliente) {
            cerrarDropdown();
        }
    });

    btnCambiarCliente?.addEventListener("click", () => {
        limpiarClienteSeleccionado();
        inpBuscarCliente.focus();
    });

    // ============ CLIENTE NUEVO: MOSTRAR / OCULTAR (T02) ============
    const mostrarClienteNuevo = () => {
        // T03: cliente existente y cliente nuevo son excluyentes
        limpiarClienteSeleccionado();

        bloqueClienteNuevo.classList.remove("d-none");
        inpBuscarCliente.value = "";
        inpBuscarCliente.disabled = true;
        inpBuscarCliente.placeholder = PLACEHOLDER_NUEVO;
        btnNuevo.textContent = "Cancelar";
    };

    const ocultarClienteNuevo = () => {
        bloqueClienteNuevo.classList.add("d-none");
        inpBuscarCliente.disabled = false;
        inpBuscarCliente.placeholder = PLACEHOLDER_BUSCADOR;
        btnNuevo.textContent = "+ Nuevo";
    };

    btnNuevo?.addEventListener("click", () => {
        if (bloqueClienteNuevo.classList.contains("d-none")) mostrarClienteNuevo();
        else ocultarClienteNuevo();
    });

    btnNuevoCerrar?.addEventListener("click", ocultarClienteNuevo);

    // ============ PRIMERA FILA DE PRODUCTO (T02) ============
    const dibujarPrimeraFila = () => {
        if (!plantillaFila || !contenedorFilas) return;
        contenedorFilas.replaceChildren(plantillaFila.content.cloneNode(true));
    };

    modalEl.addEventListener("show.bs.modal", () => {
        limpiarClienteSeleccionado(); // T03: buscador limpio al abrir
        ocultarClienteNuevo();
        dibujarPrimeraFila();
    });
})();