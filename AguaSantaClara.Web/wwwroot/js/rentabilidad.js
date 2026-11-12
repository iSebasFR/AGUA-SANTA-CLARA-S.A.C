document.addEventListener('DOMContentLoaded', () => {
    const inputFechaDesde = document.getElementById('inputFechaDesde');
    const inputFechaHasta = document.getElementById('inputFechaHasta');
    const tarjetas = document.getElementById('tarjetasLineas');
    const detalle = document.getElementById('detalleLineas');
    const alertaOtros = document.getElementById('alertaOtros');
    const alertaError = document.getElementById('alertaError');
    const canvas = document.getElementById('graficoMargen');
    let grafico = null;
    let solicitudActual = 0;

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text ?? '';
        return div.innerHTML;
    }

    function moneda(monto) {
        return 'S/ ' + Number(monto).toLocaleString('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function porcentaje(valor) {
        return valor === null || valor === undefined
            ? '—'
            : Number(valor).toLocaleString('es-PE', { minimumFractionDigits: 1, maximumFractionDigits: 1 }) + '%';
    }

    function claseMargen(negativo) {
        return negativo ? 'text-danger' : 'text-success';
    }

    function renderTarjetas(lineas) {
        tarjetas.innerHTML = lineas.map(l => `
            <div class="col-12 col-sm-6 col-lg-3">
                <div class="card kpi-card h-100 ${l.margenNegativo ? 'margen-negativo' : ''}">
                    <div class="card-body p-3">
                        <div class="kpi-label">${escapeHtml(l.nombre.toUpperCase())}</div>
                        <div class="kpi-value ${claseMargen(l.margenNegativo)}">${moneda(l.margen)}</div>
                        <div class="kpi-detalle mt-2">
                            <div>Ingresos netos: <strong>${moneda(l.ingresoNeto)}</strong></div>
                            <div>% margen: <strong class="${claseMargen(l.margenNegativo)}">${porcentaje(l.porcentajeMargen)}</strong></div>
                        </div>
                    </div>
                </div>
            </div>`).join('');
    }

    function renderFilaProducto(p) {
        const negativo = p.margenNegativo;
        return `
            <tr class="${negativo ? 'table-danger' : ''}">
                <td class="fw-medium">${negativo ? '<i class="bi bi-exclamation-triangle-fill text-danger me-1" title="Margen negativo"></i>' : ''}${escapeHtml(p.nombre)}</td>
                <td class="text-end">${p.cantidad}</td>
                <td class="text-end">${moneda(p.precioVenta)}</td>
                <td class="text-end">${moneda(p.costoUnitario)}</td>
                <td class="text-end">${moneda(p.ingresoBruto)}</td>
                <td class="text-end">${moneda(p.descuentos)}</td>
                <td class="text-end">${moneda(p.ingresoNeto)}</td>
                <td class="text-end">${moneda(p.costo)}</td>
                <td class="text-end fw-semibold ${claseMargen(negativo)}">${moneda(p.margen)}</td>
                <td class="text-end fw-semibold ${claseMargen(negativo)}">${porcentaje(p.porcentajeMargen)}</td>
            </tr>`;
    }

    function renderDetalle(lineas) {
        detalle.innerHTML = lineas.map(l => `
            <div class="reporte-section-card mb-4">
                <div class="reporte-section-header">
                    <h2 class="section-heading-title">${escapeHtml(l.nombre.toUpperCase())}</h2>
                    <span class="section-count-badge">${l.productos.length}</span>
                </div>
                <div class="table-responsive">
                    <table class="table reporte-table">
                        <thead>
                            <tr>
                                <th>PRODUCTO</th>
                                <th class="text-end">CANT.</th>
                                <th class="text-end">PRECIO VENTA</th>
                                <th class="text-end">COSTO UNIT.</th>
                                <th class="text-end">INGRESO BRUTO</th>
                                <th class="text-end">DESCUENTOS</th>
                                <th class="text-end">INGRESO NETO</th>
                                <th class="text-end">COSTO</th>
                                <th class="text-end">MARGEN</th>
                                <th class="text-end">% MARGEN</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${l.productos.length > 0
                                ? l.productos.map(renderFilaProducto).join('')
                                : `<tr><td colspan="10" class="text-center py-4 text-muted">
                                       <i class="bi bi-inbox d-block mb-1" style="font-size: 1.5rem;"></i>
                                       Sin ventas de esta línea en el período seleccionado.
                                   </td></tr>`}
                        </tbody>
                    </table>
                </div>
            </div>`).join('');
    }

    function renderAlertaOtros(categorias) {
        if (categorias.length === 0) {
            alertaOtros.classList.add('d-none');
            return;
        }
        alertaOtros.textContent = `Hay productos con categorías fuera de las líneas definidas (${categorias.join(', ')}). Se agrupan en "Otros".`;
        alertaOtros.classList.remove('d-none');
    }

    function renderGrafico(lineas) {
        if (typeof Chart === 'undefined') return;

        const etiquetas = lineas.map(l => l.nombre);
        const margenes = lineas.map(l => Number(l.margen));
        const colores = lineas.map(l => l.margenNegativo ? '#dc3545' : '#198754');

        if (grafico) {
            grafico.data.labels = etiquetas;
            grafico.data.datasets[0].data = margenes;
            grafico.data.datasets[0].backgroundColor = colores;
            grafico.update();
            return;
        }

        grafico = new Chart(canvas, {
            type: 'bar',
            data: {
                labels: etiquetas,
                datasets: [{ label: 'Margen (S/)', data: margenes, backgroundColor: colores, borderRadius: 6 }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: { callbacks: { label: ctx => moneda(ctx.parsed.y) } }
                },
                scales: { y: { beginAtZero: true, ticks: { callback: valor => moneda(valor) } } }
            }
        });
    }

    function render(reporte) {
        renderTarjetas(reporte.lineas);
        renderDetalle(reporte.lineas);
        renderAlertaOtros(reporte.categoriasSinLinea);
        renderGrafico(reporte.lineas);
    }

    async function cargar() {
        const desde = inputFechaDesde.value;
        const hasta = inputFechaHasta.value;
        if (!desde || !hasta) return;

        const numeroSolicitud = ++solicitudActual;
        const url = new URL(document.getElementById('rentabilidad').dataset.url, window.location.origin);
        url.searchParams.set('fechaDesde', desde);
        url.searchParams.set('fechaHasta', hasta);

        try {
            const respuesta = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!respuesta.ok) throw new Error(`HTTP ${respuesta.status}`);
            const resultado = await respuesta.json();
            if (numeroSolicitud !== solicitudActual || !resultado.success) return;

            const reporte = resultado.reporte;
            inputFechaDesde.value = reporte.fechaDesde;
            inputFechaHasta.value = reporte.fechaHasta;
            window.history.replaceState({}, '', `${url.pathname}?fechaDesde=${reporte.fechaDesde}&fechaHasta=${reporte.fechaHasta}`);
            alertaError.classList.add('d-none');
            render(reporte);
        } catch (error) {
            console.error('Error al cargar la rentabilidad', error);
            alertaError.classList.remove('d-none');
        }
    }

    inputFechaDesde.addEventListener('change', cargar);
    inputFechaHasta.addEventListener('change', cargar);

    render(JSON.parse(document.getElementById('datosRentabilidad').textContent));
});
