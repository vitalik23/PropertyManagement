(function ($) {
    'use strict';

    var $grid = $('#applications-grid');
    if (!$grid.length) {
        return;
    }

    var apiUrl = $grid.data('api-url');
    var detailUrlTemplate = $grid.data('detail-url-template');
    var isPm = $grid.data('is-pm') === true || $grid.data('is-pm') === 'true';
    var canWithdraw = $grid.data('can-withdraw') === true || $grid.data('can-withdraw') === 'true';

    var state = {
        status: '',
        propertyId: '',
        sortBy: 'createdAt',
        sortDir: 'desc',
        page: 1,
        pageSize: 10
    };

    function escapeHtml(value) {
        return $('<div>').text(value == null ? '' : value).html();
    }

    function rowActionsHtml(row) {
        var actions = '';
        if (isPm) {
            actions += '<a href="' + detailUrlTemplate + row.id + '" class="btn btn-sm btn-outline-primary">View</a>';
        } else {
            var label = (row.status === 'Draft' || row.status === 'Returned') ? 'Continue' : 'View';
            actions += '<a href="' + detailUrlTemplate + row.id + '" class="btn btn-sm btn-outline-primary">' + label + '</a>';
            if (canWithdraw && (row.status === 'Draft' || row.status === 'Submitted' || row.status === 'Returned')) {
                actions += ' <a href="#" class="btn btn-sm btn-outline-danger" data-modal-url="/Applications/Withdraw/' + row.id + '">Withdraw</a>';
            }
        }
        return actions;
    }

    function renderRows(rows) {
        var $body = $('#applications-grid-body');
        $body.empty();

        if (rows.length === 0) {
            var colspan = isPm ? 5 : 4;
            $body.append('<tr><td colspan="' + colspan + '" class="text-center text-muted">No applications match this filter.</td></tr>');
            return;
        }

        rows.forEach(function (row) {
            var cells = '<td>' + escapeHtml(row.propertyName) + '</td>' +
                '<td>' + escapeHtml(row.unitNumber) + '</td>';
            if (isPm) {
                cells += '<td>' + escapeHtml(row.applicantFullName) + ' (' + escapeHtml(row.applicantEmail) + ')</td>';
            }
            cells += '<td>' + escapeHtml(row.status) + '</td>' +
                '<td class="text-end">' + rowActionsHtml(row) + '</td>';
            $body.append('<tr>' + cells + '</tr>');
        });
    }

    function fetchGrid() {
        $.getJSON(apiUrl, {
            status: state.status,
            propertyId: state.propertyId,
            sortBy: state.sortBy,
            sortDir: state.sortDir,
            page: state.page,
            pageSize: state.pageSize
        }).done(function (data) {
            renderRows(data.rows);

            var start = data.totalCount === 0 ? 0 : (state.page - 1) * state.pageSize + 1;
            var end = Math.min(state.page * state.pageSize, data.totalCount);
            $('#applications-grid-summary').text('Showing ' + start + '–' + end + ' of ' + data.totalCount);

            $grid.find('[data-grid-page="prev"]').prop('disabled', state.page <= 1);
            $grid.find('[data-grid-page="next"]').prop('disabled', end >= data.totalCount);
        });
    }

    $grid.on('change', '[data-grid-filter]', function () {
        var key = $(this).data('grid-filter');
        state[key] = $(this).val();
        state.page = 1;
        fetchGrid();
    });

    $grid.on('click', '[data-grid-sort]', function () {
        var column = $(this).data('grid-sort');
        if (state.sortBy === column) {
            state.sortDir = state.sortDir === 'asc' ? 'desc' : 'asc';
        } else {
            state.sortBy = column;
            state.sortDir = 'asc';
        }
        state.page = 1;
        fetchGrid();
    });

    $grid.on('click', '[data-grid-page="prev"]', function () {
        if (state.page > 1) {
            state.page -= 1;
            fetchGrid();
        }
    });

    $grid.on('click', '[data-grid-page="next"]', function () {
        state.page += 1;
        fetchGrid();
    });

    $('#shared-modal').on('hidden.bs.modal', function () {
        fetchGrid();
    });

    fetchGrid();
})(jQuery);
