(() => {
    const openButton = document.getElementById('feedback-open');
    const dialog = document.getElementById('feedback-dialog');
    const form = document.getElementById('feedback-form');
    const messageInput = document.getElementById('feedback-message');
    const status = document.getElementById('feedback-status');
    const submitButton = document.getElementById('feedback-submit');

    if (!openButton || !dialog || !form || !messageInput || !status || !submitButton) {
        return;
    }

    let lastFocusedElement = null;
    const minMessageLength = Number(messageInput.getAttribute('minlength')) || 5;

    function setStatus(message, type = '') {
        status.textContent = message;
        status.classList.toggle('is-error', type === 'error');
        status.classList.toggle('is-success', type === 'success');
    }

    function setSubmitting(isSubmitting) {
        submitButton.disabled = isSubmitting;
        submitButton.innerHTML = isSubmitting
            ? '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span><span>Sending</span>'
            : '<i class="bi bi-send" aria-hidden="true"></i><span>Send feedback</span>';
    }

    function openDialog() {
        lastFocusedElement = document.activeElement;
        dialog.hidden = false;
        document.body.classList.add('is-feedback-open');
        setStatus('');
        window.requestAnimationFrame(() => messageInput.focus());
    }

    function closeDialog() {
        dialog.hidden = true;
        document.body.classList.remove('is-feedback-open');
        setStatus('');

        if (lastFocusedElement instanceof HTMLElement) {
            lastFocusedElement.focus();
        }
    }

    async function submitFeedback(event) {
        event.preventDefault();

        const message = messageInput.value.trim();

        if (!message) {
            setStatus('Write a message before sending.', 'error');
            messageInput.focus();
            return;
        }

        if (message.length < minMessageLength) {
            setStatus(`Write at least ${minMessageLength} characters.`, 'error');
            messageInput.focus();
            return;
        }

        setSubmitting(true);
        setStatus('');

        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });
            const result = await response.json().catch(() => ({}));
            const message = result.message || 'Feedback is temporarily unavailable. Please try again later.';

            if (!response.ok || result.isOk === false) {
                setStatus(message, 'error');
                return;
            }

            form.reset();
            setStatus(message, 'success');
            window.setTimeout(closeDialog, 1200);
        } catch {
            setStatus('Feedback is temporarily unavailable. Please try again later.', 'error');
        } finally {
            setSubmitting(false);
        }
    }

    openButton.addEventListener('click', openDialog);
    form.addEventListener('submit', submitFeedback);

    dialog.querySelectorAll('[data-feedback-close]').forEach(element => {
        element.addEventListener('click', closeDialog);
    });

    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && !dialog.hidden) {
            closeDialog();
        }
    });
})();
