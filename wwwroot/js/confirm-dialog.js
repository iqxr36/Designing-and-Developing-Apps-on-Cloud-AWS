(() => {
    const ready = (callback) => {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', callback);
        } else {
            callback();
        }
    };

    ready(() => {
        const dialog = document.querySelector('[data-confirm-dialog]');
        if (!dialog) return;

        const panel = dialog.querySelector('.app-confirm__panel');
        const title = dialog.querySelector('#appConfirmTitle');
        const message = dialog.querySelector('#appConfirmMessage');
        const submitButton = dialog.querySelector('[data-confirm-submit]');
        const cancelButtons = dialog.querySelectorAll('[data-confirm-cancel]');
        let pendingAction = null;
        let previousFocus = null;

        const hasConfirm = (element) => element && (
            element.hasAttribute('data-confirm-title') ||
            element.hasAttribute('data-confirm-message') ||
            element.hasAttribute('data-confirm-action')
        );

        const getText = (element, key, fallback) => {
            if (!element) return fallback;
            const value = element.getAttribute(key);
            return value && value.trim() ? value : fallback;
        };

        const openDialog = (trigger, action) => {
            previousFocus = document.activeElement;
            pendingAction = action;
            title.textContent = getText(trigger, 'data-confirm-title', 'Confirm action');
            message.textContent = getText(trigger, 'data-confirm-message', 'Are you sure you want to continue?');
            submitButton.textContent = getText(trigger, 'data-confirm-action', 'Confirm');
            dialog.hidden = false;
            document.body.classList.add('app-confirm-open');
            window.setTimeout(() => panel.focus(), 0);
        };

        const closeDialog = () => {
            dialog.hidden = true;
            document.body.classList.remove('app-confirm-open');
            pendingAction = null;
            if (previousFocus && typeof previousFocus.focus === 'function') {
                previousFocus.focus();
            }
        };

        const submitForm = (form, submitter) => {
            form.dataset.appConfirmAccepted = 'true';
            if (typeof form.requestSubmit === 'function') {
                form.requestSubmit(submitter || undefined);
                return;
            }
            form.submit();
        };

        document.addEventListener('click', (event) => {
            const trigger = event.target.closest('[data-confirm-title], [data-confirm-message], [data-confirm-action]');
            if (!trigger || trigger.tagName === 'FORM') return;

            const href = trigger.getAttribute('href');
            const form = trigger.form;
            const isSubmitter = form && (trigger.type === 'submit' || trigger.getAttribute('type') === 'submit');

            if (!href && !isSubmitter) return;

            event.preventDefault();
            openDialog(trigger, () => {
                if (isSubmitter) {
                    submitForm(form, trigger);
                    return;
                }

                window.location.href = href;
            });
        });

        document.addEventListener('submit', (event) => {
            const form = event.target;
            if (!hasConfirm(form) || form.dataset.appConfirmAccepted === 'true') {
                if (form.dataset.appConfirmAccepted === 'true') {
                    delete form.dataset.appConfirmAccepted;
                }
                return;
            }

            event.preventDefault();
            openDialog(form, () => submitForm(form, event.submitter));
        });

        submitButton.addEventListener('click', () => {
            const action = pendingAction;
            closeDialog();
            if (action) action();
        });

        cancelButtons.forEach((button) => button.addEventListener('click', closeDialog));

        document.addEventListener('keydown', (event) => {
            if (event.key === 'Escape' && !dialog.hidden) {
                closeDialog();
            }
        });
    });
})();
