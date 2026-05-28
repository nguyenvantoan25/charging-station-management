window.TramSacPagination = (function () {
    function resolveElement(value) {
        if (!value) return null;
        if (typeof value === 'string') return document.getElementById(value);
        return value;
    }

    function buildPageList(currentPage, totalPages) {
        const pages = [];

        if (totalPages <= 7) {
            for (let i = 1; i <= totalPages; i++) pages.push(i);
            return pages;
        }

        pages.push(1);

        if (currentPage > 3) {
            pages.push('...');
        }

        const start = Math.max(2, currentPage - 1);
        const end = Math.min(totalPages - 1, currentPage + 1);

        for (let i = start; i <= end; i++) {
            pages.push(i);
        }

        if (currentPage < totalPages - 2) {
            pages.push('...');
        }

        pages.push(totalPages);
        return pages;
    }

    function createPager(options) {
        const state = {
            items: [],
            page: 1,
            pageSize: Number(options.pageSize || 15)
        };

        const paginationElement = resolveElement(options.paginationContainer);
        const infoElement = resolveElement(options.infoContainer);
        const onPageChange = typeof options.onPageChange === 'function'
            ? options.onPageChange
            : function () { };

        function getMeta() {
            const totalItems = state.items.length;
            const totalPages = totalItems === 0 ? 0 : Math.ceil(totalItems / state.pageSize);
            const startIndex = totalItems === 0 ? 0 : ((state.page - 1) * state.pageSize) + 1;
            const endIndex = totalItems === 0 ? 0 : Math.min(state.page * state.pageSize, totalItems);

            return {
                page: state.page,
                pageSize: state.pageSize,
                totalItems,
                totalPages,
                startIndex,
                endIndex
            };
        }

        function renderInfo() {
            if (!infoElement) return;

            const meta = getMeta();

            infoElement.textContent = meta.totalItems === 0
                ? 'Không có dữ liệu.'
                : `Hiển thị ${meta.startIndex}-${meta.endIndex} / ${meta.totalItems}`;
        }

        function renderButtons() {
            if (!paginationElement) return;

            const meta = getMeta();

            if (meta.totalPages <= 1) {
                paginationElement.innerHTML = '';
                return;
            }

            const pageList = buildPageList(meta.page, meta.totalPages);

            paginationElement.innerHTML = `
                <nav aria-label="Pagination">
                    <ul class="pagination pagination-sm mb-0">
                        <li class="page-item ${meta.page === 1 ? 'disabled' : ''}">
                            <button type="button" class="page-link" data-page="${meta.page - 1}">Trước</button>
                        </li>

                        ${pageList.map(x => {
                if (x === '...') {
                    return `<li class="page-item disabled"><span class="page-link">…</span></li>`;
                }

                return `
                                <li class="page-item ${x === meta.page ? 'active' : ''}">
                                    <button type="button" class="page-link" data-page="${x}">${x}</button>
                                </li>
                            `;
            }).join('')}

                        <li class="page-item ${meta.page === meta.totalPages ? 'disabled' : ''}">
                            <button type="button" class="page-link" data-page="${meta.page + 1}">Sau</button>
                        </li>
                    </ul>
                </nav>
            `;

            paginationElement.querySelectorAll('button[data-page]').forEach(button => {
                button.addEventListener('click', function () {
                    const page = Number(button.dataset.page || 1);
                    goToPage(page);
                });
            });
        }

        function render() {
            const meta = getMeta();

            if (meta.totalPages > 0 && state.page > meta.totalPages) {
                state.page = meta.totalPages;
            }

            const start = (state.page - 1) * state.pageSize;
            const pageItems = state.items.slice(start, start + state.pageSize);

            onPageChange(pageItems, getMeta());
            renderInfo();
            renderButtons();
        }

        function setItems(items, preservePage = false) {
            state.items = Array.isArray(items) ? items : [];
            state.page = preservePage ? state.page : 1;
            render();
        }

        function goToPage(page) {
            const meta = getMeta();
            if (meta.totalPages === 0) {
                state.page = 1;
                render();
                return;
            }

            state.page = Math.min(Math.max(1, page), meta.totalPages);
            render();
        }

        function goToItem(predicate) {
            const index = state.items.findIndex(predicate);
            if (index < 0) {
                return false;
            }

            state.page = Math.floor(index / state.pageSize) + 1;
            render();
            return true;
        }

        function refresh() {
            render();
        }

        return {
            state,
            setItems,
            goToPage,
            goToItem,
            refresh
        };
    }

    function attachDomPager(options) {
        const container = document.querySelector(options.containerSelector);
        if (!container) {
            return null;
        }

        const pager = createPager({
            pageSize: options.pageSize || 15,
            paginationContainer: options.paginationContainer,
            infoContainer: options.infoContainer,
            onPageChange: function (items) {
                const nodes = Array.from(container.querySelectorAll(options.itemSelector));

                nodes.forEach(node => {
                    node.style.display = 'none';
                });

                items.forEach(node => {
                    node.style.display = '';
                });
            }
        });

        function refresh() {
            const nodes = Array.from(container.querySelectorAll(options.itemSelector));
            pager.setItems(nodes, false);
        }

        refresh();

        return {
            refresh,
            goToPage: pager.goToPage,
            state: pager.state
        };
    }

    return {
        createPager,
        attachDomPager
    };
})();
