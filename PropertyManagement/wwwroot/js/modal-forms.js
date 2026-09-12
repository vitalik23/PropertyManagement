(function ($) {
    'use strict';

    var $modal = $('#shared-modal');
    var $modalContent = $('#shared-modal-content');

    function bindValidation() {
        var $form = $modalContent.find('form');
        if ($form.length) {
            $form.removeData('validator').removeData('unobtrusiveValidation');
            $.validator.unobtrusive.parse($form);
        }
    }

    function refreshRegion(listUrl, containerSelector) {
        if (!listUrl || !containerSelector) {
            return;
        }
        $.get(listUrl, function (html) {
            $(containerSelector).html(html);
        });
    }

    $(document).on('click', '[data-modal-url]', function (e) {
        e.preventDefault();
        var url = $(this).data('modal-url');
        $.get(url, function (html) {
            $modalContent.html(html);
            bindValidation();
            $modal.modal('show');
        });
    });

    $(document).on('submit', 'form[data-modal-form]', function (e) {
        e.preventDefault();
        var $form = $(this);

        if (!$form.valid()) {
            return;
        }

        $.post({
            url: $form.attr('action'),
            data: $form.serialize()
        }).done(function (response, status, xhr) {
            var contentType = xhr.getResponseHeader('content-type') || '';
            if (contentType.indexOf('application/json') !== -1) {
                $modal.modal('hide');
                refreshRegion($form.data('refresh-url'), $form.data('refresh-target'));
            } else {
                $modalContent.html(response);
                bindValidation();
            }
        });
    });
})(jQuery);
