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
    const btnAgregarProducto = $("btnAutoventaAgregar"); // T06

    const PLACEHOLDER_BUSCADOR = "Buscar por nombre, DNI o teléfono...";
    const PLACEHOLDER_NUEVO = "Oculto: registrando cliente nuevo";
    const PLACEHOLDER_SELECCIONADO = "Cliente seleccionado";

    const moneda = v => "S/ " + Number(v || 0).toFixed(2); // T05

    // ============ REFERENCIAS DE LA BÚSQUEDA DE CLIENTE (T03) ============
    const dropdown = $("autoventaDropdown");
    const msgBuscar = $("autoventaBuscarMensaje");
    const tarjetaCliente = $("autoventaClienteSel");
    const lblClienteNombre = $("autoventaClienteNombre");
    const lblClienteMeta = $("autoventaClienteMeta");
    const btnCambiarCliente = $("btnAutoventaCambiarCliente");

    // Cliente existente elegido en el buscador (null si no hay ninguno)
    let clienteActual = null;

    // ============ REFERENCIAS DEL CLIENTE NUEVO (T04) ============
    const inpNuevoNombre = $("autoventaNuevoNombre");
    const inpNuevoTelefono = $("autoventaNuevoTelefono");
    const inpNuevoDni = $("autoventaNuevoDni");
    const inpNuevoDireccion = $("autoventaNuevoDireccion");
    const inpNuevoCiudad = $("autoventaNuevoCiudad");
    const inpNuevoReferencia = $("autoventaNuevoReferencia");

    const errNuevoNombre = $("autoventaNuevoNombreError");
    const errNuevoTelefono = $("autoventaNuevoTelefonoError");
    const errNuevoDni = $("autoventaNuevoDniError");
    const errNuevoDireccion = $("autoventaNuevoDireccionError");

    const MSG_DNI_DUPLICADO = "Ya existe un cliente con ese dni";

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

    // ============ CLIENTE NUEVO: VALIDACIONES (T04) ============
    const setError = (input, etiqueta, mensaje) => {
        etiqueta.textContent = mensaje;
        input.classList.toggle("is-invalid", Boolean(mensaje));
    };

    const validarObligatorio = (input, etiqueta, mensaje) => {
        const valido = input.value.trim().length > 0;
        setError(input, etiqueta, valido ? "" : mensaje);
        return valido;
    };

    const validarNombre = () =>
        validarObligatorio(inpNuevoNombre, errNuevoNombre, "Ingresa el nombre o la razón social.");

    const validarTelefono = () =>
        validarObligatorio(inpNuevoTelefono, errNuevoTelefono, "Ingresa el teléfono.");

    const validarDireccion = () =>
        validarObligatorio(inpNuevoDireccion, errNuevoDireccion, "Ingresa la dirección.");

    let numeroValidacionDni = 0; // descarta respuestas viejas del servidor

    // DNI opcional: si se ingresa, 8 dígitos y único. Devuelve true si es válido.
    const validarDni = async () => {
        const valor = inpNuevoDni.value.trim();
        const miValidacion = ++numeroValidacionDni;

        if (!valor) {
            setError(inpNuevoDni, errNuevoDni, "");
            return true;
        }

        if (!/^\d{8}$/.test(valor)) {
            setError(inpNuevoDni, errNuevoDni, "El DNI debe tener 8 dígitos.");
            return false;
        }

        try {
            const resp = await fetch(
                `/Pedidos/ValidarDniCliente?dni=${encodeURIComponent(valor)}`,
                { credentials: "same-origin" });

            // Si el servidor no responde bien, no bloqueamos: la T10 revalida al guardar
            if (!resp.ok) return true;

            const { existe } = await resp.json();
            if (miValidacion === numeroValidacionDni) {
                setError(inpNuevoDni, errNuevoDni, existe ? MSG_DNI_DUPLICADO : "");
            }
            return !existe;
        } catch {
            return true;
        }
    };

    // Valida todo el bloque. La usará T10 antes de guardar.
    const validarClienteNuevo = async () => {
        const nombreOk = validarNombre();
        const telefonoOk = validarTelefono();
        const direccionOk = validarDireccion();
        const dniOk = await validarDni();

        if (!nombreOk) inpNuevoNombre.focus();
        else if (!telefonoOk) inpNuevoTelefono.focus();
        else if (!dniOk) inpNuevoDni.focus();
        else if (!direccionOk) inpNuevoDireccion.focus();

        return nombreOk && telefonoOk && direccionOk && dniOk;
    };

    // Datos limpios del cliente nuevo para el guardado (T10). null si el bloque está oculto.
    const obtenerClienteNuevo = () => {
        if (bloqueClienteNuevo.classList.contains("d-none")) return null;

        const opcional = input => input.value.trim() || null;

        return {
            nombre: inpNuevoNombre.value.trim(),
            telefono: inpNuevoTelefono.value.trim(),
            dni: opcional(inpNuevoDni),
            direccion: inpNuevoDireccion.value.trim(),
            ciudad: opcional(inpNuevoCiudad),
            referencia: opcional(inpNuevoReferencia)
        };
    };

    // Vacía los campos y quita los errores del bloque Cliente nuevo
    const limpiarClienteNuevo = () => {
        numeroValidacionDni++;

        [inpNuevoNombre, inpNuevoTelefono, inpNuevoDni, inpNuevoDireccion,
            inpNuevoCiudad, inpNuevoReferencia].forEach(input => {
            input.value = "";
            input.classList.remove("is-invalid");
        });

        [errNuevoNombre, errNuevoTelefono, errNuevoDni, errNuevoDireccion]
            .forEach(etiqueta => { etiqueta.textContent = ""; });
    };

    // Validar al salir del campo y quitar el error al volver a escribir
    inpNuevoNombre?.addEventListener("blur", validarNombre);
    inpNuevoNombre?.addEventListener("input", () => setError(inpNuevoNombre, errNuevoNombre, ""));

    inpNuevoTelefono?.addEventListener("blur", validarTelefono);
    inpNuevoTelefono?.addEventListener("input", () => setError(inpNuevoTelefono, errNuevoTelefono, ""));

    inpNuevoDireccion?.addEventListener("blur", validarDireccion);
    inpNuevoDireccion?.addEventListener("input", () => setError(inpNuevoDireccion, errNuevoDireccion, ""));

    inpNuevoDni?.addEventListener("input", () => {
        // Solo dígitos
        inpNuevoDni.value = inpNuevoDni.value.replace(/\D/g, "");
        numeroValidacionDni++;
        setError(inpNuevoDni, errNuevoDni, "");
    });
    inpNuevoDni?.addEventListener("blur", validarDni);

    // ============ CLIENTE NUEVO: MOSTRAR / OCULTAR (T02) ============
    const mostrarClienteNuevo = () => {
        // T03: cliente existente y cliente nuevo son excluyentes
        limpiarClienteSeleccionado();

        limpiarClienteNuevo(); // T04
        bloqueClienteNuevo.classList.remove("d-none");
        inpBuscarCliente.value = "";
        inpBuscarCliente.disabled = true;
        inpBuscarCliente.placeholder = PLACEHOLDER_NUEVO;
        btnNuevo.textContent = "Cancelar";
    };

    const ocultarClienteNuevo = () => {
        limpiarClienteNuevo(); // T04: Cancelar descarta lo escrito
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

    // ============ PRODUCTOS: CATÁLOGO PRODUCTO + LOCAL (T05) ============
    const localesDisponibles = window.__pedidosData?.locales ?? [];

    // Cada elemento: { clave, idLocal, localNombre, idProducto, nombre, precio, stock }
    let catalogo = [];
    let estadoCatalogo = "cargando"; // "cargando" | "listo" | "error"
    let numeroCargaCatalogo = 0;     // descarta respuestas viejas al reabrir el modal

    const claveProducto = (idLocal, idProducto) => `${idLocal}:${idProducto}`;

    const textoOpcionProducto = p =>
        `${p.nombre} · ${p.localNombre} (stock ${p.stock}) · ${moneda(p.precio)}`;

    const textoPlaceholderProducto = () => {
        if (estadoCatalogo === "cargando") return "Cargando productos...";
        if (estadoCatalogo === "error") return "No se pudieron cargar los productos";
        if (!catalogo.length) return "No hay productos disponibles";
        return "Producto...";
    };

    // Llena un desplegable de producto conservando la opción elegida (si sigue existiendo)
    const llenarSelectProducto = select => {
        const valorActual = select.value;

        const placeholder = document.createElement("option");
        placeholder.value = "";
        placeholder.textContent = textoPlaceholderProducto();

        const opciones = catalogo.map(p => {
            const opcion = document.createElement("option");
            opcion.value = p.clave;
            opcion.textContent = textoOpcionProducto(p);
            return opcion;
        });

        select.replaceChildren(placeholder, ...opciones);

        if (valorActual && catalogo.some(p => p.clave === valorActual)) {
            select.value = valorActual;
        }
        select.disabled = estadoCatalogo !== "listo" || !catalogo.length;
    };

    // Llena todos los desplegables de producto que hay en pantalla
    const llenarSelectsProducto = () => {
        contenedorFilas?.querySelectorAll(".av-fila-producto").forEach(llenarSelectProducto);
    };

    // Consulta los productos de cada local activo (una vez por apertura del modal)
    const cargarCatalogo = async () => {
        const miCarga = ++numeroCargaCatalogo;
        estadoCatalogo = "cargando";
        catalogo = [];
        llenarSelectsProducto();

        try {
            const respuestas = await Promise.all(localesDisponibles.map(async local => {
                const resp = await fetch(
                    `/Pedidos/ProductosPorLocal?idLocal=${encodeURIComponent(local.id)}`,
                    { credentials: "same-origin" });
                if (!resp.ok) throw new Error("Error al cargar productos");
                return { local, productos: await resp.json() };
            }));

            if (miCarga !== numeroCargaCatalogo) return;

            catalogo = respuestas
                .flatMap(({ local, productos }) => productos.map(p => ({
                    clave: claveProducto(local.id, p.idProducto),
                    idLocal: local.id,
                    localNombre: local.nombre,
                    idProducto: p.idProducto,
                    nombre: p.nombre,
                    precio: Number(p.precio),
                    stock: Number(p.stock)
                })))
                .sort((a, b) =>
                    a.nombre.localeCompare(b.nombre, "es") ||
                    a.localNombre.localeCompare(b.localNombre, "es"));

            estadoCatalogo = "listo";
        } catch {
            if (miCarga !== numeroCargaCatalogo) return;
            catalogo = [];
            estadoCatalogo = "error";
        }

        llenarSelectsProducto();
    };

    // ============ PRODUCTOS: FILAS Y CANTIDADES (T06) ============
    // Mensaje de error de la sección de productos (se crea aquí, bajo la lista de filas)
    const msgProductos = document.createElement("div");
    msgProductos.id = "autoventaProductosError";
    msgProductos.className = "av-msg-error";
    contenedorFilas?.insertAdjacentElement("afterend", msgProductos);

    const MSG_CANTIDAD = "La cantidad debe ser un número entero mayor a cero.";
    const MSG_PRODUCTO = "Selecciona un producto en cada fila o quita la fila vacía.";

    const obtenerFilas = () =>
        contenedorFilas ? [...contenedorFilas.querySelectorAll(".av-fila")] : [];

    const cantidadValida = input => {
        const valor = input.value.trim();
        return /^\d+$/.test(valor) && Number(valor) >= 1;
    };

    // Marca o limpia el borde rojo de una cantidad. Devuelve true si es válida.
    const marcarCantidad = input => {
        const valida = cantidadValida(input);
        input.classList.toggle("is-invalid", !valida);
        return valida;
    };

    // Deja el mensaje solo si todavía hay alguna cantidad inválida
    const refrescarMensajeProductos = () => {
        const hayInvalida = contenedorFilas?.querySelector(".av-fila-cantidad.is-invalid");
        msgProductos.textContent = hayInvalida ? MSG_CANTIDAD : "";
    };

    const agregarFila = () => {
        if (!plantillaFila || !contenedorFilas) return;
        contenedorFilas.append(plantillaFila.content.cloneNode(true));

        const select = contenedorFilas.lastElementChild.querySelector(".av-fila-producto");
        llenarSelectProducto(select);
        select.focus();
    };

    const quitarFila = fila => {
        if (obtenerFilas().length > 1) {
            fila.remove();
        } else {
            // Siempre queda al menos una fila: la última solo se vacía
            fila.querySelector(".av-fila-producto").value = "";
            fila.querySelector(".av-fila-cantidad").value = "1";
            fila.querySelector(".av-fila-descuento").value = "0.00";
            fila.querySelectorAll(".is-invalid").forEach(n => n.classList.remove("is-invalid"));
        }
        refrescarMensajeProductos();
    };

    // Valida todas las filas. La usará T10 antes de guardar.
    const validarProductos = () => {
        let mensaje = "";

        obtenerFilas().forEach(fila => {
            const select = fila.querySelector(".av-fila-producto");
            const cantidad = fila.querySelector(".av-fila-cantidad");

            if (!marcarCantidad(cantidad)) mensaje = MSG_CANTIDAD;
            if (!select.value && !mensaje) mensaje = MSG_PRODUCTO;
        });

        msgProductos.textContent = mensaje;
        return mensaje === "";
    };

    // Productos elegidos listos para el guardado (T10): idLocal, idProducto y cantidad
    const obtenerProductos = () =>
        obtenerFilas()
            .map(fila => {
                const elegido = catalogo.find(
                    p => p.clave === fila.querySelector(".av-fila-producto").value);
                if (!elegido) return null;

                return {
                    idLocal: elegido.idLocal,
                    idProducto: elegido.idProducto,
                    cantidad: Number(fila.querySelector(".av-fila-cantidad").value)
                };
            })
            .filter(Boolean);

    btnAgregarProducto?.addEventListener("click", agregarFila);

    // Eventos delegados: valen para la primera fila y para las que se agreguen después
    contenedorFilas?.addEventListener("click", e => {
        const botonQuitar = e.target.closest(".av-fila-quitar");
        if (botonQuitar) quitarFila(botonQuitar.closest(".av-fila"));
    });

    contenedorFilas?.addEventListener("focusout", e => {
        if (e.target.classList.contains("av-fila-cantidad")) {
            marcarCantidad(e.target);
            refrescarMensajeProductos();
        }
    });

    contenedorFilas?.addEventListener("input", e => {
        if (e.target.classList.contains("av-fila-cantidad")) {
            e.target.classList.remove("is-invalid");
            refrescarMensajeProductos();
        }
    });

    // ============ PRIMERA FILA DE PRODUCTO (T02) ============
    const dibujarPrimeraFila = () => {
        if (!plantillaFila || !contenedorFilas) return;
        contenedorFilas.replaceChildren(plantillaFila.content.cloneNode(true));
        msgProductos.textContent = ""; // T06
        llenarSelectsProducto(); // T05
    };

    modalEl.addEventListener("show.bs.modal", () => {
        limpiarClienteSeleccionado(); // T03: buscador limpio al abrir
        ocultarClienteNuevo();        // T02 + T04: bloque oculto y vacío
        dibujarPrimeraFila();
        cargarCatalogo();             // T05: productos al día cada vez que se abre
    });
})();