(() => {
	const modalEl = document.getElementById("pedidoModal");
	if (!modalEl) return;

	const $ = id => document.getElementById(id);
	const selLocal = $("pedidoLocal");
	const selBuscarTipo = $("pedidoBuscarTipo");
	const inpBuscar = $("pedidoBuscar");
	const msgBuscar = $("pedidoBuscarMensaje");
	const contenedor = $("pedidoClientes");
	const selRepartidor = $("pedidoRepartidor");
	const lblTotal = $("pedidoTotal");
	const alertaErrores = $("pedidoErrores");
	const alertaEnviado = $("pedidoEnviado");
	const formulario = $("pedidoFormulario");
	const btnGuardar = $("pedidoGuardarBtn");
	const btnEnviar = $("pedidoEnviarBtn");
	const token = modalEl.querySelector("input[name='__RequestVerificationToken']")?.value ?? "";

	let productos = [];
	let bloques = [];
	let pedidoRegistrado = false;
	let pedidoAEditar = null;
	let detallesPendientes = null;

	const moneda = valor => "S/ " + valor.toFixed(2);

	const h = (tag, atributos = {}, ...hijos) => {
		const nodo = document.createElement(tag);
		for (const [clave, valor] of Object.entries(atributos)) {
			if (clave === "class") nodo.className = valor;
			else if (clave.startsWith("on")) nodo.addEventListener(clave.slice(2), valor);
			else nodo.setAttribute(clave, valor);
		}
		for (const hijo of hijos) nodo.append(hijo);
		return nodo;
	};

	const textoDireccion = d => d.ciudad ? `${d.direccion}, ${d.ciudad}` : d.direccion;

	const limpiarErrores = () => {
		alertaErrores.classList.add("d-none");
		alertaErrores.replaceChildren();
		modalEl.querySelectorAll(".is-invalid").forEach(nodo => nodo.classList.remove("is-invalid"));
		msgBuscar.textContent = "";
	};

	const mostrarErrores = errores => {
		alertaErrores.replaceChildren(h("ul", { class: "mb-0" }, ...errores.map(e => h("li", {}, e.mensaje))));
		alertaErrores.classList.remove("d-none");

		for (const error of errores) {
			if (error.campo.startsWith("stock:")) {
				const id = error.campo.slice(6);
				modalEl.querySelectorAll(`[data-producto-id='${id}']`).forEach(nodo => nodo.classList.add("is-invalid"));
			} else {
				modalEl.querySelectorAll(`[data-campo='${CSS.escape(error.campo)}']`).forEach(nodo => nodo.classList.add("is-invalid"));
			}
		}
	};

	const actualizarTotales = () => {
		let total = 0;
		for (const bloque of bloques) {
			let subtotal = 0;
			for (const fila of bloque.filas) {
				const producto = productos.find(p => p.idProducto === Number(fila.select.value));
				const cantidad = Number(fila.cantidad.value);
				if (producto && cantidad > 0) subtotal += producto.precio * cantidad;
			}
			bloque.lblSubtotal.textContent = moneda(subtotal);
			total += subtotal;
		}
		lblTotal.textContent = moneda(total);
	};

	const direccionesUsadas = cliente => bloques
		.filter(b => b.cliente.id === cliente.id)
		.map(b => Number(b.selectDireccion.value));

	const actualizarBotonesDireccion = () => {
		for (const bloque of bloques) {
			if (!bloque.btnOtraDireccion) continue;
			bloque.btnOtraDireccion.disabled = direccionesUsadas(bloque.cliente).length >= bloque.cliente.direcciones.length;
		}
	};

	const opcionesProducto = () => [
		h("option", { value: "" }, "Selecciona un producto"),
		...productos.map(p => h("option", { value: p.idProducto }, `${p.nombre} - ${moneda(p.precio)} (stock: ${p.stock})`))
	];

	const agregarFila = bloque => {
		const select = h("select", { class: "form-select" }, ...opcionesProducto());
		const cantidad = h("input", { type: "number", min: "1", step: "1", class: "form-control", value: "1", "aria-label": "Cantidad" });
		const lblSubtotal = h("span", { class: "text-nowrap" }, moneda(0));
		const fila = { select, cantidad };
		const marcarProducto = () => {
			const id = select.value;
			[select, cantidad].forEach(nodo => {
				if (id) nodo.setAttribute("data-producto-id", id); else nodo.removeAttribute("data-producto-id");
			});
		};
		const refrescar = () => {
			marcarProducto();
			const producto = productos.find(p => p.idProducto === Number(select.value));
			const cant = Number(cantidad.value);
			lblSubtotal.textContent = moneda(producto && cant > 0 ? producto.precio * cant : 0);
			actualizarTotales();
		};
		select.addEventListener("change", refrescar);
		cantidad.addEventListener("input", refrescar);

		const nodo = h("div", { class: "row g-2 align-items-center mb-2" },
			h("div", { class: "col-md-6" }, select),
			h("div", { class: "col-4 col-md-2" }, cantidad),
			h("div", { class: "col-4 col-md-2 text-md-end" }, lblSubtotal),
			h("div", { class: "col-4 col-md-2 text-end" },
				h("button", {
					type: "button", class: "btn btn-sm btn-outline-danger", "aria-label": "Quitar producto",
					onclick: () => {
						bloque.filas = bloque.filas.filter(f => f !== fila);
						nodo.remove();
						actualizarTotales();
					}
				}, h("i", { class: "bi bi-trash" }))));

		fila.nodo = nodo;
		bloque.filas.push(fila);
		bloque.contenedorFilas.append(nodo);
		actualizarTotales();
	};

	const reiniciarFilas = () => {
		for (const bloque of bloques) {
			bloque.filas = [];
			bloque.contenedorFilas.replaceChildren();
			agregarFila(bloque);
		}
		actualizarTotales();
	};

	const actualizarOpcionesProductos = () => {
		for (const bloque of bloques) {
			for (const fila of bloque.filas) {
				const valorActual = fila.select.value;
				fila.select.replaceChildren(...opcionesProducto());
				if (valorActual && fila.select.querySelector(`option[value='${valorActual}']`))
					fila.select.value = valorActual;
			}
		}
	};

	const agregarBloque = (cliente, idDireccion, detalles = null) => {
		const bloque = { cliente, filas: [] };
		const varias = cliente.direcciones.length > 1;

		bloque.selectDireccion = h("select", { class: "form-select", "aria-label": "Dirección de entrega" },
			...cliente.direcciones.map(d => h("option", { value: d.id }, textoDireccion(d))));
		bloque.selectDireccion.value = String(idDireccion);
		bloque.selectDireccion.addEventListener("change", actualizarBotonesDireccion);
		if (!varias) bloque.selectDireccion.disabled = true;

		bloque.contenedorFilas = h("div");
		bloque.lblSubtotal = h("strong", {}, moneda(0));

		if (varias) {
			bloque.btnOtraDireccion = h("button", {
				type: "button", class: "btn btn-sm btn-outline-primary",
				onclick: () => {
					const usadas = direccionesUsadas(cliente);
					const libre = cliente.direcciones.find(d => !usadas.includes(d.id));
					if (libre) agregarBloque(cliente, libre.id);
				}
			}, "Agregar otra dirección");
		}

		bloque.root = h("div", { class: "card" },
			h("div", { class: "card-header d-flex justify-content-between align-items-center gap-2" },
				h("div", {},
					h("strong", {}, cliente.nombre),
					h("span", { class: "text-muted small ms-2" }, [cliente.telefono, cliente.dni ? `DNI ${cliente.dni}` : null].filter(Boolean).join(" · "))),
				h("button", {
					type: "button", class: "btn btn-sm btn-outline-danger",
					onclick: () => {
						bloques = bloques.filter(b => b !== bloque);
						bloque.root.remove();
						actualizarBotonesDireccion();
						actualizarTotales();
					}
				}, "Quitar")),
			h("div", { class: "card-body" },
				h("div", { class: "row g-2 mb-3 align-items-center" },
					h("div", { class: "col-md-9" }, bloque.selectDireccion),
					h("div", { class: "col-md-3 text-md-end" }, bloque.btnOtraDireccion ?? "")),
				bloque.contenedorFilas,
				h("div", { class: "d-flex justify-content-between align-items-center mt-2" },
					h("button", { type: "button", class: "btn btn-sm btn-outline-secondary", onclick: () => agregarFila(bloque) }, "Agregar producto"),
					h("span", {}, "Subtotal: ", bloque.lblSubtotal))));

		bloques.push(bloque);
		contenedor.append(bloque.root);
		agregarFila(bloque);
		if (detalles?.length) {
			for (let i = 1; i < detalles.length; i++) agregarFila(bloque);
			detalles.forEach((detalle, i) => {
				const fila = bloque.filas[i];
				fila.select.value = String(detalle.idProducto);
				fila.cantidad.value = String(detalle.cantidad);
				fila.select.dispatchEvent(new Event("change"));
				fila.cantidad.dispatchEvent(new Event("input"));
			});
		}
		actualizarBotonesDireccion();
	};

	const configurarInputBusqueda = () => {
		const tipo = selBuscarTipo.value;
		if (tipo === "telefono") {
			inpBuscar.placeholder = "987654321";
			inpBuscar.setAttribute("inputmode", "numeric");
			inpBuscar.setAttribute("maxlength", "9");
			inpBuscar.value = inpBuscar.value.replace(/\D/g, "").slice(0, 9);
		} else if (tipo === "dni") {
			inpBuscar.placeholder = "12345678";
			inpBuscar.setAttribute("inputmode", "numeric");
			inpBuscar.setAttribute("maxlength", "8");
			inpBuscar.value = inpBuscar.value.replace(/\D/g, "").slice(0, 8);
		} else {
			inpBuscar.placeholder = "Ana Torres";
			inpBuscar.removeAttribute("inputmode");
			inpBuscar.setAttribute("maxlength", "100");
		}
		msgBuscar.textContent = "";
		inpBuscar.classList.remove("is-invalid");
	};

	const buscarCliente = async () => {
		msgBuscar.textContent = "";
		inpBuscar.classList.remove("is-invalid");
		const tipo = selBuscarTipo.value;
		const termino = inpBuscar.value.trim();

		if (!termino) {
			inpBuscar.classList.add("is-invalid");
			msgBuscar.textContent = "Ingresa un valor para buscar.";
			return;
		}

		if (tipo === "telefono" && termino.length < 6) {
			inpBuscar.classList.add("is-invalid");
			msgBuscar.textContent = "Ingresa al menos 6 dígitos del teléfono.";
			return;
		}
		if (tipo === "dni" && termino.length < 4) {
			inpBuscar.classList.add("is-invalid");
			msgBuscar.textContent = "Ingresa al menos 4 dígitos del DNI.";
			return;
		}
		if (tipo === "nombre" && termino.length < 2) {
			inpBuscar.classList.add("is-invalid");
			msgBuscar.textContent = "Ingresa al menos 2 letras del nombre.";
			return;
		}

		const resp = await fetch(`/Pedidos/BuscarCliente?tipo=${encodeURIComponent(tipo)}&q=${encodeURIComponent(termino)}`);
		if (!resp.ok) {
			inpBuscar.classList.add("is-invalid");
			msgBuscar.textContent = "No se encontró un cliente con esos criterios.";
			return;
		}

		const cliente = await resp.json();
		if (!cliente.direcciones.length) {
			inpBuscar.classList.add("is-invalid");
			msgBuscar.textContent = "El cliente no tiene direcciones registradas.";
			return;
		}

		const usadas = direccionesUsadas(cliente);
		const libre = cliente.direcciones.find(d => !usadas.includes(d.id));
		if (!libre) {
			inpBuscar.classList.add("is-invalid");
			msgBuscar.textContent = "El cliente ya está en el pedido con todas sus direcciones.";
			return;
		}

		agregarBloque(cliente, libre.id);
		inpBuscar.value = "";
	};

	const cargarProductos = async (reiniciar = false) => {
		productos = [];
		if (selLocal.value) {
			const resp = await fetch(`/Pedidos/ProductosPorLocal?idLocal=${encodeURIComponent(selLocal.value)}`);
			if (resp.ok) productos = await resp.json();
		}
		if (reiniciar) {
			reiniciarFilas();
		} else {
			actualizarOpcionesProductos();
			actualizarTotales();
		}
	};

	const cargarPedidoParaEditar = async () => {
		try {
			const respuesta = await fetch(`/Pedidos/ObtenerParaEditar?idPedido=${pedidoAEditar}`);
			if (!respuesta.ok) throw new Error();
			const pedido = await respuesta.json();

			$("pedidoModalLabel").textContent = `Editar pedido N° ${pedido.idPedido}`;
			btnEnviar.classList.add("d-none");
			selRepartidor.value = pedido.idRepartidor == null ? "" : String(pedido.idRepartidor);

			if (pedido.idLocal == null) {
				detallesPendientes = pedido.clientes;
				selLocal.value = "";
				productos = [];
				bloques = [];
				contenedor.replaceChildren();
				lblTotal.textContent = moneda(0);
				mostrarErrores([{
					campo: "idLocal",
					mensaje: "Este pedido no tiene Local asignado. Selecciona uno para editar los productos."
				}]);
				return;
			}

			detallesPendientes = null;
			selLocal.value = String(pedido.idLocal);
			await cargarProductos(true);
			for (const linea of pedido.clientes)
				agregarBloque(linea.cliente, linea.idDireccion, linea.detalles);
			actualizarTotales();
		} catch {
			mostrarErrores([{ campo: "", mensaje: "No se pudo cargar el pedido para editar." }]);
		}
	};

	const serializar = () => {
		const clientes = bloques.map((bloque, i) => {
			bloque.selectDireccion.setAttribute("data-campo", `clientes[${i}].idDireccion`);
			bloque.contenedorFilas.setAttribute("data-campo", `clientes[${i}].detalles`);
			return {
				idCliente: bloque.cliente.id,
				idDireccion: Number(bloque.selectDireccion.value),
				detalles: bloque.filas.map((fila, j) => {
					fila.select.setAttribute("data-campo", `clientes[${i}].detalles[${j}].idProducto`);
					fila.cantidad.setAttribute("data-campo", `clientes[${i}].detalles[${j}].cantidad`);
					return { idProducto: Number(fila.select.value) || 0, cantidad: Number(fila.cantidad.value) || 0 };
				})
			};
		});

		return {
			idLocal: Number(selLocal.value) || null,
			idRepartidor: Number(selRepartidor.value) || null,
			clientes
		};
	};

	const registrar = async (url, esEnvio) => {
		limpiarErrores();
		const cuerpo = serializar();
		btnGuardar.disabled = btnEnviar.disabled = true;

		try {
			const resp = await fetch(url, {
				method: "POST",
				headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
				body: JSON.stringify(cuerpo)
			});
			const datos = await resp.json().catch(() => null);

			if (!resp.ok) {
				mostrarErrores(datos?.errores ?? [{ campo: "", mensaje: "No se pudo registrar el pedido." }]);
				btnGuardar.disabled = btnEnviar.disabled = false;
				return;
			}

			pedidoRegistrado = true;
			if (!esEnvio) {
				bootstrap.Modal.getInstance(modalEl).hide();
				return;
			}

			$("pedidoEnviadoNumero").textContent = `N° ${datos.idPedido}`;
			$("pedidoWhatsapp").href = datos.whatsappUrl;
			alertaEnviado.classList.remove("d-none");
			formulario.classList.add("d-none");
		} catch {
			mostrarErrores([{ campo: "", mensaje: "No se pudo conectar con el servidor." }]);
			btnGuardar.disabled = btnEnviar.disabled = false;
		}
	};

	const previewYEnviar = async () => {
		limpiarErrores();
		const cuerpo = serializar();
		btnEnviar.disabled = true;

		try {
			const resp = await fetch("/Pedidos/PreviewEnviar", {
				method: "POST",
				headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
				body: JSON.stringify(cuerpo)
			});
			const datos = await resp.json().catch(() => null);

			if (!resp.ok) {
				mostrarErrores(datos?.errores ?? [{ campo: "", mensaje: "No se pudo validar el pedido." }]);
				btnEnviar.disabled = false;
				return;
			}

			const previewModalEl = document.getElementById("previewEnvioModal");
			document.getElementById("previewEnvioMensaje").textContent = datos.mensaje ?? "";
			document.getElementById("previewEnvioDestinatario").textContent =
				selRepartidor.options[selRepartidor.selectedIndex]?.text ?? "Repartidor";

			const confirmBtn = document.getElementById("previewEnvioConfirmar");
			const nuevoBtn = confirmBtn.cloneNode(true);
			confirmBtn.parentNode.replaceChild(nuevoBtn, confirmBtn);

			nuevoBtn.addEventListener("click", async () => {
				nuevoBtn.disabled = true;
				try {
					const resp2 = await fetch("/Pedidos/Enviar", {
						method: "POST",
						headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
						body: JSON.stringify(cuerpo)
					});
					const datos2 = await resp2.json().catch(() => null);
					if (!resp2.ok) {
						mostrarErrores(datos2?.errores ?? [{ campo: "", mensaje: "No se pudo guardar el pedido." }]);
						nuevoBtn.disabled = false;
						return;
					}

					pedidoRegistrado = true;
					const previewModal = bootstrap.Modal.getInstance(previewModalEl);
					if (previewModal) previewModal.hide();
					bootstrap.Modal.getInstance(modalEl).hide();
					window.open(datos2.whatsappUrl, "_blank");
				} catch {
					mostrarErrores([{ campo: "", mensaje: "No se pudo conectar con el servidor." }]);
					nuevoBtn.disabled = false;
				}
			});

			new bootstrap.Modal(previewModalEl).show();
			btnEnviar.disabled = false;
		} catch {
			mostrarErrores([{ campo: "", mensaje: "No se pudo conectar con el servidor." }]);
			btnEnviar.disabled = false;
		}
	};

	const reiniciar = () => {
		limpiarErrores();
		alertaEnviado.classList.add("d-none");
		formulario.classList.remove("d-none");
		selLocal.value = "";
		selBuscarTipo.value = "telefono";
		inpBuscar.value = "";
		selRepartidor.value = "";
		productos = [];
		bloques = [];
		contenedor.replaceChildren();
		lblTotal.textContent = moneda(0);
		btnGuardar.disabled = btnEnviar.disabled = false;
		$("pedidoModalLabel").textContent = "Crear pedido";
		btnEnviar.classList.remove("d-none");
		pedidoAEditar = null;
		detallesPendientes = null;
		configurarInputBusqueda();
	};

	selLocal.addEventListener("change", async () => {
		if (pedidoAEditar === null) {
			await cargarProductos(true);
		} else {
			await cargarProductos(bloques.length === 0);
			if (detallesPendientes && productos.length > 0) {
				for (const linea of detallesPendientes)
					agregarBloque(linea.cliente, linea.idDireccion, linea.detalles);
				detallesPendientes = null;
				limpiarErrores();
			}
			actualizarTotales();
		}
	});

	selBuscarTipo.addEventListener("change", configurarInputBusqueda);

	inpBuscar.addEventListener("input", () => {
		if (selBuscarTipo.value === "telefono") inpBuscar.value = inpBuscar.value.replace(/\D/g, "").slice(0, 9);
		else if (selBuscarTipo.value === "dni") inpBuscar.value = inpBuscar.value.replace(/\D/g, "").slice(0, 8);
	});

	// ✅ Cambio clave: leer el botón que disparó el modal
	modalEl.addEventListener("show.bs.modal", async (evento) => {
		const trigger = evento.relatedTarget;
		if (trigger && trigger.classList?.contains("pedido-editar")) {
			pedidoAEditar = trigger.dataset.pedidoId;
		} else {
			pedidoAEditar = null;
		}

		configurarInputBusqueda();
		if (pedidoAEditar === null) {
			productos = [];
			reiniciarFilas();
		} else {
			await cargarPedidoParaEditar();
		}
	});

	$("pedidoBuscarBtn").addEventListener("click", buscarCliente);
	inpBuscar.addEventListener("keydown", evento => {
		if (evento.key === "Enter") {
			evento.preventDefault();
			buscarCliente();
		}
	});

	btnGuardar.addEventListener("click", () => registrar(
		pedidoAEditar === null ? "/Pedidos/Guardar" : `/Pedidos/Actualizar?idPedido=${pedidoAEditar}`,
		false));
	btnEnviar.addEventListener("click", previewYEnviar);

	modalEl.addEventListener("hidden.bs.modal", () => {
		if (pedidoRegistrado) {
			window.location.reload();
			return;
		}
		reiniciar();
	});
})();