(() => {
	const form = document.getElementById("pedidosFiltros");
	if (!form) return;

	const grupos = [...form.querySelectorAll("[data-tipo-filtro]")];
	const contador = document.getElementById("pedidosFiltrosContador");
	const activos = () => grupos.filter(grupo =>
		[...grupo.querySelectorAll("input, select")].some(control => control.value.trim() !== ""));

	const actualizar = () => {
		const filtrosActivos = activos();
		contador.textContent = `${filtrosActivos.length} de 3 filtros`;
		for (const grupo of grupos) {
			const deshabilitar = filtrosActivos.length >= 3 && !filtrosActivos.includes(grupo);
			grupo.querySelectorAll("input, select").forEach(control => control.disabled = deshabilitar);
		}
	};

	grupos.forEach(grupo => grupo.querySelectorAll("input, select").forEach(control => {
		control.addEventListener("input", actualizar);
		control.addEventListener("change", actualizar);
	}));

	form.addEventListener("submit", evento => {
		if (activos().length > 3) evento.preventDefault();
	});

	actualizar();
})();
