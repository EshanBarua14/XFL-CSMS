/* Xpert CSMS - the ticket lists and the ticket report (Views/Shared/TicketExplorer.cshtml).
   - detailed table from the JSON the server wrote into the page (DataTables, deferred rendering)
   - "Columns" button to show / hide fields (remembered per page in this browser), per-column search row,
     click a row's + for every field of the ticket
   - Copy / CSV / Excel / PDF / Print of the visible columns and the rows that pass the searches
   - report charts (xfl-charts.js) and sortable / exportable breakdown tables
   - filter form: select2 pickers, period <-> custom dates */
(function () {
    'use strict';
    var $ = window.jQuery;
    var root = document.querySelector('.xfl-explorer');
    if (!root || !$ || !$.fn.DataTable) { return; }

    var ctl = root.getAttribute('data-controller');
    var role = root.getAttribute('data-role');
    var canDelete = root.getAttribute('data-can-delete') === '1';
    var pageTitle = root.getAttribute('data-title') || 'Tickets';
    var generated = root.getAttribute('data-generated') || '';

    function json(id) {
        var el = document.getElementById(id);
        if (!el) { return null; }
        try { return JSON.parse(el.textContent || 'null'); } catch (e) { return null; }
    }
    function esc(v) {
        return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; });
    }
    var MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    function pad(n) { return n < 10 ? '0' + n : '' + n; }
    function fmtDate(iso) {
        if (!iso) { return ''; }
        var m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(iso);
        return m ? (m[3] + ' ' + MONTHS[+m[2] - 1] + ' ' + m[1] + ' ' + m[4] + ':' + m[5]) : iso;
    }
    function dur(h) {
        if (h == null || h === '') { return ''; }
        h = +h;
        if (h < 1) { return Math.round(h * 60) + 'm'; }
        if (h < 48) { return Math.floor(h) + 'h ' + pad(Math.round((h - Math.floor(h)) * 60) % 60) + 'm'; }
        return Math.floor(h / 24) + 'd ' + Math.round(h % 24) + 'h';
    }
    var statusClass = { 'Open': 'primary', 'Inqueue': 'warning', 'Inprogress': 'purple', 'Close': 'success' };
    var priorityClass = { 'High': 'danger', 'Medium': 'warning', 'Low': 'info' };
    var slaClass = { 'Met': 'success', 'Breached': 'danger', 'At risk': 'warning', 'On track': 'info', 'Pending': 'secondary' };
    function badge(text, cls) { return text ? '<span class="badge badge-' + (cls || 'secondary') + ' xfl-badge">' + esc(text) + '</span>' : ''; }
    function stars(n) { return n ? '<span class="xfl-stars" title="' + n + ' of 5">' + '★★★★★'.slice(0, n) + '<span class="o">' + '★★★★★'.slice(n) + '</span></span>' : ''; }

    // ---------- filter form ----------
    if ($.fn.select2) {
        $('.xfl-select2').each(function () {
            var $s = $(this);
            $s.select2({ theme: 'bootstrap4', width: '100%', placeholder: $s.data('placeholder') || '', allowClear: !!$s.data('allow-clear') || $s.prop('multiple'), closeOnSelect: !$s.prop('multiple') });
        });
    }
    var period = document.getElementById('f-period');
    var from = document.getElementById('f-from');
    var to = document.getElementById('f-to');
    if (period && from && to) {
        period.addEventListener('change', function () {
            if (period.value !== 'custom') { from.value = ''; to.value = ''; }
            else { from.focus(); }
        });
        [from, to].forEach(function (inp) { inp.addEventListener('change', function () { if (inp.value) { period.value = 'custom'; } }); });
        var form = document.getElementById('xfl-filter-form');
        form && form.addEventListener('submit', function () {
            if (period.value === 'custom') { period.disabled = true; }       // dates win
            else if (period.value) { from.disabled = true; to.disabled = true; }
            // empty fields stay out of the address so links stay short
            Array.prototype.forEach.call(form.elements, function (e) { if (e.name && !e.disabled && e.value === '' && e.tagName !== 'BUTTON' && !e.multiple) { e.disabled = true; } });
        });
    }

    // ---------- export settings shared by every table ----------
    var filtersText = (json('xfl-filters-text') || []).join(' · ') || 'No filter';
    function buttonsConfig(title, landscape, skipColvis) {
        var list = exportButtons(title, landscape);
        return { dom: { button: { className: 'btn btn-sm btn-light border' } }, buttons: skipColvis ? list.slice(1) : list };
    }
    function exportButtons(title, landscape) {
        var opts = { columns: ':visible:not(.no-export)', orthogonal: 'export' };
        var top = 'Filters: ' + filtersText + '   |   Generated: ' + generated;
        return [
            { extend: 'colvis', text: '<i class="fas fa-columns mr-1"></i>Columns', columns: ':not(.no-colvis)', postfixButtons: ['colvisRestore'] },
            { extend: 'copyHtml5', text: '<i class="fas fa-copy mr-1"></i>Copy', exportOptions: opts, title: title },
            { extend: 'csvHtml5', text: '<i class="fas fa-file-csv mr-1"></i>CSV', exportOptions: opts, title: title, filename: fileName(title) },
            { extend: 'excelHtml5', text: '<i class="fas fa-file-excel mr-1"></i>Excel', exportOptions: opts, title: title, messageTop: top, filename: fileName(title), autoFilter: true, sheetName: 'Tickets' },
            {
                extend: 'pdfHtml5', text: '<i class="fas fa-file-pdf mr-1"></i>PDF', exportOptions: opts, title: title, messageTop: top,
                filename: fileName(title), orientation: landscape ? 'landscape' : 'portrait', pageSize: landscape ? 'A3' : 'A4',
                customize: function (doc) {
                    doc.defaultStyle.fontSize = 7; doc.styles.tableHeader.fontSize = 7.5; doc.styles.tableHeader.fillColor = '#1f3b73';
                    doc.pageMargins = [20, 30, 20, 30];
                    doc.footer = function (page, pages) { return { text: 'Xpert CSMS · ' + title + ' · page ' + page + ' of ' + pages, alignment: 'center', fontSize: 7, margin: [0, 10, 0, 0] }; };
                }
            },
            { extend: 'print', text: '<i class="fas fa-print mr-1"></i>Print', exportOptions: opts, title: title, messageTop: '<p style="font-size:12px">' + esc(top) + '</p>' }
        ];
    }
    function fileName(title) {
        var d = new Date();
        return ('XFL-CSMS ' + title + ' ' + d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate())).replace(/[^\w\- ]+/g, '').replace(/\s+/g, '-');
    }
    var DOM = "<'row align-items-center'<'col-md-8 mb-2'B><'col-md-4 mb-2'f>>" + "<'row'<'col-12 table-responsive'tr>>" + "<'row align-items-center mt-2'<'col-md-4'l><'col-md-4'i><'col-md-4'p>>";
    var LANG = { search: '', searchPlaceholder: 'Search all columns', lengthMenu: 'Show _MENU_', info: '_START_–_END_ of _TOTAL_', infoFiltered: '(filtered from _MAX_)', emptyTable: 'No tickets match the filters.', zeroRecords: 'No tickets match the search.' };

    // ---------- detailed ticket table ----------
    var rows = json('xfl-rows') || [];
    var link = function (action, id) { return '/' + ctl + '/' + action + '/' + id; };
    function dateCol(field, title, visible) {
        return { data: field, title: title, visible: visible, className: 'text-nowrap', render: function (v, type) { return type === 'display' || type === 'export' ? fmtDate(v) : (v || ''); } };
    }
    function durCol(field, title, visible) {
        return { data: field, title: title, visible: visible, className: 'text-nowrap text-right', render: function (v, type) { return type === 'display' || type === 'export' ? dur(v) : (v == null ? -1 : v); } };
    }
    var maker = role === 'Maker';
    var columns = [
        { data: null, title: '', orderable: false, searchable: false, className: 'xfl-expand no-export no-colvis', defaultContent: '<button type="button" class="btn btn-link btn-sm p-0" title="Show every field"><i class="fas fa-plus-circle"></i></button>', width: '18px' },
        { data: 'number', title: 'Ticket', className: 'text-nowrap', render: function (v, type, r) { return type === 'display' ? '<a href="' + link('TicketView', r.id) + '"><b>' + esc(v) + '</b></a>' : v; } },
        { data: 'title', title: 'Title', render: function (v, type) { return type === 'display' ? '<span class="xfl-title-cell" title="' + esc(v) + '">' + esc(v) + '</span>' : v; } },
        { data: 'statusLabel', title: 'Status', className: 'xfl-select-filter', render: function (v, type, r) { return type === 'display' ? badge(v, statusClass[r.status]) : v; } },
        { data: 'priority', title: 'Priority', className: 'xfl-select-filter', render: function (v, type) { return type === 'display' ? badge(v, priorityClass[v]) : v; } },
        { data: 'product', title: 'Product', className: 'xfl-select-filter' },
        { data: 'house', title: 'Brokerage house', visible: !maker, className: 'xfl-select-filter' + (maker ? ' no-colvis' : '') },
        { data: 'branch', title: 'Branch', visible: false, className: 'xfl-select-filter' },
        { data: 'raisedBy', title: 'Raised by', visible: !maker },
        { data: 'supportType', title: 'Support type', visible: false, className: 'xfl-select-filter' },
        { data: 'category', title: 'Category', visible: false, className: 'xfl-select-filter' },
        { data: 'subCategory', title: 'Sub-category', visible: false, className: 'xfl-select-filter' },
        { data: 'section', title: 'Affected section', visible: false, className: 'xfl-select-filter' },
        { data: 'engineer', title: 'Engineer', className: 'xfl-select-filter', render: function (v, type) { return type === 'display' && !v ? '<span class="text-danger small">Unassigned</span>' : v; } },
        dateCol('created', 'Created', true),
        dateCol('assigned', 'Assigned', false),
        dateCol('firstResponse', 'First response', false),
        dateCol('updated', 'Updated', false),
        { data: 'updatedBy', title: 'Updated by', visible: false },
        dateCol('closed', 'Closed', false),
        { data: 'closedBy', title: 'Closed by', visible: false },
        durCol('ageHours', 'Age', true),
        durCol('responseHours', 'Response time', false),
        durCol('resolutionHours', 'Resolution time', false),
        { data: 'responseSla', title: 'Response SLA', visible: false, className: 'xfl-select-filter', render: function (v, type) { return type === 'display' ? badge(v, slaClass[v]) : v; } },
        { data: 'resolutionSla', title: 'Resolution SLA', className: 'xfl-select-filter', render: function (v, type, r) { return type === 'display' ? badge(v, slaClass[v]) + '<div class="small text-muted">target ' + dur(r.resolutionTargetHours) + '</div>' : v; } },
        { data: 'rating', title: 'Rating', visible: false, className: 'text-nowrap', render: function (v, type) { return type === 'display' ? stars(v) : (v || ''); } },
        { data: 'attachments', title: 'Files', visible: false, className: 'text-right' },
        // support level and development (added after the older columns so their positions stay the same)
        { data: 'levelCode', title: 'Level', visible: !maker, className: 'xfl-select-filter text-nowrap' + (maker ? ' no-colvis' : ''), render: function (v, type, r) { return type === 'display' ? '<span class="xfl-lvl xfl-lvl-sm xfl-lvl-' + r.level + '">' + esc(v) + '</span>' : v; } },
        { data: 'escalations', title: 'Escalations', visible: false, className: 'text-right' },
        { data: 'devStage', title: 'Development', visible: !maker, className: 'xfl-select-filter', render: function (v, type, r) { return type === 'display' && v ? '<span class="xfl-dev-chip' + (r.devOverdue ? ' text-danger' : '') + '">' + esc(v) + '</span>' : v; } },
        { data: 'devOwner', title: 'Dev owner', visible: false, className: 'xfl-select-filter' },
        { data: 'devEta', title: 'Dev ETA', visible: false, className: 'text-nowrap', render: function (v, type, r) { return type === 'display' || type === 'export' ? (v ? fmtDate(v).slice(0, 11) + (r.devOverdue && type === 'display' ? ' <span class="badge badge-danger">late</span>' : '') : '') : (v || ''); } },
        {
            data: null, title: 'Actions', orderable: false, searchable: false, className: 'text-nowrap no-export no-colvis',
            render: function (v, type, r) {
                if (type !== 'display') { return ''; }
                var h = '<a class="btn btn-xs btn-outline-primary mr-1" href="' + link('TicketView', r.id) + '" title="View"><i class="fas fa-eye"></i></a>';
                h += '<a class="btn btn-xs btn-outline-info mr-1" href="' + link('EditTicket', r.id) + '" title="Edit"><i class="fas fa-pencil-alt"></i></a>';
                if (maker && r.isClosed && !r.rating) { h += '<a class="btn btn-xs btn-outline-warning mr-1" href="/Maker/RateTicket/' + r.id + '" title="Rate the support"><i class="fas fa-star"></i></a>'; }
                if (canDelete) { h += '<button type="button" class="btn btn-xs btn-outline-danger" data-delete-url="/Admin/DeleteTicket/' + r.id + '" data-delete-name="' + esc(r.number) + '" title="Delete"><i class="fas fa-trash"></i></button>'; }
                return h;
            }
        }
    ];

    var $table = $('#xfl-tickets');
    if ($table.length) {
        // second header row: one search box / list per column
        var thead = '<thead><tr>' + columns.map(function (c) { return '<th>' + esc(c.title) + '</th>'; }).join('') + '</tr><tr class="xfl-col-filters">' +
            columns.map(function (c) { return '<th></th>'; }).join('') + '</tr></thead>';
        $table.html(thead);

        if (root.getAttribute('data-preset') === 'closed') {
            [19, 20, 23, 26].forEach(function (i) { columns[i].visible = true; });   // closed, closed by, resolution time, rating
            columns[21].visible = false;                                            // age = resolution time here
        }

        var order = [[14, 'desc']];
        if (['mine', 'unassigned', 'levelqueue'].indexOf(root.getAttribute('data-preset')) >= 0) { order = [[21, 'desc']]; }   // oldest first
        if (root.getAttribute('data-preset') === 'closed') { order = [[19, 'desc']]; }

        var dt = $table.DataTable({
            data: rows,
            columns: columns,
            deferRender: true,
            orderCellsTop: true,
            order: order,
            pageLength: 25,
            lengthMenu: [[10, 25, 50, 100, 250, -1], [10, 25, 50, 100, 250, 'All']],
            autoWidth: false,
            dom: DOM,
            language: LANG,
            buttons: buttonsConfig(pageTitle, true),
            stateSave: true,
            stateDuration: 0,
            stateSaveParams: function (settings, data) {
                // remember columns and page size, not the searches (a new visit starts clean)
                data.search.search = '';
                data.start = 0;
                (data.columns || []).forEach(function (c) { c.search.search = ''; });
            },
            createdRow: function (tr, r) {
                if (!r.isClosed && r.resolutionSla === 'Breached') { tr.classList.add('xfl-row-breached'); }
                else if (!r.isClosed && r.resolutionSla === 'At risk') { tr.classList.add('xfl-row-risk'); }
            },
            initComplete: function () {
                var api = this.api();
                api.columns().every(function (i) {
                    var col = this;
                    var def = columns[i];
                    // the cell of this column in the second header row (DataTables keeps it, also while the column is hidden)
                    var layout = api.settings()[0].aoHeader[1];
                    var cell = $(layout && layout[i] ? layout[i].cell : null);
                    if (def.searchable === false) { return; }
                    if ((def.className || '').indexOf('xfl-select-filter') >= 0) {
                        var values = {};
                        col.data().each(function (v) { if (v != null && v !== '') { values[v] = true; } });
                        var sel = $('<select class="form-control form-control-sm"><option value="">All</option></select>');
                        Object.keys(values).sort().forEach(function (v) { sel.append($('<option>').val(v).text(v)); });
                        sel.on('change', function () {
                            var v = $.fn.dataTable.util.escapeRegex(this.value);
                            col.search(v ? '^' + v + '$' : '', true, false).draw();
                        });
                        cell.append(sel);
                    } else {
                        var inp = $('<input type="search" class="form-control form-control-sm" placeholder="Search">');
                        inp.on('input', function () { if (col.search() !== this.value) { col.search(this.value).draw(); } });
                        cell.append(inp);
                    }
                });
            }
        });

        // row details
        $table.on('click', 'td.xfl-expand button', function () {
            var tr = $(this).closest('tr');
            var row = dt.row(tr);
            if (row.child.isShown()) { row.child.hide(); tr.removeClass('shown'); $(this).find('i').attr('class', 'fas fa-plus-circle'); return; }
            var r = row.data();
            var f = [
                ['Ticket', r.number], ['Title', r.title], ['Status', r.statusLabel], ['Priority', r.priority], ['Product', r.product],
                ['Brokerage house', r.house + (r.houseCode ? ' (' + r.houseCode + ')' : '')], ['Branch', r.branch], ['Raised by', r.raisedBy],
                ['Support type', r.supportType], ['Category', r.category], ['Sub-category', r.subCategory], ['Affected section', r.section],
                ['Engineer', r.engineer || 'Unassigned'], ['Created', fmtDate(r.created)], ['Assigned', fmtDate(r.assigned)],
                ['First response', fmtDate(r.firstResponse) + (r.responseHours != null ? ' (' + dur(r.responseHours) + ')' : '')],
                ['Updated', fmtDate(r.updated) + (r.updatedBy ? ' by ' + r.updatedBy : '')],
                ['Closed', fmtDate(r.closed) + (r.closedBy ? ' by ' + r.closedBy : '')],
                ['Age', dur(r.ageHours)], ['Resolution time', dur(r.resolutionHours)],
                ['Response SLA', r.responseSla + ' (target ' + dur(r.responseTargetHours) + ')'],
                ['Resolution SLA', r.resolutionSla + ' (target ' + dur(r.resolutionTargetHours) + ')'],
                ['Client rating', r.rating ? r.rating + ' / 5' : 'Not rated'], ['Attachments', r.attachments],
                ['Support level', r.levelCode + (r.escalations ? ' (escalated ' + r.escalations + '×)' : '')],
                ['Development', r.devStage ? r.devStage + (r.devOwner ? ' · ' + r.devOwner : '') + (r.devEta ? ' · ETA ' + fmtDate(r.devEta).slice(0, 11) : '') : 'None']
            ];
            row.child('<dl class="row xfl-detail mb-0">' + f.map(function (x) {
                return '<dt class="col-sm-4 col-lg-2">' + esc(x[0]) + '</dt><dd class="col-sm-8 col-lg-4">' + (esc(x[1]) || '<span class="text-muted">–</span>') + '</dd>';
            }).join('') + '</dl>').show();
            tr.addClass('shown');
            $(this).find('i').attr('class', 'fas fa-minus-circle');
        });

        // recalculate column widths when the menu or a tab changes the space
        $(document).on('collapsed.lte.pushmenu shown.lte.pushmenu', function () { setTimeout(function () { dt.columns.adjust(); }, 350); });
    }

    // ---------- breakdown tables ----------
    $('.xfl-breakdown').each(function () {
        var title = this.getAttribute('data-export-title') || pageTitle;
        var bt = $(this).DataTable({
            order: [[1, 'desc']], pageLength: 10, lengthMenu: [[10, 25, 100, -1], [10, 25, 100, 'All']], dom: DOM, language: Object.assign({}, LANG, { searchPlaceholder: 'Search' }),
            buttons: buttonsConfig(title, false, true), autoWidth: false
        });
        $(this).data('dt', bt);
    });
    $('a[data-toggle="tab"]').on('shown.bs.tab', function () { $.fn.dataTable.tables({ visible: true, api: true }).columns.adjust(); });

    // ---------- charts (report) ----------
    var d = json('xfl-chart-data');
    if (d && window.XflCharts && window.Chart) {
        var X = window.XflCharts, K = X.colors;
        X.line('ch-trend', d.trend.labels, [
            { label: 'Created', data: d.trend.created, color: K.blue },
            { label: 'Closed', data: d.trend.closed, color: K.green }
        ]);
        X.doughnut('ch-status', d.status.labels, d.status.values, d.status.labels.map(X.statusColor));
        X.bar('ch-priority', d.priority.labels, [
            { label: 'Open', data: d.priority.open, colors: d.priority.labels.map(X.priorityColor) },
            { label: 'Closed', data: d.priority.closed, color: K.slate },
            { label: 'SLA met %', data: d.priority.sla, color: K.green, percent: true }
        ], { stacked: true });
        X.bar('ch-aging', d.aging.labels, [{ label: 'Open tickets', data: d.aging.values, colors: [K.green, K.teal, K.amber, K.orange, K.red] }]);
        X.bar('ch-product', d.product.labels, [
            { label: 'Open', data: d.product.open, color: K.blue },
            { label: 'Closed', data: d.product.closed, color: K.green }
        ], { stacked: true });
        X.bar('ch-house', d.house.labels, [
            { label: 'Open', data: d.house.open, color: K.blue },
            { label: 'Closed', data: d.house.closed, color: K.green }
        ], { stacked: true, horizontal: true });
        X.bar('ch-engineer', d.engineer.labels, [
            { label: 'Open', data: d.engineer.open, color: K.amber },
            { label: 'Closed', data: d.engineer.closed, color: K.green },
            { label: 'SLA met %', data: d.engineer.sla, color: K.purple, percent: true }
        ], { stacked: true });
        X.bar('ch-category', d.category.labels, [{ label: 'Tickets', data: d.category.values, color: K.teal }], { horizontal: true });
        if (d.level) {
            X.bar('ch-level', d.level.labels, [
                { label: 'Open', data: d.level.open, color: K.amber },
                { label: 'Closed', data: d.level.closed, color: K.green },
                { label: 'SLA met %', data: d.level.sla, color: K.purple, percent: true }
            ], { stacked: true });
        }
        if (d.dev) {
            X.bar('ch-dev', d.dev.labels, [{ label: 'Tickets', data: d.dev.values, color: K.purple }], { horizontal: true });
        }
    }
})();
