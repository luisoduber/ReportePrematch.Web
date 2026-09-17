/**
 * export-helpers.js
 * Motor genérico de exportación Excel + PDF para todos los reportes.
 * Requiere: xlsx-js-style, jsPDF 2.5.1, jspdf-autotable 3.8.2
 */
(function (window) {
    'use strict';

    // ── Formato numérico ──────────────────────────────────────────────────────
    function fmtMoney(n) {
        var v = parseFloat(n || 0);
        return v.toLocaleString('es-VE', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }
    function fmtInt(n) {
        return parseInt(n || 0).toLocaleString('es-VE');
    }
    function fmtPct(n) {
        return parseFloat(n || 0).toFixed(2) + '%';
    }
    function numOf(v) { return parseFloat(v || 0); }

    // ── Excel genérico ────────────────────────────────────────────────────────
    function exportarExcel(lastData, config) {
        if (!lastData || !lastData.length) { alert('Sin datos para exportar.'); return; }
        var XLSX = window.XLSX;
        if (!XLSX) { alert('Librería Excel no cargada.'); return; }

        var cols  = config.cols;
        var nCols = cols.length;
        var rows  = [];

        // Cabecera
        rows.push(cols.map(function (c) { return c.head; }));

        // Datos
        lastData.forEach(function (r) {
            rows.push(cols.map(function (c) {
                var v = c.get(r);
                return (c.type === 'money' || c.type === 'int') ? numOf(v) : (v == null ? '—' : String(v));
            }));
        });

        // Totales
        var totRow = cols.map(function (c, i) {
            if (i === 0) return 'TOTAL';
            if (c.type === 'money' || c.type === 'int') {
                return lastData.reduce(function (s, r) { return s + numOf(c.get(r)); }, 0);
            }
            return '';
        });
        rows.push(totRow);

        var ws = XLSX.utils.aoa_to_sheet(rows);
        var nRows = rows.length;

        // Anchos de columna
        ws['!cols'] = cols.map(function () { return { wch: 18 }; });

        // Estilos
        for (var ri = 0; ri < nRows; ri++) {
            for (var ci = 0; ci < nCols; ci++) {
                var ref  = XLSX.utils.encode_cell({ r: ri, c: ci });
                if (!ws[ref]) ws[ref] = { v: '', t: 's' };
                var col  = cols[ci];
                var isHd = ri === 0;
                var isTt = ri === nRows - 1;
                var s    = { alignment: { horizontal: 'center', vertical: 'center' } };
                if (isHd) {
                    s.font = { bold: true, color: { rgb: 'FFFFFF' }, sz: 11 };
                    s.fill = { fgColor: { rgb: '003D6B' } };
                    s.border = { bottom: { style: 'medium', color: { rgb: '00B0D0' } } };
                } else if (isTt) {
                    s.font = { bold: true, color: { rgb: '00E5FF' }, sz: 10 };
                    s.fill = { fgColor: { rgb: '001830' } };
                    s.border = { top: { style: 'medium', color: { rgb: '00B0D0' } } };
                } else {
                    s.font = { sz: 10 };
                }
                if (!isHd && col.type === 'money') s.numFmt = '#,##0.00';
                if (!isHd && col.type === 'int')   s.numFmt = '#,##0';
                ws[ref].s = s;
            }
        }

        var wb = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(wb, ws, (config.reportName || 'Reporte').substring(0, 31));
        XLSX.writeFile(wb, (config.reportName || 'Reporte') + '.xlsx');
    }

    // ── PDF genérico ──────────────────────────────────────────────────────────
    var CARD_GRAYS = [200, 185, 170, 155, 140, 125, 110, 95, 80, 70];

    function exportarPDF(lastData, config, fechaD, fechaH) {
        if (!lastData || !lastData.length) { alert('Sin datos para exportar.'); return; }
        if (!window.jspdf) { alert('Librería PDF no cargada.'); return; }

        var cols      = config.cols;
        var moneyCols = cols.filter(function (c) { return c.type === 'money' || c.type === 'int'; });
        var doc       = new window.jspdf.jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' });
        var pW        = doc.internal.pageSize.getWidth();
        var pH        = doc.internal.pageSize.getHeight();

        // Agrupar por agente
        var grupos = {}, orden = [];
        lastData.forEach(function (r) {
            var ag = config.groupField(r);
            if (!grupos[ag]) { grupos[ag] = []; orden.push(ag); }
            grupos[ag].push(r);
        });

        // Encabezado de página
        doc.setFillColor(40);
        doc.rect(0, 0, pW, 22, 'F');
        doc.setFontSize(13); doc.setTextColor(255);
        doc.text(config.reportName || 'Reporte', 10, 10);
        doc.setFontSize(8); doc.setTextColor(200);
        var periodo = (fechaD && fechaH) ? ('Período: ' + fechaD + ' al ' + fechaH + '   ·   ') : '';
        doc.text(periodo + 'Generado: ' + new Date().toLocaleString('es-VE'), 10, 17);

        var y = 28;

        orden.forEach(function (ag) {
            var rows = grupos[ag];

            // Totales por columna money
            var sums = {};
            moneyCols.forEach(function (c) {
                sums[c.head] = rows.reduce(function (s, r) { return s + numOf(c.get(r)); }, 0);
            });

            // Estimar espacio
            var spaceNeeded = 14 + (moneyCols.length ? 20 : 0) + Math.min(rows.length + 1, 20) * 6 + 12;
            if (y + spaceNeeded > pH - 10) { doc.addPage(); y = 15; }

            // Banda de agente
            doc.setFillColor(220);
            doc.roundedRect(8, y, pW - 16, 8, 2, 2, 'F');
            doc.setFontSize(10); doc.setTextColor(30);
            doc.text('  ' + ag, 12, y + 5.5);
            y += 12;

            // Cards de totales (máx. 9)
            var cardDefs = moneyCols.slice(0, CARD_GRAYS.length);
            if (cardDefs.length > 0) {
                var cardW = (pW - 16) / cardDefs.length;
                cardDefs.forEach(function (c, i) {
                    var cx  = 8 + i * cardW;
                    var bg  = CARD_GRAYS[i];
                    var brd = Math.max(0, bg - 40);
                    var tv  = bg < 130 ? 255 : 30;
                    var tl  = bg < 130 ? 180 : 90;
                    doc.setFillColor(bg);
                    doc.roundedRect(cx + 0.5, y, cardW - 1, 14, 1.5, 1.5, 'F');
                    doc.setDrawColor(brd); doc.setLineWidth(0.25);
                    doc.roundedRect(cx + 0.5, y, cardW - 1, 14, 1.5, 1.5, 'S');
                    doc.setFontSize(6); doc.setTextColor(tl);
                    doc.text(String(c.head).toUpperCase(), cx + cardW / 2, y + 4.5, { align: 'center' });
                    doc.setFontSize(8); doc.setTextColor(tv);
                    var val = c.type === 'int' ? fmtInt(sums[c.head]) : fmtMoney(sums[c.head]);
                    doc.text(val, cx + cardW / 2, y + 10.5, { align: 'center' });
                });
                y += 18;
            }

            // Tabla de detalle
            var head = [cols.map(function (c) { return c.head; })];
            var body = rows.map(function (r) {
                return cols.map(function (c) {
                    var v = c.get(r);
                    if (c.type === 'money') return fmtMoney(v);
                    if (c.type === 'int')   return fmtInt(v);
                    if (c.type === 'pct')   return fmtPct(v);
                    return String(v == null ? '—' : v);
                });
            });
            // Fila TOTAL
            var totRow = cols.map(function (c, i) {
                if (i === 0) return 'TOTAL';
                if (c.type === 'money') return fmtMoney(sums[c.head] || 0);
                if (c.type === 'int')   return fmtInt(sums[c.head] || 0);
                return '';
            });
            body.push(totRow);

            doc.autoTable({
                startY: y,
                head: head,
                body: body,
                headStyles: { fillColor: [60, 60, 60], textColor: [255, 255, 255], fontSize: 7, fontStyle: 'bold', halign: 'center' },
                bodyStyles: { fontSize: 6.5, textColor: [30, 30, 30], fillColor: [255, 255, 255], halign: 'center' },
                alternateRowStyles: { fillColor: [242, 242, 242] },
                didParseCell: function (data) {
                    if (data.row.index === body.length - 1 && data.section === 'body') {
                        data.cell.styles.fontStyle = 'bold';
                        data.cell.styles.textColor = [255, 255, 255];
                        data.cell.styles.fillColor = [70, 70, 70];
                    }
                },
                margin: { left: 8, right: 8 },
                tableWidth: pW - 16
            });

            y = doc.lastAutoTable.finalY + 8;
        });

        doc.save((config.reportName || 'Reporte') + '_' + (fechaD || '') + '_' + (fechaH || '') + '.pdf');
    }

    window.ExportHelpers = {
        exportarExcel: exportarExcel,
        exportarPDF:   exportarPDF
    };

})(window);
