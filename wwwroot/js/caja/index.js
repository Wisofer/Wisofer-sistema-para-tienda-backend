// ============================================
// CAJA - Sistema de Pago
// ============================================

(function() {
    'use strict';
    
    // Variables globales (se inicializarán desde la vista)
    let ordenId = 0;
    let totalOrden = 0;
    let montoExacto = 0;
    let montoRedondeado100 = 0;
    let montoRedondeado500 = 0;
    let tipoCambio = 36.80; // Valor por defecto (fallback) - El valor real viene de la configuración del sistema
    let montoEnDolares = 0;
    let monedaSeleccionada = 'C$'; // C$ o $
    let displayValue = '0';
    let metodoPago = 'Efectivo';

    // Inicializar valores desde data attributes o variables globales
    function inicializarValores() {
        const container = document.querySelector('[data-caja-config]');
        if (container) {
            ordenId = parseInt(container.dataset.ordenId) || 0;
            totalOrden = parseFloat(container.dataset.totalOrden) || 0;
            montoExacto = parseFloat(container.dataset.montoExacto) || 0;
            montoRedondeado100 = parseFloat(container.dataset.montoRedondeado100) || 0;
            montoRedondeado500 = parseFloat(container.dataset.montoRedondeado500) || 0;
            // IMPORTANTE: El tipo de cambio viene de la configuración del sistema (Moneda/Tipo de Cambio)
            // El valor 36.80 es solo un fallback si no se puede obtener del servidor
            tipoCambio = parseFloat(container.dataset.tipoCambio) || 36.80;
            montoEnDolares = parseFloat(container.dataset.montoDolares) || 0;
            
            console.log('Tipo de cambio obtenido de configuración:', tipoCambio);
        }
        
        console.log('=== INICIO SCRIPT CAJA ===');
        console.log('Total orden:', totalOrden);
        console.log('Orden ID:', ordenId);
        console.log('Tipo de cambio:', tipoCambio);
        console.log('Monto en dólares:', montoEnDolares);
    }
    
    // Seleccionar moneda
    function setMoneda(moneda) {
        console.log('setMoneda llamado con:', moneda);
        monedaSeleccionada = moneda;
        
        const btnCordoba = document.getElementById('btn-moneda-cordoba');
        const btnDolar = document.getElementById('btn-moneda-dolar');
        const infoTipoCambio = document.getElementById('info-tipo-cambio');
        const displayTotalCordoba = document.getElementById('display-total-cordoba');
        const displayTotalDolar = document.getElementById('display-total-dolar');
        
        // Actualizar botones
        if (moneda === 'C$') {
            if (btnCordoba) {
                btnCordoba.classList.remove('bg-gray-300', 'dark:bg-gray-700', 'text-gray-700', 'dark:text-gray-300');
                btnCordoba.classList.add('bg-amber-500', 'text-white');
            }
            if (btnDolar) {
                btnDolar.classList.remove('bg-amber-500', 'text-white');
                btnDolar.classList.add('bg-gray-300', 'dark:bg-gray-700', 'text-gray-700', 'dark:text-gray-300');
            }
            if (infoTipoCambio) infoTipoCambio.classList.add('hidden');
            if (displayTotalCordoba) displayTotalCordoba.classList.remove('hidden');
            if (displayTotalDolar) displayTotalDolar.classList.add('hidden');
        } else {
            if (btnCordoba) {
                btnCordoba.classList.remove('bg-amber-500', 'text-white');
                btnCordoba.classList.add('bg-gray-300', 'dark:bg-gray-700', 'text-gray-700', 'dark:text-gray-300');
            }
            if (btnDolar) {
                btnDolar.classList.remove('bg-gray-300', 'dark:bg-gray-700', 'text-gray-700', 'dark:text-gray-300');
                btnDolar.classList.add('bg-amber-500', 'text-white');
            }
            if (infoTipoCambio) infoTipoCambio.classList.remove('hidden');
            if (displayTotalCordoba) displayTotalCordoba.classList.add('hidden');
            if (displayTotalDolar) displayTotalDolar.classList.remove('hidden');
        }
        
        // Actualizar display y recalcular
        actualizarDisplay();
    }

    // Actualizar reloj
    function actualizarReloj() {
        try {
            const reloj = document.getElementById('reloj');
            if (reloj) {
                const ahora = new Date();
                const horas = String(ahora.getHours()).padStart(2, '0');
                const minutos = String(ahora.getMinutes()).padStart(2, '0');
                reloj.textContent = `${horas}:${minutos}`;
            }
        } catch (e) {
            console.error('Error al actualizar reloj:', e);
        }
    }

    // Calculadora
    function appendNumber(num) {
        console.log('appendNumber llamado con:', num);
        try {
            if (displayValue === '0') {
                displayValue = num === '.' ? '0.' : num;
            } else {
                if (num === '.' && displayValue.includes('.')) return;
                displayValue += num;
            }
            console.log('Nuevo displayValue:', displayValue);
            actualizarDisplay();
        } catch (e) {
            console.error('Error en appendNumber:', e);
            if (window.CustomModal) {
                CustomModal.error('Error al agregar número: ' + e.message);
            } else {
                alert('Error al agregar número: ' + e.message);
            }
        }
    }

    function borrarUltimo() {
        console.log('borrarUltimo llamado');
        displayValue = displayValue.slice(0, -1) || '0';
        actualizarDisplay();
    }

    function limpiar() {
        console.log('limpiar llamado');
        displayValue = '0';
        actualizarDisplay();
    }

    function setMonto(monto) {
        console.log('setMonto llamado con:', monto);
        displayValue = monto.toString();
        actualizarDisplay();
    }

    function agregarMonto(cantidad) {
        console.log('agregarMonto llamado con:', cantidad);
        const valorActual = parseFloat(displayValue) || 0;
        displayValue = (valorActual + cantidad).toString();
        actualizarDisplay();
    }

    function actualizarDisplay() {
        try {
            console.log('actualizarDisplay llamado, displayValue:', displayValue, 'moneda:', monedaSeleccionada);
            const display = document.getElementById('display-calculadora');
            if (!display) {
                console.error('ERROR: No se encontró el display');
                return;
            }
            
            // Actualizar símbolo de moneda en el display
            const simboloMoneda = monedaSeleccionada === '$' ? '$' : 'C$';
            display.value = displayValue;
            console.log('Display actualizado a:', displayValue);
            
            // Calcular cambio según la moneda seleccionada
            const montoPagado = parseFloat(displayValue) || 0;
            let cambio = 0;
            let cambioEnCordobas = 0;
            let totalAComparar = totalOrden;
            let mostrarCambioEnCordobas = false;
            
            if (monedaSeleccionada === '$') {
                // Si la moneda es dólares, comparar con el monto en dólares
                totalAComparar = montoEnDolares;
                const cambioEnDolares = montoPagado - totalAComparar;
                // Convertir el cambio a córdobas (siempre se da cambio en córdobas cuando se paga en dólares)
                cambioEnCordobas = cambioEnDolares * tipoCambio;
                cambio = cambioEnDolares; // Para validación
                mostrarCambioEnCordobas = true;
            } else {
                // Si la moneda es córdobas, comparar con el total en córdobas
                cambio = montoPagado - totalOrden;
                cambioEnCordobas = cambio;
            }
            
            console.log('Monto pagado:', montoPagado, 'Total a comparar:', totalAComparar, 'Cambio:', cambio, 'Cambio en córdobas:', cambioEnCordobas);
            
            const displayCambio = document.getElementById('display-cambio');
            const btnCobrar = document.getElementById('btn-cobrar');
            
            if (!displayCambio) {
                console.error('ERROR: No se encontró el display de cambio');
                return;
            }
            
            if (cambio < 0) {
                displayCambio.textContent = `C$0.00`;
                displayCambio.classList.remove('text-green-600');
                displayCambio.classList.add('text-gray-400');
                if (btnCobrar) {
                    btnCobrar.disabled = true;
                    console.log('Botón cobrar deshabilitado (monto insuficiente)');
                }
            } else {
                // Siempre mostrar cambio en córdobas (especialmente cuando se paga en dólares)
                displayCambio.textContent = `C$${cambioEnCordobas.toFixed(2)}`;
                displayCambio.classList.remove('text-gray-400');
                displayCambio.classList.add('text-green-600');
                if (btnCobrar) {
                    btnCobrar.disabled = false;
                    console.log('Botón cobrar habilitado');
                }
            }
        } catch (e) {
            console.error('Error en actualizarDisplay:', e);
            if (window.CustomModal) {
                CustomModal.error('Error al actualizar display: ' + e.message);
            } else {
                alert('Error al actualizar display: ' + e.message);
            }
        }
    }

    // Método de pago
    function setMetodoPago(metodo) {
        console.log('setMetodoPago llamado con:', metodo);
        metodoPago = metodo;
        
        try {
            // Actualizar botones
            const btnEfectivo = document.getElementById('btn-efectivo');
            const btnTarjeta = document.getElementById('btn-tarjeta');
            const btnTransferencia = document.getElementById('btn-transferencia');
            
            // Remover clase active de todos
            [btnEfectivo, btnTarjeta, btnTransferencia].forEach(btn => {
                if (btn) {
                    btn.classList.remove('active');
                    btn.classList.remove('bg-green-500', 'text-white');
                    btn.classList.add('bg-white', 'border-gray-200', 'text-gray-700');
                }
            });
            
            // Agregar clase active al seleccionado
            if (metodo === 'Efectivo' && btnEfectivo) {
                btnEfectivo.classList.add('active');
                btnEfectivo.classList.remove('bg-white', 'border-gray-200', 'text-gray-700');
                btnEfectivo.classList.add('bg-green-50', 'border-green-500', 'text-green-700');
            } else if (metodo === 'Tarjeta' && btnTarjeta) {
                btnTarjeta.classList.add('active');
                btnTarjeta.classList.remove('bg-white', 'border-gray-200', 'text-gray-700');
                btnTarjeta.classList.add('bg-green-50', 'border-green-500', 'text-green-700');
            } else if (metodo === 'Transferencia' && btnTransferencia) {
                btnTransferencia.classList.add('active');
                btnTransferencia.classList.remove('bg-white', 'border-gray-200', 'text-gray-700');
                btnTransferencia.classList.add('bg-green-50', 'border-green-500', 'text-green-700');
            }
            
            console.log('Método de pago actualizado a:', metodoPago);
        } catch (e) {
            console.error('Error en setMetodoPago:', e);
        }
    }

    // Procesar pago
    async function procesarPago() {
        console.log('=== procesarPago llamado ===');
        console.log('displayValue:', displayValue);
        console.log('totalOrden:', totalOrden);
        console.log('metodoPago:', metodoPago);
        console.log('monedaSeleccionada:', monedaSeleccionada);
        
        const montoPagado = parseFloat(displayValue) || 0;
        console.log('Monto pagado calculado:', montoPagado);
        
        // Validar según la moneda seleccionada
        let totalAValidar = totalOrden;
        let simboloMoneda = 'C$';
        
        if (monedaSeleccionada === '$') {
            totalAValidar = montoEnDolares;
            simboloMoneda = '$';
        }
        
        if (montoPagado < totalAValidar) {
            if (window.CustomModal) {
                CustomModal.error(`El monto es insuficiente. Monto recibido: ${simboloMoneda}${montoPagado.toFixed(2)}, Total: ${simboloMoneda}${totalAValidar.toFixed(2)}`);
            } else {
                alert(`❌ El monto es insuficiente. Monto recibido: ${simboloMoneda}${montoPagado.toFixed(2)}, Total: ${simboloMoneda}${totalAValidar.toFixed(2)}`);
            }
            return;
        }

        if (totalAValidar <= 0) {
            if (window.CustomModal) {
                CustomModal.error('El total de la orden es inválido');
            } else {
                alert('❌ Error: El total de la orden es inválido');
            }
            return;
        }

        if (ordenId <= 0) {
            if (window.CustomModal) {
                CustomModal.error('No se encontró la orden');
            } else {
                alert('❌ Error: No se encontró la orden');
            }
            return;
        }

        // Calcular cambio según moneda
        let cambio = montoPagado - totalAValidar;
        let cambioEnCordobas = cambio;
        
        // Si se paga en dólares, el cambio siempre se da en córdobas
        if (monedaSeleccionada === '$') {
            cambioEnCordobas = cambio * tipoCambio;
        }
        
        const mensajeCambio = monedaSeleccionada === '$' 
            ? `Cambio: C$${cambioEnCordobas.toFixed(2)} (equivalente a $${cambio.toFixed(2)})`
            : `Cambio: C$${cambio.toFixed(2)}`;
        
        const mensajeConfirmacion = `¿Confirmar pago de ${simboloMoneda}${totalAValidar.toFixed(2)} con ${metodoPago}?\n\nMonto recibido: ${simboloMoneda}${montoPagado.toFixed(2)}\n${mensajeCambio}`;
        
        // Usar el modal personalizado - debe estar disponible porque se carga antes
        const mostrarConfirmacion = () => {
            if (window.CustomModal && typeof window.CustomModal.confirm === 'function') {
                CustomModal.confirm(mensajeConfirmacion, 'Confirmar Pago', async (confirmado) => {
                    if (!confirmado) return;
                    await procesarPagoContinuar(montoPagado);
                });
            } else {
                // Si no está disponible, esperar un momento (el script se carga antes)
                console.warn('CustomModal no disponible, esperando...');
                setTimeout(() => {
                    if (window.CustomModal && typeof window.CustomModal.confirm === 'function') {
                        CustomModal.confirm(mensajeConfirmacion, 'Confirmar Pago', async (confirmado) => {
                            if (!confirmado) return;
                            await procesarPagoContinuar(montoPagado);
                        });
                    } else {
                        console.error('CustomModal no disponible después de esperar');
                        // Último recurso: mostrar error
                        if (window.CustomModal) {
                            CustomModal.error('Error: El modal de confirmación no está disponible. Por favor, recarga la página.');
                        } else {
                            alert('Error: El modal de confirmación no está disponible. Por favor, recarga la página.');
                        }
                    }
                }, 200);
            }
        };
        
        mostrarConfirmacion();
    }
    
    async function procesarPagoContinuar(montoPagado) {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        if (!tokenInput) {
            if (window.CustomModal) {
                CustomModal.error('No se encontró el token de seguridad');
            } else {
                alert('❌ Error: No se encontró el token de seguridad');
            }
            return;
        }
        const token = tokenInput.value;
        console.log('Token encontrado:', token ? 'Sí' : 'No');
        
        const request = {
            ordenId: ordenId,
            tipoPago: metodoPago,
            montoPagado: montoPagado,
            moneda: monedaSeleccionada,
            banco: null,
            tipoCuenta: null,
            observaciones: null
        };
        
        console.log('Request a enviar:', request);

        try {
            console.log('Enviando petición a /caja/procesar-pago...');
            const response = await fetch('/caja/procesar-pago', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify(request)
            });

            console.log('Respuesta recibida, status:', response.status);
            const data = await response.json();
            console.log('Datos recibidos:', data);
            
            if (data.success) {
                const cambio = data.vuelto || 0;
                // El cambio siempre se muestra en córdobas (especialmente cuando se paga en dólares)
                const mensaje = `Pago procesado exitosamente\n\nOrden: ${data.ordenNumero}\nCambio: C$${cambio.toFixed(2)}`;
                
                if (window.CustomModal) {
                    CustomModal.success(mensaje, 'Pago Exitoso');
                } else {
                    alert(`✅ ${mensaje}`);
                }
                
                // Imprimir recibo automáticamente
                if (data.urlImpresionRecibo) {
                    setTimeout(() => {
                        imprimirTicket(data.urlImpresionRecibo);
                    }, 500);
                }
                
                // Redirigir a mesas
                setTimeout(() => {
                    window.location.href = '/mesas';
                }, 2500);
            } else {
                // Si la caja está cerrada, mostrar mensaje especial y redirigir
                if (data.cajaCerrada) {
                    const mensaje = '⚠️ ' + (data.message || 'La caja está cerrada. Debe abrir la caja antes de procesar pagos.');
                    if (window.CustomModal) {
                        CustomModal.error(mensaje, 'Caja Cerrada', () => {
                            // Redirigir a la página de caja después de cerrar el modal
                            window.location.href = '/caja';
                        });
                    } else {
                        if (confirm(mensaje + '\n\n¿Desea ir a la página de caja para abrirla?')) {
                            window.location.href = '/caja';
                        }
                    }
                } else {
                if (window.CustomModal) {
                    CustomModal.error(data.message || 'No se pudo procesar el pago');
                } else {
                    alert('❌ Error: ' + (data.message || 'No se pudo procesar el pago'));
                    }
                }
            }
        } catch (error) {
            console.error('Error completo:', error);
            if (window.CustomModal) {
                CustomModal.error('Error al procesar el pago: ' + error.message);
            } else {
                alert('❌ Error al procesar el pago: ' + error.message);
            }
        }
    }

    // Función para imprimir ticket - Abre directamente el diálogo de impresión (Ctrl+P)
    function imprimirTicket(url) {
        // Crear un iframe oculto para cargar el recibo
        const iframe = document.createElement('iframe');
        iframe.style.position = 'fixed';
        iframe.style.right = '-9999px'; // Fuera de la pantalla
        iframe.style.bottom = '-9999px';
        iframe.style.width = '1px';
        iframe.style.height = '1px';
        iframe.style.border = 'none';
        iframe.style.opacity = '0';
        iframe.style.pointerEvents = 'none';
        iframe.src = url;
        
        document.body.appendChild(iframe);
        
        // Cuando el iframe carga, imprimir directamente (el script en el HTML llama a window.print())
        iframe.onload = function() {
            setTimeout(() => {
                try {
                    // El script en la página del recibo ya llama a window.print() automáticamente
                    // Solo necesitamos esperar a que se cargue
                    // Remover el iframe después de un tiempo
                    setTimeout(() => {
                        try {
                            document.body.removeChild(iframe);
                        } catch (e) {
                            // Ignorar errores
                        }
                    }, 3000);
                } catch (e) {
                    console.error('Error al imprimir:', e);
                    // Si falla, intentar con ventana nueva como respaldo
                    const ventanaImpresion = window.open(url, '_blank', 'width=1,height=1,left=0,top=0');
                    if (ventanaImpresion) {
                        setTimeout(() => {
                            try {
                                ventanaImpresion.close();
                            } catch (e) {
                                // Ignorar
                            }
                        }, 2000);
                    }
                    try {
                        document.body.removeChild(iframe);
                    } catch (e) {
                        // Ignorar
                    }
                }
            }, 200);
        };
        
        // Si hay error al cargar, usar método alternativo
        iframe.onerror = function() {
            const ventanaImpresion = window.open(url, '_blank', 'width=1,height=1,left=0,top=0');
        if (ventanaImpresion) {
                setTimeout(() => {
                    try {
                        ventanaImpresion.close();
                    } catch (e) {
                        // Ignorar
                    }
                }, 2000);
            }
            try {
                document.body.removeChild(iframe);
            } catch (e) {
                // Ignorar
        }
        };
    }

    // Función para inicializar la caja
    function inicializarCaja() {
        console.log('=== INICIALIZANDO CAJA ===');
        console.log('Total orden:', totalOrden);
        console.log('Orden ID:', ordenId);
        
        // Esperar un momento para asegurar que el DOM esté completamente renderizado
        setTimeout(function() {
            // Verificar que los elementos existan
            const display = document.getElementById('display-calculadora');
            const btnCobrar = document.getElementById('btn-cobrar');
            const displayCambio = document.getElementById('display-cambio');
            
            console.log('Display encontrado:', display !== null);
            console.log('Botón cobrar encontrado:', btnCobrar !== null);
            console.log('Display cambio encontrado:', displayCambio !== null);
            
            if (!display) {
                console.error('ERROR CRÍTICO: No se encontró el display de la calculadora');
                if (window.CustomModal) {
                    CustomModal.error('No se encontró el display de la calculadora. Por favor, recarga la página.');
                } else {
                    alert('Error: No se encontró el display de la calculadora. Por favor, recarga la página.');
                }
                return;
            }
            
            if (!btnCobrar) {
                console.error('ERROR CRÍTICO: No se encontró el botón de cobrar');
                if (window.CustomModal) {
                    CustomModal.error('No se encontró el botón de cobrar. Por favor, recarga la página.');
                } else {
                    alert('Error: No se encontró el botón de cobrar. Por favor, recarga la página.');
                }
                return;
            }
            
            // Verificar que las funciones estén disponibles globalmente
            window.appendNumber = appendNumber;
            window.borrarUltimo = borrarUltimo;
            window.limpiar = limpiar;
            window.setMonto = setMonto;
            window.agregarMonto = agregarMonto;
            window.setMetodoPago = setMetodoPago;
            window.setMoneda = setMoneda;
            window.procesarPago = procesarPago;
            
            console.log('Funciones asignadas a window');
            
            // Configurar event listeners para todos los botones con data-action
            console.log('Configurando event listeners...');
            const botones = document.querySelectorAll('[data-action]');
            console.log('Botones encontrados:', botones.length);
            
            if (botones.length === 0) {
                console.error('ERROR: No se encontraron botones con data-action');
                if (window.CustomModal) {
                    CustomModal.error('No se encontraron botones. Por favor, recarga la página.');
                } else {
                    alert('Error: No se encontraron botones. Por favor, recarga la página.');
                }
                return;
            }
            
            // Usar delegación de eventos en lugar de agregar listeners individuales
            document.addEventListener('click', function(e) {
                // Verificar si el click es en el modal (no procesar)
                const modal = document.getElementById('custom-modal');
                if (modal && !modal.classList.contains('hidden')) {
                    // Si el modal está abierto, no procesar otros clicks
                    if (!e.target.closest('#modal-content')) {
                        return;
                    }
                }
                
                const button = e.target.closest('[data-action]');
                if (!button) return;
                
                e.preventDefault();
                e.stopPropagation();
                
                const action = button.getAttribute('data-action');
                console.log('=== BOTÓN CLICKEADO ===');
                console.log('Acción:', action);
                console.log('Botón:', button);
                console.log('displayValue antes:', displayValue);
                
                try {
                    switch(action) {
                        case 'appendNumber':
                            const value = button.getAttribute('data-value');
                            console.log('appendNumber con valor:', value);
                            appendNumber(value);
                            break;
                        case 'borrarUltimo':
                            console.log('borrarUltimo');
                            borrarUltimo();
                            break;
                        case 'limpiar':
                            console.log('limpiar');
                            limpiar();
                            break;
                        case 'setMonto':
                            const montoStr = button.getAttribute('data-monto');
                            let monto = parseFloat(montoStr);
                            
                            // Si el monto no se puede parsear, intentar usar los valores predefinidos
                            if (isNaN(monto)) {
                                const actionText = button.textContent.trim();
                                if (actionText.includes('Exacto')) {
                                    monto = montoExacto;
                                } else if (actionText.includes('100')) {
                                    monto = montoRedondeado100;
                                } else if (actionText.includes('500')) {
                                    monto = montoRedondeado500;
                                }
                            }
                            
                            console.log('setMonto con monto:', monto, 'de string:', montoStr);
                            if (!isNaN(monto)) {
                                setMonto(monto);
                            } else {
                                console.error('Error: monto inválido:', montoStr);
                            }
                            break;
                        case 'agregarMonto':
                            const cantidadStr = button.getAttribute('data-cantidad');
                            const cantidad = parseInt(cantidadStr);
                            console.log('agregarMonto con cantidad:', cantidad);
                            if (!isNaN(cantidad)) {
                                agregarMonto(cantidad);
                            } else {
                                console.error('Error: cantidad inválida:', cantidadStr);
                            }
                            break;
                        case 'setMetodoPago':
                            const metodo = button.getAttribute('data-metodo');
                            console.log('setMetodoPago con método:', metodo);
                            setMetodoPago(metodo);
                            break;
                        case 'procesarPago':
                            console.log('procesarPago');
                            procesarPago();
                            break;
                        case 'setMoneda':
                            const moneda = button.getAttribute('data-moneda');
                            console.log('setMoneda con moneda:', moneda);
                            setMoneda(moneda);
                            break;
                        default:
                            console.warn('Acción desconocida:', action);
                    }
                } catch (error) {
                    console.error('Error al ejecutar acción:', action, error);
                    console.error('Stack trace:', error.stack);
                    if (window.CustomModal) {
                        CustomModal.error('Error: ' + error.message);
                    } else {
                        alert('Error: ' + error.message);
                    }
                }
            });
            
            console.log('Delegación de eventos configurada');
            console.log('Event listeners configurados para', botones.length, 'botones');
            
            // Inicializar moneda por defecto (Córdobas)
            setMoneda('C$');
            
            // Inicializar display
            console.log('Inicializando display...');
            actualizarDisplay();
            console.log('Display inicializado');
            
            console.log('=== FIN INICIALIZACIÓN CAJA ===');
        }, 100); // Esperar 100ms para asegurar que el DOM esté completamente renderizado
    }

    // Inicializar cuando el DOM esté listo
    function init() {
        inicializarValores();
        
        // Verificar que los elementos existan
        document.addEventListener('DOMContentLoaded', function() {
            console.log('DOM cargado');
            const display = document.getElementById('display-calculadora');
            const btnCobrar = document.getElementById('btn-cobrar');
            console.log('Display encontrado:', display !== null);
            console.log('Botón cobrar encontrado:', btnCobrar !== null);
            
            if (!display) {
                console.error('ERROR: No se encontró el display de la calculadora');
            }
            if (!btnCobrar) {
                console.error('ERROR: No se encontró el botón de cobrar');
            }
        });

        // Actualizar reloj cada segundo
        setInterval(actualizarReloj, 1000);
        actualizarReloj();

        // Atajos de teclado
        document.addEventListener('keydown', (e) => {
            if (e.key >= '0' && e.key <= '9') {
                appendNumber(e.key);
            } else if (e.key === '.') {
                appendNumber('.');
            } else if (e.key === 'Backspace') {
                borrarUltimo();
            } else if (e.key === 'Escape') {
                limpiar();
            } else if (e.key === 'Enter') {
                procesarPago();
            }
        });

        // Inicializar cuando el DOM esté listo
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', function() {
                console.log('DOM cargado, inicializando...');
                inicializarCaja();
            });
        } else {
            console.log('DOM ya está listo, inicializando...');
            inicializarCaja();
        }
    }

    // Ejecutar inicialización
    init();
})();

