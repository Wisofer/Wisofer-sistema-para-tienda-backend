// ============================================
// MESAS - Gestión de Mesas
// ============================================

(function() {
    'use strict';
    
    let mesaSeleccionadaId = null;
    let mesaSeleccionadaNumero = null;
    let mesaSeleccionadaEstado = null;
    let mesaTieneOrdenActiva = false;

    function seleccionarMesa(id, numero, estado, tieneOrdenActiva) {
        mesaSeleccionadaId = id;
        mesaSeleccionadaNumero = numero;
        mesaSeleccionadaEstado = estado;
        mesaTieneOrdenActiva = tieneOrdenActiva || false;
        
        document.getElementById('modalMesaNumero').textContent = numero;
        
        // Aplicar estilo al estado según el tipo
        const estadoElement = document.getElementById('modalMesaEstado');
        estadoElement.textContent = estado;
        
        // Remover clases anteriores
        estadoElement.className = 'text-xs font-semibold px-2 py-0.5 rounded-full';
        
        // Aplicar estilo según el estado
        switch(estado) {
            case 'Libre':
                estadoElement.classList.add('bg-green-100', 'dark:bg-green-900/30', 'text-green-700', 'dark:text-green-300');
                break;
            case 'Ocupada':
                estadoElement.classList.add('bg-red-100', 'dark:bg-red-900/30', 'text-red-700', 'dark:text-red-300');
                break;
            case 'Reservada':
                estadoElement.classList.add('bg-yellow-100', 'dark:bg-yellow-900/30', 'text-yellow-700', 'dark:text-yellow-300');
                break;
            default:
                estadoElement.classList.add('bg-gray-100', 'dark:bg-gray-700', 'text-gray-700', 'dark:text-gray-300');
        }
        
        // Mostrar/ocultar botones según si tiene orden activa
        const btnCrearOrden = document.getElementById('btnCrearOrden');
        const btnAgregarProductos = document.getElementById('btnAgregarProductos');
        const btnVerCaja = document.getElementById('btnVerCaja');
        
        if (btnCrearOrden) {
            btnCrearOrden.style.display = mesaTieneOrdenActiva ? 'none' : 'block';
        }
        if (btnAgregarProductos) {
            btnAgregarProductos.style.display = mesaTieneOrdenActiva ? 'block' : 'none';
        }
        if (btnVerCaja) {
            btnVerCaja.style.display = mesaTieneOrdenActiva ? 'block' : 'none';
        }
        
        document.getElementById('modalAcciones').classList.remove('hidden');
        document.getElementById('modalAcciones').classList.add('flex');
    }

    function cerrarModal() {
        document.getElementById('modalAcciones').classList.add('hidden');
        document.getElementById('modalAcciones').classList.remove('flex');
        mesaSeleccionadaId = null;
        mesaSeleccionadaNumero = null;
        mesaSeleccionadaEstado = null;
    }

    function cambiarEstado(nuevoEstado) {
        if (!mesaSeleccionadaId) return;

        fetch(`/mesas/${mesaSeleccionadaId}/cambiar-estado`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
            },
            body: `estado=${nuevoEstado}&__RequestVerificationToken=${document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''}`
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                if (window.CustomModal) {
                    CustomModal.success('Estado actualizado correctamente');
                    setTimeout(() => location.reload(), 1000);
                } else {
                    location.reload();
                }
            } else {
                if (window.CustomModal) {
                    CustomModal.error(data.message || 'Error al cambiar el estado');
                } else {
                    alert('Error: ' + data.message);
                }
            }
        })
        .catch(error => {
            console.error('Error:', error);
            if (window.CustomModal) {
                CustomModal.error('Error al cambiar el estado de la mesa');
            } else {
                alert('Error al cambiar el estado de la mesa');
            }
        });
    }

    function crearOrden() {
        if (!mesaSeleccionadaId) return;
        window.location.href = `/pos?mesaId=${mesaSeleccionadaId}`;
    }

    function agregarProductos() {
        if (!mesaSeleccionadaId) return;
        // Ir al POS con la mesa, el sistema detectará automáticamente la orden activa
        window.location.href = `/pos?mesaId=${mesaSeleccionadaId}`;
    }

    function verCaja() {
        if (!mesaSeleccionadaId) return;
        window.location.href = `/caja/cobro?mesaId=${mesaSeleccionadaId}`;
    }

    async function cancelarOrdenMesa() {
        if (!mesaSeleccionadaId) return;
        
        const mensaje = `¿Está seguro de cancelar la orden de la mesa "${mesaSeleccionadaNumero}"? El stock será restaurado automáticamente.`;
        
        if (window.CustomModal) {
            CustomModal.confirm(mensaje, 'Confirmar Cancelación', async (confirmado) => {
                if (!confirmado) return;
                await cancelarOrdenMesaContinuar();
            });
        } else {
            if (!confirm(mensaje)) return;
            await cancelarOrdenMesaContinuar();
        }
    }

    async function cancelarOrdenMesaContinuar() {
        // Primero obtener la orden activa de la mesa
        try {
            const response = await fetch(`/mesas/${mesaSeleccionadaId}/orden-activa`);
            const data = await response.json();
            
            if (!data.success || !data.ordenId) {
                if (window.CustomModal) {
                    CustomModal.error('No se encontró una orden activa para esta mesa');
                } else {
                    alert('No se encontró una orden activa para esta mesa');
                }
                return;
            }

            const ordenId = data.ordenId;
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
            
            const cancelResponse = await fetch(`/pos/cancelar/${ordenId}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'RequestVerificationToken': token
                },
                body: `__RequestVerificationToken=${token}`
            });

            const cancelData = await cancelResponse.json();
            
            if (cancelData.success) {
                if (window.CustomModal) {
                    CustomModal.success(cancelData.message || 'Orden cancelada exitosamente');
                } else {
                    alert('✅ ' + (cancelData.message || 'Orden cancelada exitosamente'));
                }
                
                setTimeout(() => {
                    location.reload();
                }, 1500);
            } else {
                if (window.CustomModal) {
                    CustomModal.error(cancelData.message || 'Error al cancelar la orden');
                } else {
                    alert('❌ ' + (cancelData.message || 'Error al cancelar la orden'));
                }
            }
        } catch (error) {
            console.error('Error al cancelar orden:', error);
            if (window.CustomModal) {
                CustomModal.error('Error al cancelar la orden: ' + error.message);
            } else {
                alert('❌ Error al cancelar la orden: ' + error.message);
            }
        }
    }

    function editarMesa(id) {
        event.stopPropagation();
        window.location.href = `/mesas/${id}/editar`;
    }

    function eliminarMesa(id, numero) {
        event.stopPropagation();
        const mensaje = `¿Está seguro de eliminar la mesa "${numero}"?`;
        
        if (window.CustomModal) {
            CustomModal.confirm(mensaje, 'Confirmar Eliminación', (confirmado) => {
                if (confirmado) {
                    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
                    const form = document.createElement('form');
                    form.method = 'POST';
                    form.action = `/mesas/${id}/eliminar`;
                    
                    const tokenInput = document.createElement('input');
                    tokenInput.type = 'hidden';
                    tokenInput.name = '__RequestVerificationToken';
                    tokenInput.value = token;
                    form.appendChild(tokenInput);
                    
                    document.body.appendChild(form);
                    form.submit();
                }
            });
        } else {
            if (confirm(mensaje)) {
                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
                const form = document.createElement('form');
                form.method = 'POST';
                form.action = `/mesas/${id}/eliminar`;
                
                const tokenInput = document.createElement('input');
                tokenInput.type = 'hidden';
                tokenInput.name = '__RequestVerificationToken';
                tokenInput.value = token;
                form.appendChild(tokenInput);
                
                document.body.appendChild(form);
                form.submit();
            }
        }
    }

    function editarMesaDesdeModal() {
        if (!mesaSeleccionadaId) return;
        window.location.href = `/mesas/${mesaSeleccionadaId}/editar`;
    }

    function eliminarMesaDesdeModal() {
        if (!mesaSeleccionadaId) return;
        eliminarMesa(mesaSeleccionadaId, mesaSeleccionadaNumero);
    }

    // Exponer funciones globalmente
    window.seleccionarMesa = seleccionarMesa;
    window.cerrarModal = cerrarModal;
    window.cambiarEstado = cambiarEstado;
    window.crearOrden = crearOrden;
    window.agregarProductos = agregarProductos;
    window.verCaja = verCaja;
    window.cancelarOrdenMesa = cancelarOrdenMesa;
    window.editarMesa = editarMesa;
    window.eliminarMesa = eliminarMesa;
    window.editarMesaDesdeModal = editarMesaDesdeModal;
    window.eliminarMesaDesdeModal = eliminarMesaDesdeModal;
})();

