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
    const lblTotal = $("autoventaTotal");                // T08
    const selMetodoPago = $("autoventaMetodoPago");      // T09
    const errMetodoPago = $("autoventaMetodoPagoError"); // T09

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
        actualizarTotal(); // T08: los precios del catálogo recién cargado pueden cambiar el total
    };

    // ============ PRODUCTOS: FILAS Y CANTIDADES (T06) ============
    // Mensaje de error de la sección de productos (se crea aquí, bajo la lista de filas)
    const msgProductos = document.createElement("div");
    msgProductos.id = "autoventaProductosError";
    msgProductos.className = "av-msg-error";
    contenedorFilas?.insertAdjacentElement("afterend", msgProductos);

    const MSG_CANTIDAD = "La cantidad debe ser un número entero mayor a cero.";
    const MSG_PRODUCTO = "Selecciona un producto en cada fila o quita la fila vacía.";
    const MSG_DESCUENTO = "El descuento debe ser un monto mayor o igual a cero, con máximo 2 decimales."; // T07

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

    // ============ PRODUCTOS: DESCUENTOS (T07) ============
    // Producto + local elegido en una fila (undefined si todavía no eligió)
    const productoElegido = fila =>
        catalogo.find(p => p.clave === fila.querySelector(".av-fila-producto").value);

    // Devuelve el mensaje de error del descuento de una fila, o "" si es válido
    const descuentoMensaje = fila => {
        const texto = fila.querySelector(".av-fila-descuento").value.trim();
        if (texto === "") return ""; // vacío se toma como 0

        // No negativo y máximo 2 decimales
        if (!/^(\d+|\d*\.\d{1,2})$/.test(texto)) return MSG_DESCUENTO;

        // No puede superar cantidad × precio (solo se compara si ya se puede calcular)
        const elegido = productoElegido(fila);
        const cantidad = fila.querySelector(".av-fila-cantidad").value.trim();
        if (!elegido || !/^\d+$/.test(cantidad) || Number(cantidad) < 1) return "";

        const brutoCentimos = Math.round(elegido.precio * 100) * Number(cantidad);
        if (Math.round(Number(texto) * 100) > brutoCentimos) {
            return `El descuento no puede superar el monto del producto (${moneda(brutoCentimos / 100)}).`;
        }
        return "";
    };

    // Marca o limpia el borde rojo del descuento. Devuelve el mensaje ("" si es válido)
    const marcarDescuento = fila => {
        const input = fila.querySelector(".av-fila-descuento");
        const mensaje = descuentoMensaje(fila);
        input.classList.toggle("is-invalid", mensaje !== "");
        input.dataset.mensaje = mensaje;
        return mensaje;
    };

    // Deja el mensaje solo si todavía hay algo inválido (la cantidad tiene prioridad)
    const refrescarMensajeProductos = () => {
        if (!contenedorFilas) return;

        if (contenedorFilas.querySelector(".av-fila-cantidad.is-invalid")) {
            msgProductos.textContent = MSG_CANTIDAD;
            return;
        }

        const descuentoInvalido = contenedorFilas.querySelector(".av-fila-descuento.is-invalid");
        msgProductos.textContent = descuentoInvalido
            ? (descuentoInvalido.dataset.mensaje || MSG_DESCUENTO)
            : "";
    };

    // ============ TOTAL DE LA AUTOVENTA (T08) ============
    // Subtotal de una fila en céntimos: (cantidad × precio) − descuento.
    // Sin producto o con cantidad inválida no suma. Un descuento vacío o inválido cuenta como 0.
    const subtotalFilaCentimos = fila => {
        const elegido = productoElegido(fila);
        const cantidadTexto = fila.querySelector(".av-fila-cantidad").value.trim();
        if (!elegido || !/^\d+$/.test(cantidadTexto) || Number(cantidadTexto) < 1) return 0;

        const brutoCentimos = Math.round(elegido.precio * 100) * Number(cantidadTexto);

        const descuentoTexto = fila.querySelector(".av-fila-descuento").value.trim();
        let descuentoCentimos = 0;
        if (descuentoTexto !== "" && /^(\d+|\d*\.\d{1,2})$/.test(descuentoTexto)) {
            descuentoCentimos = Math.round(Number(descuentoTexto) * 100);
        }
        if (descuentoCentimos > brutoCentimos) descuentoCentimos = 0;

        return brutoCentimos - descuentoCentimos;
    };

    const calcularTotalCentimos = () =>
        obtenerFilas().reduce((suma, fila) => suma + subtotalFilaCentimos(fila), 0);

    // Total como número (la T10 lo usará; el servidor lo recalcula y ese valor es el que vale)
    const totalAutoventa = () => calcularTotalCentimos() / 100;

    // Escribe el total en la caja azul (solo lectura)
    const actualizarTotal = () => {
        if (lblTotal) lblTotal.textContent = moneda(totalAutoventa());
    };

    const agregarFila = () => {
        if (!plantillaFila || !contenedorFilas) return;
        contenedorFilas.append(plantillaFila.content.cloneNode(true));

        const select = contenedorFilas.lastElementChild.querySelector(".av-fila-producto");
        llenarSelectProducto(select);
        actualizarTotal(); // T08
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
        actualizarTotal(); // T08
    };

    // Valida todas las filas. La usará T10 antes de guardar.
    const validarProductos = () => {
        let mensaje = "";

        obtenerFilas().forEach(fila => {
            const select = fila.querySelector(".av-fila-producto");
            const cantidad = fila.querySelector(".av-fila-cantidad");

            if (!marcarCantidad(cantidad)) mensaje = MSG_CANTIDAD;
            if (!select.value && !mensaje) mensaje = MSG_PRODUCTO;

            const errorDescuento = marcarDescuento(fila); // T07
            if (errorDescuento && !mensaje) mensaje = errorDescuento;
        });

        msgProductos.textContent = mensaje;
        return mensaje === "";
    };

    // Productos elegidos listos para el guardado (T10): idLocal, idProducto, cantidad y descuento
    const obtenerProductos = () =>
        obtenerFilas()
            .map(fila => {
                const elegido = productoElegido(fila);
                if (!elegido) return null;

                const descuento = Number(fila.querySelector(".av-fila-descuento").value) || 0;

                return {
                    idLocal: elegido.idLocal,
                    idProducto: elegido.idProducto,
                    cantidad: Number(fila.querySelector(".av-fila-cantidad").value),
                    descuentoMonto: Math.round(descuento * 100) / 100 // T07
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
        const fila = e.target.closest(".av-fila");
        if (!fila) return;

        if (e.target.classList.contains("av-fila-cantidad")) {
            marcarCantidad(e.target);
            marcarDescuento(fila); // T07: el límite del descuento depende de la cantidad
            refrescarMensajeProductos();
        } else if (e.target.classList.contains("av-fila-descuento")) {
            marcarDescuento(fila); // T07
            refrescarMensajeProductos();
        }
    });

    contenedorFilas?.addEventListener("input", e => {
        if (e.target.classList.contains("av-fila-cantidad")) {
            e.target.classList.remove("is-invalid");
            refrescarMensajeProductos();
            actualizarTotal(); // T08
        } else if (e.target.classList.contains("av-fila-descuento")) {
            // T07: quitar el error mientras se corrige
            e.target.classList.remove("is-invalid");
            e.target.dataset.mensaje = "";
            refrescarMensajeProductos();
            actualizarTotal(); // T08
        }
    });

    // T07: al cambiar de producto, el límite del descuento cambia
    contenedorFilas?.addEventListener("change", e => {
        if (e.target.classList.contains("av-fila-producto")) {
            marcarDescuento(e.target.closest(".av-fila"));
            refrescarMensajeProductos();
            actualizarTotal(); // T08
        }
    });

    // ============ MÉTODO DE PAGO (T09) ============
    const MSG_METODO_PAGO = "Selecciona un método de pago.";

    // Obligatorio: marca el desplegable en rojo si no hay método elegido. Devuelve true si es válido.
    const validarMetodoPago = () => {
        const valido = selMetodoPago.value !== "";
        setError(selMetodoPago, errMetodoPago, valido ? "" : MSG_METODO_PAGO);
        return valido;
    };

    // Id del método elegido para el guardado (T10). null si no eligió ninguno.
    const obtenerMetodoPago = () => {
        const valor = selMetodoPago.value;
        return valor === "" ? null : Number(valor);
    };

    // Vuelve a "Seleccione un método de pago" y quita el error
    const limpiarMetodoPago = () => {
        selMetodoPago.value = "";
        setError(selMetodoPago, errMetodoPago, "");
    };

    selMetodoPago?.addEventListener("blur", validarMetodoPago);
    selMetodoPago?.addEventListener("change", () => setError(selMetodoPago, errMetodoPago, ""));

    // ============ PRIMERA FILA DE PRODUCTO (T02) ============
    const dibujarPrimeraFila = () => {
        if (!plantillaFila || !contenedorFilas) return;
        contenedorFilas.replaceChildren(plantillaFila.content.cloneNode(true));
        msgProductos.textContent = ""; // T06
        llenarSelectsProducto(); // T05
        actualizarTotal();       // T08: vuelve a S/ 0.00
    };

    modalEl.addEventListener("show.bs.modal", () => {
        limpiarClienteSeleccionado(); // T03: buscador limpio al abrir
        ocultarClienteNuevo();        // T02 + T04: bloque oculto y vacío
        dibujarPrimeraFila();
        limpiarMetodoPago();          // T09: método de pago sin elegir y sin error
        cargarCatalogo();             // T05: productos al día cada vez que se abre
    });
})();