/* ============================================================
   busy.js — Overlay de carga y barra de progreso
   ReportePrematch GCIT
   ============================================================ */

(function () {
    'use strict';

    /* ── Referencias DOM ── */
    const overlay = document.getElementById('busy-overlay');
    const progressBar = document.getElementById('progress-bar');
    let progressTimer = null;
    let progressValue = 0;

    /* ──────────────────────────────────────────
       Overlay de carga global
    ────────────────────────────────────────── */

    /**
     * Muestra el overlay de carga.
     * @param {string} [texto='Cargando...'] - Mensaje visible.
     */
    window.showBusy = function (texto) {
        if (!overlay) return;
        const textEl = overlay.querySelector('.busy-text');
        if (textEl) textEl.textContent = texto || 'Cargando...';
        overlay.classList.add('show');
        startProgress();
    };

    /**
     * Oculta el overlay de carga y completa la barra de progreso.
     */
    window.hideBusy = function () {
        if (!overlay) return;
        overlay.classList.remove('show');
        finishProgress();
    };

    /* ──────────────────────────────────────────
       Barra de progreso superior
    ────────────────────────────────────────── */

    function startProgress() {
        if (!progressBar) return;
        clearInterval(progressTimer);
        progressValue = 0;
        progressBar.style.width = '0%';
        progressBar.style.opacity = '1';
        progressBar.style.transition = 'width 0.3s ease';

        // Avance simulado rápido hasta ~85 % y luego se detiene
        progressTimer = setInterval(function () {
            if (progressValue < 85) {
                const increment = progressValue < 30 ? 8
                    : progressValue < 60 ? 4
                    : 1;
                progressValue = Math.min(progressValue + increment, 85);
                progressBar.style.width = progressValue + '%';
            }
        }, 180);
    }

    function finishProgress() {
        if (!progressBar) return;
        clearInterval(progressTimer);
        progressValue = 100;
        progressBar.style.transition = 'width 0.25s ease';
        progressBar.style.width = '100%';

        setTimeout(function () {
            progressBar.style.transition = 'opacity 0.4s ease';
            progressBar.style.opacity = '0';
            setTimeout(function () {
                progressBar.style.width = '0%';
                progressBar.style.opacity = '1';
                progressValue = 0;
            }, 420);
        }, 280);
    }

    /* ──────────────────────────────────────────
       Interceptor global de AJAX (jQuery)
       Activa/desactiva el overlay automáticamente
       en peticiones que duren más de 300 ms.
    ────────────────────────────────────────── */
    if (typeof $ !== 'undefined' && $.ajaxSetup) {
        let ajaxCount = 0;
        let autoShowTimer = null;

        $(document)
            .ajaxSend(function () {
                ajaxCount++;
                // Solo mostrar si el request tarda > 300 ms (evita parpadeos)
                if (ajaxCount === 1) {
                    autoShowTimer = setTimeout(function () {
                        if (ajaxCount > 0 && !overlay.classList.contains('show')) {
                            showBusy('Consultando...');
                        }
                    }, 300);
                }
            })
            .ajaxComplete(function () {
                ajaxCount = Math.max(0, ajaxCount - 1);
                if (ajaxCount === 0) {
                    clearTimeout(autoShowTimer);
                    hideBusy();
                }
            });
    }

    /* ──────────────────────────────────────────
       Reloj en navbar (si existe .nav-clock)
    ────────────────────────────────────────── */
    const clockEl = document.querySelector('.nav-clock');
    if (clockEl) {
        function updateClock() {
            const now = new Date();
            const hh = String(now.getHours()).padStart(2, '0');
            const mm = String(now.getMinutes()).padStart(2, '0');
            const ss = String(now.getSeconds()).padStart(2, '0');
            clockEl.textContent = hh + ':' + mm + ':' + ss;
        }
        updateClock();
        setInterval(updateClock, 1000);
    }

})();
