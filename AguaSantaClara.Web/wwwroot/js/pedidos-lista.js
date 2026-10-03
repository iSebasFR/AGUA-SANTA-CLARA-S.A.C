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

	btnEnviar.addEventListener("click", async () => {
		alertaErrores.classList.add("d-none");
		btnEnviar.disabled = true;

		try {
			const resp = await fetch("/Pedidos/EnviarSeleccionados", {
				method: "POST",
				headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
				body: JSON.stringify({
					idsPedidos: marcados().map(c => Number(c.value)),
					idRepartidor: Number(selRepartidor.value) || null
				})
			});
			const datos = await resp.json().catch(() => null);

			if (!resp.ok) {
				mostrarErrores(datos?.errores ?? [{ mensaje: "No se pudo enviar los pedidos." }]);
				actualizar();
				return;
			}

			$("pedidosWhatsapp").href = datos.whatsappUrl;
			$("pedidosWhatsapp").addEventListener("click", () => setTimeout(() => window.location.reload(), 500), { once: true });
			alertaEnviado.classList.remove("d-none");
			selRepartidor.disabled = true;
			todos.disabled = true;
			checks().forEach(c => c.disabled = true);
		} catch {
			mostrarErrores([{ mensaje: "No se pudo conectar con el servidor." }]);
			actualizar();
		}
	});
})();
