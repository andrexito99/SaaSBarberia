// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Shared, client-side listing controls. They intentionally operate only on
// views marked with data-listing, so dashboards and forms remain untouched.
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-listing]').forEach((container, listingIndex) => {
        const table = container.querySelector('table');
        const grid = container.querySelector('[data-listing-grid]');
        const items = table
            ? Array.from(table.querySelectorAll('tbody > tr')).filter(row => !row.querySelector('[colspan]'))
            : grid ? Array.from(grid.children).filter(item => item.matches('.col')) : [];
        if (items.length === 0) return;

        const pageSize = 10;
        const key = listingIndex === 0 ? '' : `_${listingIndex + 1}`;
        const params = new URLSearchParams(window.location.search);
        let filter = params.get(`buscar${key}`) || '';
        let page = Math.max(1, Number.parseInt(params.get(`pagina${key}`), 10) || 1);

        const toolbar = document.createElement('div');
        toolbar.className = 'd-flex flex-column flex-md-row gap-2 justify-content-between align-items-md-center mb-3 listing-toolbar';
        toolbar.innerHTML = `<input type="search" class="form-control" style="max-width: 360px" placeholder="Buscar en el listado..." aria-label="Buscar" value="${filter.replace(/&/g, '&amp;').replace(/"/g, '&quot;')}"><small class="text-muted listing-summary"></small>`;
        container.insertBefore(toolbar, table || grid);
        const input = toolbar.querySelector('input');
        const summary = toolbar.querySelector('.listing-summary');
        const pager = document.createElement('nav');
        pager.className = 'd-flex flex-wrap justify-content-between align-items-center gap-2 mt-3 listing-pager';
        container.appendChild(pager);

        const writeState = () => {
            const url = new URL(window.location.href);
            if (filter) url.searchParams.set(`buscar${key}`, filter); else url.searchParams.delete(`buscar${key}`);
            url.searchParams.set(`pagina${key}`, page);
            window.history.replaceState({}, '', url);
        };
        const button = (label, target, disabled = false, active = false) =>
            `<button type="button" class="btn btn-sm ${active ? 'btn-primary' : 'btn-outline-secondary'}" data-page="${target}" ${disabled ? 'disabled' : ''}>${label}</button>`;
        const render = () => {
            const normalized = filter.trim().toLocaleLowerCase();
            const filtered = normalized ? items.filter(item => item.textContent.toLocaleLowerCase().includes(normalized)) : items;
            const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
            page = Math.min(page, totalPages);
            const start = (page - 1) * pageSize;
            items.forEach(item => { item.style.display = filtered.slice(start, start + pageSize).includes(item) ? '' : 'none'; });
            summary.textContent = `${filtered.length} de ${items.length} registros · Página ${page} de ${totalPages}`;
            const pages = Array.from({ length: totalPages }, (_, index) => index + 1)
                .map(number => button(number, number, false, number === page)).join('');
            pager.innerHTML = `<span class="small text-muted">Total: ${filtered.length} registros</span><div class="btn-group" role="group">${button('Primera', 1, page === 1)}${button('Anterior', page - 1, page === 1)}${pages}${button('Siguiente', page + 1, page === totalPages)}${button('Última', totalPages, page === totalPages)}</div>`;
            writeState();
        };
        input.addEventListener('input', () => { filter = input.value; page = 1; render(); });
        pager.addEventListener('click', event => {
            const target = event.target.closest('[data-page]');
            if (!target || target.disabled) return;
            page = Number.parseInt(target.dataset.page, 10); render();
        });
        render();
    });
});
