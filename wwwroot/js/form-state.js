(() => {
    const ready = (callback) => {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', callback);
        } else {
            callback();
        }
    };

    const getSubmitLabel = (button) => {
        const custom = button.getAttribute('data-loading-text');
        if (custom && custom.trim()) return custom.trim();

        const text = button.textContent.replace(/\s+/g, ' ').trim();
        if (!text) return 'Working...';

        const lower = text.toLowerCase();
        if (lower.includes('log out')) return 'Logging out...';
        if (lower.includes('upload')) return 'Uploading...';
        if (lower.includes('save')) return 'Saving...';
        if (lower.includes('submit')) return 'Submitting...';
        if (lower.includes('assign')) return 'Assigning...';
        if (lower.includes('add')) return 'Adding...';
        if (lower.includes('create')) return 'Creating...';
        if (lower.includes('update')) return 'Updating...';
        if (lower.includes('delete') || lower.includes('remove')) return 'Removing...';
        return 'Working...';
    };

    const shouldHandleForm = (form) => {
        if (!form || form.dataset.noSubmitLoading === 'true') return false;
        const method = (form.getAttribute('method') || 'get').toLowerCase();
        return method === 'post';
    };

    const isValidForSubmit = (form, submitter) => {
        const skipsValidation = submitter?.hasAttribute('formnovalidate') || form.noValidate;
        if (skipsValidation) return true;

        if (window.jQuery && window.jQuery.fn?.valid && window.jQuery(form).data('validator')) {
            return window.jQuery(form).valid();
        }

        if (typeof form.checkValidity === 'function') {
            return form.checkValidity();
        }

        return true;
    };

    const setButtonLoading = (button) => {
        if (!button || button.dataset.submitLoading === 'true') return;

        button.dataset.submitLoading = 'true';
        button.dataset.originalHtml = button.innerHTML;
        button.setAttribute('aria-busy', 'true');
        button.classList.add('pm-btn--loading');
        button.innerHTML = `<span class="pm-btn__spinner" aria-hidden="true"></span><span>${getSubmitLabel(button)}</span>`;
    };

    ready(() => {
        document.addEventListener('submit', (event) => {
            if (event.defaultPrevented) return;

            const form = event.target;
            if (!shouldHandleForm(form)) return;
            if (form.dataset.submitLoading === 'true') {
                event.preventDefault();
                return;
            }

            const submitter = event.submitter || form.querySelector('button[type="submit"], input[type="submit"]');
            if (!isValidForSubmit(form, submitter)) return;

            form.dataset.submitLoading = 'true';
            form.setAttribute('aria-busy', 'true');
            form.classList.add('pm-form--submitting');

            const buttons = form.querySelectorAll('button[type="submit"], input[type="submit"]');
            buttons.forEach((button) => {
                if (button === submitter || buttons.length === 1) {
                    setButtonLoading(button);
                }
                button.disabled = true;
            });
        });
    });
})();
