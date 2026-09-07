document.addEventListener('DOMContentLoaded', () => {
  const toggles = document.querySelectorAll('[data-password-toggle]');

  toggles.forEach((toggle) => {
    const inputId = toggle.getAttribute('data-password-toggle');
    const input = inputId
      ? document.getElementById(inputId)
      : toggle.parentElement?.querySelector('input');
    const icon = toggle.querySelector('[data-password-toggle-icon]');

    if (!(input instanceof HTMLInputElement)) {
      return;
    }

    const updateState = () => {
      const isHidden = input.type === 'password';
      toggle.setAttribute('aria-pressed', String(!isHidden));
      toggle.setAttribute(
        'aria-label',
        isHidden ? 'Show password' : 'Hide password',
      );

      if (icon) {
        icon.textContent = isHidden ? 'visibility' : 'visibility_off';
      }
    };

    toggle.addEventListener('click', () => {
      input.type = input.type === 'password' ? 'text' : 'password';
      updateState();
    });

    updateState();
  });
});
