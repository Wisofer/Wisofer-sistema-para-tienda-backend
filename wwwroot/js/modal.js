// ============================================
// MODAL PERSONALIZADO - Sistema de Notificaciones
// ============================================

(function() {
    'use strict';
    
    // Sonidos
    const soundSuccess = new Audio('/sounds/success.mp3');
    const soundError = new Audio('/sounds/error.mp3');
    
    // Configurar volumen
    soundSuccess.volume = 0.5;
    soundError.volume = 0.5;
    
    // Crear modal si no existe
    function crearModal() {
        if (document.getElementById('custom-modal')) {
            return; // Ya existe
        }
        
        const modalHTML = `
            <div id="custom-modal" class="hidden fixed inset-0 bg-black bg-opacity-50 dark:bg-opacity-70 z-[9999] flex items-center justify-center p-4" onclick="if(event.target === this) CustomModal.cerrar()">
                <div class="bg-white dark:bg-gray-800 rounded-xl shadow-2xl max-w-md w-full transform transition-all duration-300 scale-95 opacity-0" id="modal-content">
                    <div class="p-6">
                        <div class="flex items-start mb-4">
                            <div id="modal-icon" class="flex-shrink-0 mr-4"></div>
                            <div class="flex-1">
                                <h3 id="modal-title" class="text-lg font-bold text-gray-900 dark:text-white mb-2"></h3>
                                <p id="modal-message" class="text-sm text-gray-600 dark:text-gray-300"></p>
                            </div>
                            <button onclick="CustomModal.cerrar()" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 transition-colors">
                                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>
                        <div class="flex justify-end gap-2">
                            <button id="modal-btn-cancel" onclick="CustomModal.cerrar()" class="hidden px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 hover:bg-gray-200 dark:hover:bg-gray-600 rounded-lg transition-colors">
                                Cancelar
                            </button>
                            <button id="modal-btn-ok" onclick="CustomModal.cerrar()" class="px-4 py-2 text-sm font-medium text-white bg-amber-500 hover:bg-amber-600 rounded-lg transition-colors">
                                Aceptar
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;
        
        document.body.insertAdjacentHTML('beforeend', modalHTML);
    }
    
    // Mostrar modal
    function mostrar(tipo, titulo, mensaje, callback) {
        crearModal();
        
        const modal = document.getElementById('custom-modal');
        const content = document.getElementById('modal-content');
        const icon = document.getElementById('modal-icon');
        const title = document.getElementById('modal-title');
        const message = document.getElementById('modal-message');
        const btnOk = document.getElementById('modal-btn-ok');
        const btnCancel = document.getElementById('modal-btn-cancel');
        
        // Configurar según el tipo
        if (tipo === 'success') {
            icon.innerHTML = `
                <div class="w-12 h-12 bg-green-100 dark:bg-green-900/30 rounded-full flex items-center justify-center">
                    <svg class="w-6 h-6 text-green-600 dark:text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                    </svg>
                </div>
            `;
            title.textContent = titulo || 'Éxito';
            message.textContent = mensaje;
            btnOk.textContent = 'Aceptar';
            btnCancel.classList.add('hidden');
            
            // Reproducir sonido
            soundSuccess.play().catch(e => console.log('No se pudo reproducir sonido:', e));
            
        } else if (tipo === 'error') {
            icon.innerHTML = `
                <div class="w-12 h-12 bg-red-100 dark:bg-red-900/30 rounded-full flex items-center justify-center">
                    <svg class="w-6 h-6 text-red-600 dark:text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                    </svg>
                </div>
            `;
            title.textContent = titulo || 'Error';
            message.textContent = mensaje;
            btnOk.textContent = 'Aceptar';
            btnCancel.classList.add('hidden');
            
            // Reproducir sonido
            soundError.play().catch(e => console.log('No se pudo reproducir sonido:', e));
            
        } else if (tipo === 'confirm') {
            icon.innerHTML = `
                <div class="w-12 h-12 bg-yellow-100 dark:bg-yellow-900/30 rounded-full flex items-center justify-center">
                    <svg class="w-6 h-6 text-yellow-600 dark:text-yellow-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                    </svg>
                </div>
            `;
            title.textContent = titulo || 'Confirmar';
            message.textContent = mensaje;
            btnOk.textContent = 'Confirmar';
            btnCancel.classList.remove('hidden');
            
            // Configurar callbacks
            btnOk.onclick = function() {
                if (callback) callback(true);
                cerrar();
            };
            btnCancel.onclick = function() {
                if (callback) callback(false);
                cerrar();
            };
        }
        
        // Mostrar modal con animación
        modal.classList.remove('hidden');
        setTimeout(() => {
            content.classList.remove('scale-95', 'opacity-0');
            content.classList.add('scale-100', 'opacity-100');
        }, 10);
    }
    
    // Cerrar modal
    function cerrar() {
        const modal = document.getElementById('custom-modal');
        const content = document.getElementById('modal-content');
        
        if (!modal) return;
        
        content.classList.remove('scale-100', 'opacity-100');
        content.classList.add('scale-95', 'opacity-0');
        
        setTimeout(() => {
            modal.classList.add('hidden');
        }, 300);
    }
    
    // API pública
    window.CustomModal = {
        success: function(mensaje, titulo) {
            mostrar('success', titulo, mensaje);
        },
        error: function(mensaje, titulo) {
            mostrar('error', titulo, mensaje);
        },
        confirm: function(mensaje, titulo, callback) {
            mostrar('confirm', titulo, mensaje, callback);
        },
        cerrar: cerrar
    };
    
    // Reemplazar alert nativo (opcional, para compatibilidad)
    const originalAlert = window.alert;
    window.alert = function(mensaje) {
        // Detectar si es éxito o error por el contenido
        if (mensaje.includes('✅') || mensaje.includes('exitosamente') || mensaje.includes('Éxito')) {
            CustomModal.success(mensaje.replace('✅', '').trim());
        } else if (mensaje.includes('❌') || mensaje.includes('Error')) {
            CustomModal.error(mensaje.replace('❌', '').trim());
        } else {
            CustomModal.success(mensaje);
        }
    };
    
    // Reemplazar confirm nativo (mantener compatibilidad pero usar modal cuando esté disponible)
    const originalConfirm = window.confirm;
    window.confirm = function(mensaje) {
        // Si el modal está disponible, usarlo pero mantener compatibilidad síncrona
        // Nota: confirm nativo es síncrono, pero nuestro modal es asíncrono
        // Por compatibilidad, solo reemplazamos si se llama desde nuestro código
        // que ya maneja callbacks
        return originalConfirm(mensaje);
    };
})();

