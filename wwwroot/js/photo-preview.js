(() => {
    const ready = (callback) => {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', callback);
        } else {
            callback();
        }
    };

    const buildDialog = () => {
        const dialog = document.createElement('div');
        dialog.className = 'pm-photo-viewer';
        dialog.hidden = true;
        dialog.innerHTML = `
            <div class="pm-photo-viewer__backdrop" data-photo-close></div>
            <section class="pm-photo-viewer__panel" role="dialog" aria-modal="true" aria-labelledby="pmPhotoTitle" tabindex="-1">
                <div class="pm-photo-viewer__header">
                    <div>
                        <p class="pm-photo-viewer__eyebrow" data-photo-type></p>
                        <h2 id="pmPhotoTitle" class="pm-photo-viewer__title" data-photo-title></h2>
                        <p class="pm-photo-viewer__meta" data-photo-meta></p>
                    </div>
                    <button type="button" class="pm-photo-viewer__close" data-photo-close aria-label="Close preview">
                        <span class="material-symbols-outlined">close</span>
                    </button>
                </div>
                <div class="pm-photo-viewer__stage">
                    <button type="button" class="pm-photo-viewer__nav pm-photo-viewer__nav--prev" data-photo-prev aria-label="Previous photo">
                        <span class="material-symbols-outlined">chevron_left</span>
                    </button>
                    <img data-photo-image alt="" />
                    <button type="button" class="pm-photo-viewer__nav pm-photo-viewer__nav--next" data-photo-next aria-label="Next photo">
                        <span class="material-symbols-outlined">chevron_right</span>
                    </button>
                </div>
            </section>`;
        document.body.appendChild(dialog);
        return dialog;
    };

    ready(() => {
        const dialog = buildDialog();
        const panel = dialog.querySelector('.pm-photo-viewer__panel');
        const image = dialog.querySelector('[data-photo-image]');
        const title = dialog.querySelector('[data-photo-title]');
        const type = dialog.querySelector('[data-photo-type]');
        const meta = dialog.querySelector('[data-photo-meta]');
        const prev = dialog.querySelector('[data-photo-prev]');
        const next = dialog.querySelector('[data-photo-next]');
        let items = [];
        let currentIndex = 0;
        let previousFocus = null;

        const getItems = (group) => Array.from(document.querySelectorAll(`[data-photo-preview][data-photo-group="${CSS.escape(group)}"]`));

        const render = () => {
            const item = items[currentIndex];
            if (!item) return;

            const src = item.getAttribute('data-photo-src') || item.getAttribute('href') || item.querySelector('img')?.getAttribute('src') || '';
            const label = item.getAttribute('data-photo-title') || 'Photo preview';
            const typeText = item.getAttribute('data-photo-type') || 'Request photo';
            const uploadedBy = item.getAttribute('data-photo-by');
            const uploadedAt = item.getAttribute('data-photo-time');

            image.src = src;
            image.alt = label;
            title.textContent = label;
            type.textContent = typeText;
            meta.textContent = [uploadedBy ? `Uploaded by ${uploadedBy}` : '', uploadedAt || ''].filter(Boolean).join(' • ');
            prev.hidden = items.length <= 1;
            next.hidden = items.length <= 1;
        };

        const open = (trigger) => {
            const group = trigger.getAttribute('data-photo-group') || 'default';
            items = getItems(group);
            currentIndex = Math.max(items.indexOf(trigger), 0);
            previousFocus = document.activeElement;
            render();
            dialog.hidden = false;
            document.body.classList.add('pm-photo-viewer-open');
            window.setTimeout(() => panel.focus(), 0);
        };

        const close = () => {
            dialog.hidden = true;
            document.body.classList.remove('pm-photo-viewer-open');
            image.removeAttribute('src');
            if (previousFocus && typeof previousFocus.focus === 'function') {
                previousFocus.focus();
            }
        };

        const move = (direction) => {
            if (!items.length) return;
            currentIndex = (currentIndex + direction + items.length) % items.length;
            render();
        };

        document.addEventListener('click', (event) => {
            const trigger = event.target.closest('[data-photo-preview]');
            if (trigger) {
                event.preventDefault();
                open(trigger);
                return;
            }

            if (event.target.closest('[data-photo-close]')) close();
            if (event.target.closest('[data-photo-prev]')) move(-1);
            if (event.target.closest('[data-photo-next]')) move(1);
        });

        document.addEventListener('keydown', (event) => {
            if (dialog.hidden) return;
            if (event.key === 'Escape') close();
            if (event.key === 'ArrowLeft') move(-1);
            if (event.key === 'ArrowRight') move(1);
        });
    });
})();
