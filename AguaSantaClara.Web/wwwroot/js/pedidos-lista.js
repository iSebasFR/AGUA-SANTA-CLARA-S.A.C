(() => {
	const panel = document.getElementById("pedidosAcciones");
	if (!panel) return;

	const $ = id => document.getElementById(id);
	const checks = () => [...document.querySelectorAll(".pedido-check:not(:disabled)")];
	const marcados = () => checks().filter(c => c.checked);
	const todos = $("pedidosSeleccionarTodos");
	const btnEnviar = $("pedidosEnviarBtn");
	const selRepartidor = $("pedidosRepartidor");
	const alertaErrores = $("pedidosErrores");
	const alertaEstado = $("pedidoEstadoActualizado");
	const alertaEliminado = $("pedidosEliminado");
	const alertaEnviado = $("pedidosEnviado");
	const token = panel.querySelector("input[name='__RequestVerificationToken']")?.value ?? "";

	const actualizar = () => {
		const n = marcados().length;
		$("pedidosSeleccionados").textContent = n;
		btnEnviar.disabled = n === 0;
		todos.checked = n > 0 && n === checks().length;
	};

	const mostrarErrores = errores => {
		const lista = document.createElement("ul");
		lista.className = "mb-0";
		for (const e of errores) {
			const li = document.createElement("li");
			li.textContent = e.mensaje;
			lista.append(li);
		}
		alertaErrores.replaceChildren(lista);
		alertaErrores.classList.remove("d-none");
	};

	todos.addEventListener("change", () => {
		checks().forEach(c => c.checked = todos.checked);
		actualizar();
	});
	document.addEventListener("change", e => {
		if (e.target.classList?.contains("pedido-check")) actualizar();
	});
	document.querySelectorAll(".pedido-estado").forEach(select => select.addEventListener("change", async () => {
		const estadoAnterior = select.dataset.estadoOriginal;
		alertaErrores.classList.add("d-none");
		alertaEstado.classList.add("d-none");
		select.disabled = true;

		try {
			const resp = await fetch(`/Pedidos/CambiarEstado?idPedido=${encodeURIComponent(select.dataset.pedidoId)}`, {
				method: "POST",
				headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
				body: JSON.stringify({ estado: select.value })
			});
			const datos = await resp.json().catch(() => null);
			if (!resp.ok) {
				mostrarErrores(datos?.errores ?? [{ mensaje: "No se pudo actualizar el estado del pedido." }]);
				select.value = estadoAnterior;
				return;
			}

			select.value = datos.estado;
			select.dataset.estadoOriginal = datos.estado;
			const checkPedido = select.closest("tr").querySelector(".pedido-check");
			if (checkPedido) {
				checkPedido.disabled = datos.estado !== "Pendiente";
				if (checkPedido.disabled) checkPedido.checked = false;
			}
			actualizar();
			alertaEstado.textContent = `Pedido N° ${select.dataset.pedidoId}: estado actualizado a ${datos.estado}.`;
			alertaEstado.classList.remove("d-none");
		} catch {
			select.value = estadoAnterior;
			mostrarErrores([{ mensaje: "No se pudo conectar con el servidor." }]);
		} finally {
			select.disabled = false;
		}
	}));

	document.querySelectorAll(".pedido-eliminar").forEach(btn => btn.addEventListener("click", async () => {
		if (!window.confirm("¿Está seguro de borrar este pedido?")) return;

		alertaErrores.classList.add("d-none");
		alertaEliminado.classList.add("d-none");
		btn.disabled = true;
		try {
			const resp = await fetch(`/Pedidos/Eliminar?idPedido=${encodeURIComponent(btn.dataset.pedidoId)}`, {
				method: "POST",
				headers: { "RequestVerificationToken": token }
			});
			if (!resp.ok) {
				mostrarErrores([{ mensaje: resp.status === 404 ? "El pedido ya no existe." : "No se pudo borrar el pedido." }]);
				btn.disabled = false;
				return;
			}
			btn.closest("tr").remove();
			alertaEliminado.textContent = "Pedido eliminado correctamente.";
			alertaEliminado.classList.remove("d-none");
			actualizar();
			if (!document.querySelector("tbody tr")) {
				const filaVacia = document.createElement("tr");
				const celdaVacia = document.createElement("td");
				celdaVacia.colSpan = 9;
				celdaVacia.className = "text-center text-muted py-4";
				celdaVacia.textContent = "No hay pedidos registrados.";
				filaVacia.append(celdaVacia);
				document.querySelector("tbody").append(filaVacia);
			}
		} catch {
			mostrarErrores([{ mensaje: "No se pudo conectar con el servidor." }]);
			btn.disabled = false;
		}
	}));

	btnEnviar.addEventListener("click", async () => {
		alertaErrores.classList.add("d-none");
		const seleccionados = marcados();
		if (!selRepartidor.value) {
			mostrarErrores([{ mensaje: "Selecciona un repartidor." }]);
			return;
		}

		const ventanaWhatsapp = window.open("about:blank", "_blank");
		const cerrarVentanaWhatsapp = () => {
			if (ventanaWhatsapp && !ventanaWhatsapp.closed) ventanaWhatsapp.close();
		};
		btnEnviar.disabled = true;

		try {
			const resp = await fetch("/Pedidos/EnviarSeleccionados", {
				method: "POST",
				headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
				body: JSON.stringify({
					idsPedidos: seleccionados.map(c => Number(c.value)),
					idRepartidor: Number(selRepartidor.value) || null
				})
			});
			const datos = await resp.json().catch(() => null);

			if (!resp.ok) {
				cerrarVentanaWhatsapp();
				mostrarErrores(datos?.errores ?? [{ mensaje: "No se pudo enviar los pedidos." }]);
				actualizar();
				return;
			}

			$("pedidosWhatsapp").href = datos.whatsappUrl;
			$("pedidosWhatsapp").onclick = () => setTimeout(() => window.location.reload(), 500);
			if (ventanaWhatsapp) ventanaWhatsapp.location.href = datos.whatsappUrl;
			for (const check of seleccionados) {
				check.checked = false;
				check.disabled = true;
				const estado = check.closest("tr").querySelector(".pedido-estado");
				if (estado) {
					estado.value = "Enviado";
					estado.dataset.estadoOriginal = "Enviado";
				}
			}
			alertaEnviado.classList.remove("d-none");
			selRepartidor.value = "";
			actualizar();
		} catch {
			cerrarVentanaWhatsapp();
			mostrarErrores([{ mensaje: "No se pudo conectar con el servidor." }]);
			actualizar();
		}
	});
})();
