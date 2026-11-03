document.addEventListener('DOMContentLoaded', () => {
    const inputFechaDesde = document.getElementById('inputFechaDesde');
    const inputFechaHasta = document.getElementById('inputFechaHasta');
    const pills = document.querySelectorAll('.reporte-pills .pill-btn');
    const btnExportarExcel = document.getElementById('btnExportarExcel');
    const optExportarExcel = document.getElementById('optExportarExcel');
    const optExportarCsv = document.getElementById('optExportarCsv');

    let filtroActual = document.querySelector('.reporte-pills .pill-btn.active')?.dataset.filtro || 'hoy';

    function formatearFechaIso(date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    }

    function formatearMoneda(monto) {
        return 'S/ ' + Number(monto).toLocaleString('es-PE', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    function obtenerBadgeEstado(estado) {
        let badgeClass = 'badge-estado-pendiente';
        switch (estado?.toLowerCase()) {
            case 'pagado':
                badgeClass = 'badge-estado-pagado';
                break;
            case 'pago parcial':
                badgeClass = 'badge-estado-parcial';
                break;
            case 'pendiente':
                badgeClass = 'badge-estado-pendiente';
                break;
            case 'enviado':
                badgeClass = 'badge-estado-enviado';
                break;
            case 'entregado':
                badgeClass = 'badge-estado-entregado';
                break;
            case 'con incidencia':
                badgeClass = 'badge-estado-incidencia';
                break;
        }
        return `<span class="badge-estado ${badgeClass}">${escapeHtml(estado || '')}</span>`;
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    async function cargarReporte(filtro, desde, hasta) {
        try {
            const url = new URL('/Reportes/Ventas', window.location.origin);
            if (filtro) url.searchParams.set('filtro', filtro);
            if (desde) url.searchParams.set('fechaDesde', desde);
            if (hasta) url.searchParams.set('fechaHasta', hasta);
            url.searchParams.set('format', 'json');

            // Actualizar URL del navegador sin recargar
            const urlVisible = new URL(window.location.href);
            if (filtro) urlVisible.searchParams.set('filtro', filtro);
            if (desde) urlVisible.searchParams.set('fechaDesde', desde);
            if (hasta) urlVisible.searchParams.set('fechaHasta', hasta);
            window.history.replaceState({}, '', urlVisible);

            const respuesta = await fetch(url.toString(), {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (!respuesta.ok) throw new Error('Error al consultar datos');

            const resultado = await respuesta.json();
            if (!resultado.success) return;

            // Actualizar inputs si fueron recalculados por el backend
            if (resultado.fechaDesde && inputFechaDesde.value !== resultado.fechaDesde) {
                inputFechaDesde.value = resultado.fechaDesde;
            }
            if (resultado.fechaHasta && inputFechaHasta.value !== resultado.fechaHasta) {
                inputFechaHasta.value = resultado.fechaHasta;
            }

            // Actualizar KPIs superiores
            document.getElementById('kpiTotalVentas').textContent = formatearMoneda(resultado.totalVentas);
            document.getElementById('kpiTotalPagos').textContent = formatearMoneda(resultado.totalPagos);
            document.getElementById('kpiTotalDeudas').textContent = formatearMoneda(resultado.totalDeudas);
            document.getElementById('kpiCantidadPedidos').textContent = resultado.cantidadPedidos;

            // Actualizar Totales del Footer / Parte inferior
            const footerVentas = document.getElementById('footerTotalVentas');
            const footerPagos = document.getElementById('footerTotalPagos');
            const footerDeudas = document.getElementById('footerTotalDeudas');
            if (footerVentas) footerVentas.textContent = formatearMoneda(resultado.totalVentas);
            if (footerPagos) footerPagos.textContent = formatearMoneda(resultado.totalPagos);
            if (footerDeudas) footerDeudas.textContent = formatearMoneda(resultado.totalDeudas);

            // Actualizar Badges de conteo
            document.getElementById('badgeCountPedidos').textContent = resultado.cantidadPedidos;
            document.getElementById('badgeCountPagos').textContent = resultado.cantidadPagos;
            document.getElementById('badgeCountDeudas').textContent = resultado.cantidadDeudas;

            // Renderizar Tabla de Pedidos
            const tbodyPedidos = document.getElementById('tbodyPedidos');
            if (resultado.pedidos && resultado.pedidos.length > 0) {
                tbodyPedidos.innerHTML = resultado.pedidos.map(p => `
                    <tr>
                        <td class="fw-semibold text-secondary">${escapeHtml(p.numero)}</td>
                        <td class="fw-medium">${escapeHtml(p.cliente)}</td>
                        <td class="text-secondary">${escapeHtml(p.fecha)}</td>
                        <td>${obtenerBadgeEstado(p.estado)}</td>
                        <td class="text-end fw-semibold">${formatearMoneda(p.total)}</td>
                    </tr>
                `).join('');
            } else {
                tbodyPedidos.innerHTML = `
                    <tr>
                        <td colspan="5" class="text-center py-4 text-muted">
                            <i class="bi bi-inbox d-block mb-1" style="font-size: 1.5rem;"></i>
                            No se encontraron pedidos en el período seleccionado.
                        </td>
                    </tr>`;
            }

            // Renderizar Tabla de Pagos
            const tbodyPagos = document.getElementById('tbodyPagos');
            if (resultado.pagos && resultado.pagos.length > 0) {
                tbodyPagos.innerHTML = resultado.pagos.map(p => `
                    <tr>
                        <td class="fw-medium">${escapeHtml(p.cliente)}</td>
                        <td class="text-secondary">${escapeHtml(p.fecha)}</td>
                        <td><span class="badge bg-light text-dark border">${escapeHtml(p.metodo)}</span></td>
                        <td class="text-end fw-semibold text-success">${formatearMoneda(p.monto)}</td>
                    </tr>
                `).join('');
            } else {
                tbodyPagos.innerHTML = `
                    <tr>
                        <td colspan="4" class="text-center py-4 text-muted">
                            <i class="bi bi-inbox d-block mb-1" style="font-size: 1.5rem;"></i>
                            No se encontraron pagos registrados en el período seleccionado.
                        </td>
                    </tr>`;
            }

            // Renderizar Tabla de Deudas
            const tbodyDeudas = document.getElementById('tbodyDeudas');
            if (resultado.deudas && resultado.deudas.length > 0) {
                tbodyDeudas.innerHTML = resultado.deudas.map(d => `
                    <tr>
                        <td class="fw-medium">${escapeHtml(d.cliente)}</td>
                        <td class="text-secondary">${escapeHtml(d.fecha)}</td>
                        <td class="text-danger fw-medium">${escapeHtml(d.vencimiento)}</td>
                        <td class="text-end fw-semibold text-danger">${formatearMoneda(d.monto)}</td>
                    </tr>
                `).join('');
            } else {
                tbodyDeudas.innerHTML = `
                    <tr>
                        <td colspan="4" class="text-center py-4 text-muted">
                            <i class="bi bi-inbox d-block mb-1" style="font-size: 1.5rem;"></i>
                            No se encontraron deudas registradas en el período seleccionado.
                        </td>
                    </tr>`;
            }

        } catch (error) {
            console.error('Error al actualizar el reporte:', error);
        }
    }

    // Eventos de Pills rápidos
    pills.forEach(pill => {
        pill.addEventListener('click', () => {
            pills.forEach(p => p.classList.remove('active'));
            pill.classList.add('active');

            filtroActual = pill.dataset.filtro;
            const hoy = new Date();

            if (filtroActual === 'hoy') {
                const hoyStr = formatearFechaIso(hoy);
                inputFechaDesde.value = hoyStr;
                inputFechaHasta.value = hoyStr;
            } else if (filtroActual === 'ayer') {
                const ayer = new Date();
                ayer.setDate(hoy.getDate() - 1);
                const ayerStr = formatearFechaIso(ayer);
                inputFechaDesde.value = ayerStr;
                inputFechaHasta.value = ayerStr;
            } else if (filtroActual === 'semana') {
                const diaSemana = hoy.getDay(); // 0 domingo, 1 lunes...
                const diff = (diaSemana + 6) % 7; // días desde el lunes
                const lunes = new Date(hoy);
                lunes.setDate(hoy.getDate() - diff);
                inputFechaDesde.value = formatearFechaIso(lunes);
                inputFechaHasta.value = formatearFechaIso(hoy);
            }

            cargarReporte(filtroActual, inputFechaDesde.value, inputFechaHasta.value);
        });
    });

    // Eventos de cambio en los inputs de fecha
    [inputFechaDesde, inputFechaHasta].forEach(input => {
        input.addEventListener('change', () => {
            pills.forEach(p => p.classList.remove('active'));
            filtroActual = 'personalizado';
            cargarReporte(filtroActual, inputFechaDesde.value, inputFechaHasta.value);
        });
    });

    // Función de exportación
    function dispararExportacion(formato) {
        const url = new URL('/Reportes/Exportar', window.location.origin);
        url.searchParams.set('filtro', filtroActual);
        url.searchParams.set('fechaDesde', inputFechaDesde.value);
        url.searchParams.set('fechaHasta', inputFechaHasta.value);
        url.searchParams.set('formato', formato);
        window.location.href = url.toString();
    }

    if (btnExportarExcel) {
        btnExportarExcel.addEventListener('click', () => dispararExportacion('excel'));
    }
    if (optExportarExcel) {
        optExportarExcel.addEventListener('click', (e) => {
            e.preventDefault();
            dispararExportacion('excel');
        });
    }
    if (optExportarCsv) {
        optExportarCsv.addEventListener('click', (e) => {
            e.preventDefault();
            dispararExportacion('csv');
        });
    }
});
