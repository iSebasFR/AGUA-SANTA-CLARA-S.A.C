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

    // ============ CLIENTE NUEVO: MOSTRAR / OCULTAR (T02) ============
    const mostrarClienteNuevo = () => {
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
        ocultarClienteNuevo();
        dibujarPrimeraFila();
    });
})();