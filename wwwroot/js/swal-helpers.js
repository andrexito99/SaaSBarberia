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
window.confirm = async function (message) {
    const result = await SwalDarkLuxury.fire({
        icon: 'question',
        title: message,
        showCancelButton: true,
        confirmButtonText: 'Sí',
        cancelButtonText: 'No'
    });
    return result.isConfirmed;
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

document.addEventListener('DOMContentLoaded', renderTempDataToasts);
