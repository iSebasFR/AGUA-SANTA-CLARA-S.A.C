// ===== T24: abrir ventana, acción y motivos dinámicos =====
function modalEl() { return document.getElementById('modalModificarStock'); }
function contenidoModal() { return document.getElementById('contenidoModalStock'); }

function pintarAccion(form) {
    const accion = form.querySelector('#Accion').value;
    form.querySelectorAll('.accion-btn').forEach(function (b) {
        b.classList.remove('active-aumentar', 'active-descontar');
        if (b.dataset.accion === accion) {
            b.classList.add(accion === 'Aumentar' ? 'active-aumentar' : 'active-descontar');
        }
    });
}

function cargarMotivos(form) {
    const motivosPorAccion = JSON.parse(form.dataset.motivos);
    const accion = form.querySelector('#Accion').value;
    const select = form.querySelector('#Motivo');
    const lista = motivosPorAccion[accion] || [];
    const actual = select.dataset.motivoActual;

    select.innerHTML = '';
    lista.forEach(function (m) { select.add(new Option(m, m)); });
    if (lista.includes(actual)) select.value = actual;
}

function inicializarFormulario() {
    const form = document.getElementById('formModificarStock');
    if (!form) return;
    pintarAccion(form);
    cargarMotivos(form);
}

async function abrirModalModificar(id) {
    const resp = await fetch('/Inventario/Insumos/Modificar/' + id);
    if (!resp.ok) return;

    contenidoModal().innerHTML = await resp.text();
    inicializarFormulario();
    bootstrap.Modal.getOrCreateInstance(modalEl()).show();
}

document.addEventListener('click', function (e) {
    const btnModificar = e.target.closest('.btn-modificar');
    if (btnModificar) {
        abrirModalModificar(btnModificar.dataset.id);
        return;
    }

    const btnAccion = e.target.closest('.accion-btn');
    if (btnAccion) {
        const form = document.getElementById('formModificarStock');
        form.querySelector('#Accion').value = btnAccion.dataset.accion;
        form.querySelector('#Motivo').dataset.motivoActual = '';
        pintarAccion(form);
        cargarMotivos(form);
    }
});

// ==================== T25: ENVIAR FORMULARIO (VALIDACIÓN) ====================
document.addEventListener('submit', async function (e) {
    const form = e.target;
    if (form.id !== 'formModificarStock') return;

    e.preventDefault();

    const boton = form.querySelector('button[type="submit"]');
    if (boton) boton.disabled = true;

    try {
        const respuesta = await fetch(form.action, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        const tipo = respuesta.headers.get('content-type') || '';

        if (tipo.includes('application/json')) {
            // Validación correcta: cerrar la ventana y avisar (T26 actualizará el listado)
            const data = await respuesta.json();

            if (data.ok) {
                bootstrap.Modal.getOrCreateInstance(modalEl()).hide();
                document.dispatchEvent(new CustomEvent('stock:guardado', { detail: data }));
            }
        } else if (respuesta.ok) {
            // HTML con errores de validación: se muestra dentro de la misma ventana
            contenidoModal().innerHTML = await respuesta.text();
            inicializarFormulario();
        } else {
            console.error('Error al guardar el stock. Código:', respuesta.status);
        }
    } catch (error) {
        console.error('Error de red al guardar el stock:', error);
    } finally {
        // Si la ventana se reemplazó, este botón ya no existe y no pasa nada
        if (boton) boton.disabled = false;
    }
});