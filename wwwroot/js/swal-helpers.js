// SweetAlert2 Dark Luxury mixin
const SwalDarkLuxury = Swal.mixin({
    background: '#2c2c2c',
    color: '#fff',
    toast: false,
    width: '30rem',
    customClass: {
        confirmButton: 'btn btn-primary',
        cancelButton: 'btn btn-secondary',
        popup: 'border border-3 border-warning rounded-3 shadow-lg'
    },
    showClass: { popup: 'animate__animated animate__fadeInDown' },
    hideClass: { popup: 'animate__animated animate__fadeOutUp' }
});

// Override native confirm with SweetAlert2
// Override native confirm with SweetAlert2.
// Returns a Promise that resolves to true if confirmed, false otherwise.
// This allows usage with `await confirm(...)` in async contexts.
// For legacy synchronous calls (e.g., onclick="return confirm('...')"), the returned Promise is truthy,
// so we also prevent default navigation and handle the action manually via the resolved value.
window.confirm = function (message) {
    // Show the modal
    const modalPromise = SwalDarkLuxury.fire({
        icon: 'question',
        title: message,
        showCancelButton: true,
        confirmButtonText: 'Sí',
        cancelButtonText: 'No'
    }).then(result => result.isConfirmed);

    // If the call is used synchronously (like in an inline onclick handler), prevent default navigation
    // by returning false. The caller can handle navigation after the Promise resolves if needed.
    try {
        // In event handler context, `this` may be the element; we attempt to stop default action.
        if (window.event && window.event instanceof Event) {
            window.event.preventDefault();
        }
    } catch (e) { /* ignore */ }

    return modalPromise;
};

// Helper for toast notifications (success, error, warning, info)
function swalToast(type, title) {
    SwalDarkLuxury.fire({
        toast: true,
        position: 'top-end',
        icon: type,
        title: title,
        timer: type === 'error' ? 4000 : 3000,
        timerProgressBar: true,
        showConfirmButton: false
    });
}

// Auto‑render toasts from TempData (any of the supported keys)
// Auto‑render toasts from TempData (any of the supported keys).
// This function runs after the 'load' event to guarantee that the inline script in the layout
// (which fills window.tempDataMessages) has already executed.
function renderTempDataToasts() {
    if (typeof window.tempDataMessages !== 'object' || window.tempDataMessages === null) return;
    const keyMap = {
        Mensaje: 'success',
        Success: 'success',
        Exito: 'success',
        Error: 'error'
    };
    for (const [key, type] of Object.entries(keyMap)) {
        const msg = window.tempDataMessages[key];
        if (msg) {
            swalToast(type, msg);
        }
    }
}

// Use 'load' instead of 'DOMContentLoaded' to ensure the TempData script has populated the object.
window.addEventListener('load', renderTempDataToasts);
