/**
 * metric-fit.js — ajusta el tamaño de la cifra de cada card (.metric-value)
 * para que SIEMPRE quede dentro del card, sin importar si supera el millón,
 * en escritorio, tablet o teléfono.
 *
 * - Parte del tamaño definido por el CSS de la vista (no lo agranda nunca).
 * - Si la cifra no cabe, reduce la fuente de a poco hasta que entre (mínimo 9px).
 * - Se ejecuta solo: al cargar la página, al cambiar el tamaño de la ventana /
 *   girar el teléfono, y cada vez que una vista dibuja o actualiza cards
 *   (MutationObserver), así no hay que tocar ninguna vista.
 */
(function () {
    'use strict';

    var MIN_PX  = 9;      // tamaño mínimo legible
    var STEP_PX = 0.5;    // paso de reducción
    var SELECTOR = '.metric-value';

    function fit(el) {
        if (!el || !el.isConnected || el.offsetParent === null) return;   // oculto → se ajusta cuando se muestre

        // Volver al tamaño original del CSS antes de medir
        el.style.fontSize = '';
        var base = parseFloat(window.getComputedStyle(el).fontSize) || 16;
        var size = base;

        // Ancho disponible = ancho interno del card (sin padding)
        var card  = el.closest('.metric-card') || el.parentElement;
        var cs    = window.getComputedStyle(card);
        var avail = card.clientWidth - (parseFloat(cs.paddingLeft) || 0) - (parseFloat(cs.paddingRight) || 0);
        if (avail <= 0) return;

        var guard = 0;
        while (el.scrollWidth > avail && size > MIN_PX && guard < 80) {
            size -= STEP_PX;
            el.style.fontSize = size + 'px';
            guard++;
        }
    }

    function fitAll(root) {
        var scope = root && root.querySelectorAll ? root : document;
        var list = scope.querySelectorAll(SELECTOR);
        for (var i = 0; i < list.length; i++) fit(list[i]);
        if (scope.matches && scope.matches(SELECTOR)) fit(scope);
    }

    // Agrupa muchos cambios seguidos en un solo ajuste por frame
    var pending = false;
    function schedule() {
        if (pending) return;
        pending = true;
        window.requestAnimationFrame(function () {
            pending = false;
            fitAll(document);
        });
    }

    // Cambios de tamaño / orientación
    var resizeTimer = null;
    window.addEventListener('resize', function () {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(schedule, 120);
    });
    window.addEventListener('orientationchange', schedule);

    // Cards creados o actualizados dinámicamente por las vistas (innerHTML, DataTables, etc.)
    function observar() {
        if (!window.MutationObserver || !document.body) return;
        var obs = new MutationObserver(function (mutations) {
            for (var i = 0; i < mutations.length; i++) {
                var m = mutations[i];
                var t = m.target && m.target.nodeType === 1 ? m.target : (m.target && m.target.parentElement);
                if (!t) continue;
                // Solo reaccionar si el cambio toca un card de métricas
                if ((t.closest && t.closest('.metric-card, #globalCards, .quick-metrics-bar')) ||
                    (m.addedNodes && Array.prototype.some.call(m.addedNodes, function (n) {
                        return n.nodeType === 1 && (n.matches(SELECTOR) || n.querySelector && n.querySelector(SELECTOR));
                    }))) {
                    schedule();
                    return;
                }
            }
        });
        obs.observe(document.body, { childList: true, subtree: true, characterData: true });
    }

    // Las fuentes web pueden cambiar el ancho del texto al terminar de cargar
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(schedule);

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { observar(); schedule(); });
    } else {
        observar(); schedule();
    }

    // Disponible por si alguna vista quiere forzarlo manualmente
    window.MetricFit = { fitAll: schedule };
})();
