(function () {
    const pageConfig = window.tramSacContactPage || { isAuthenticated: false };
    const isAuthenticated = pageConfig.isAuthenticated === true || pageConfig.isAuthenticated === 'true';

    let supportTickets = [];
    let currentPage = 1;
    const pageSize = 5;

    document.addEventListener('DOMContentLoaded', function () {
        bindContactForm();

        if (isAuthenticated) {
            loadMyTickets();
        }
    });

    function bindContactForm() {
        const form = document.getElementById('contactForm');
        if (!form) return;

        form.addEventListener('submit', async function (event) {
            event.preventDefault();

            if (!isAuthenticated) {
                showResult('Bạn cần đăng nhập để gửi yêu cầu hỗ trợ.', false);
                window.location.href = '/User/Account/Login';
                return;
            }

            const submitBtn = document.getElementById('contactSubmitBtn');
            const payload = {
                fullName: getValue('fullName'),
                email: getValue('email'),
                phoneNumber: getValue('phoneNumber'),
                subject: getValue('subject'),
                message: getValue('message')
            };

            submitBtn.disabled = true;
            submitBtn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i><span>Đang gửi yêu cầu</span>';

            try {
                const response = await fetch('/User/api/home/support-contact', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });

                const result = await response.json();

                if (response.status === 401) {
                    showResult(result.message || 'Bạn cần đăng nhập để gửi yêu cầu hỗ trợ.', false);
                    window.location.href = '/User/Account/Login';
                    return;
                }

                showResult(result.message || 'Có lỗi xảy ra khi gửi yêu cầu.', response.ok && result.success);

                if (response.ok && result.success) {
                    form.reset();
                    await loadMyTickets();
                    window.dispatchEvent(new CustomEvent('support-notification-updated'));
                }
            } catch (error) {
                console.error(error);
                showResult('Không thể gửi yêu cầu hỗ trợ lúc này.', false);
            } finally {
                submitBtn.disabled = false;
                submitBtn.innerHTML = '<i class="fa-solid fa-paper-plane"></i><span>Gửi yêu cầu hỗ trợ</span>';
            }
        });
    }

    async function loadMyTickets() {
        try {
            const response = await fetch('/User/api/home/my-support-tickets');
            if (!response.ok) {
                throw new Error('Không thể tải danh sách yêu cầu hỗ trợ của bạn.');
            }

            const result = await response.json();
            supportTickets = Array.isArray(result.items) ? result.items.slice() : [];

            supportTickets.sort(function (a, b) {
                return new Date(b.createdAt || 0) - new Date(a.createdAt || 0);
            });

            setText('ticketSummaryTotal', String(Number(supportTickets.length || 0)));
            setText('ticketSummaryOpen', String(Number(result.unresolvedCount || 0)));
            setText('ticketSummaryUnread', String(Number(result.resolvedUnreadCount || 0)));

            currentPage = 1;
            renderTicketPage();
        } catch (error) {
            console.error(error);

            const container = document.getElementById('ticketList');
            if (container) {
                container.innerHTML = '<div class="contact-ticket-empty">Không thể tải danh sách yêu cầu hỗ trợ của bạn.</div>';
            }

            const info = document.getElementById('ticketPageInfo');
            const pager = document.getElementById('ticketPager');
            if (info) info.textContent = '';
            if (pager) pager.innerHTML = '';
        }
    }

    function renderTicketPage() {
        const container = document.getElementById('ticketList');
        const pageInfo = document.getElementById('ticketPageInfo');
        const pager = document.getElementById('ticketPager');

        if (!container || !pageInfo || !pager) return;

        if (!supportTickets.length) {
            container.innerHTML = '<div class="contact-ticket-empty">Bạn chưa có yêu cầu hỗ trợ nào.</div>';
            pageInfo.textContent = '';
            pager.innerHTML = '';
            return;
        }

        const total = supportTickets.length;
        const totalPages = Math.max(1, Math.ceil(total / pageSize));

        if (currentPage > totalPages) currentPage = totalPages;

        const start = (currentPage - 1) * pageSize;
        const end = Math.min(start + pageSize, total);
        const pageItems = supportTickets.slice(start, end);

        container.innerHTML = pageItems.map(function (item, index) {
            const badgeInfo = getTicketBadge(item.status, item.isUserSeen);

            const replyHtml = item.adminReply
                ? `<div class="contact-ticket-reply"><strong>Phản hồi từ admin:</strong><br>${escapeHtml(item.adminReply)}</div>`
                : '';

            const seenButtonHtml = item.status === 'Đã xử lý' && !item.isUserSeen
                ? `<button type="button" class="contact-ticket-btn" onclick="window.TramSacContactPageActions.markTicketSeen(${Number(item.id || 0)})"><i class="fa-solid fa-eye"></i><span>Đã xem thông báo</span></button>`
                : '';

            const updatedText = item.lastStatusChangedAt
                ? `<span>Cập nhật: ${formatDateTime(item.lastStatusChangedAt)}</span>`
                : '';

            return `
                <article class="contact-ticket-item">
                    <div class="contact-ticket-row">
                        <div class="contact-ticket-order">${start + index + 1}</div>

                        <div>
                            <div class="contact-ticket-head">
                                <div>
                                    <h3 class="contact-ticket-title">${escapeHtml(item.subject || 'Yêu cầu hỗ trợ')}</h3>
                                    <div class="contact-ticket-meta">
                                        <span>Tạo lúc: ${formatDateTime(item.createdAt)}</span>
                                        ${updatedText}
                                    </div>
                                </div>
                            </div>

                            <div class="contact-ticket-message">${escapeHtml(item.message || '')}</div>
                            ${replyHtml}

                            <div class="contact-ticket-actions">
                                ${seenButtonHtml}
                                <a class="contact-ticket-btn-secondary" href="/User/Home/Contact#contactPageTop">
                                    <i class="fa-solid fa-plus"></i>
                                    <span>Gửi yêu cầu mới</span>
                                </a>
                            </div>
                        </div>

                        <div>
                            <span class="contact-ticket-badge ${badgeInfo.className}">${badgeInfo.text}</span>
                        </div>
                    </div>
                </article>
            `;
        }).join('');

        pageInfo.textContent = `Hiển thị ${start + 1}-${end} / ${total}`;
        renderPager(totalPages);
    }

    function renderPager(totalPages) {
        const pager = document.getElementById('ticketPager');
        if (!pager) return;

        pager.innerHTML = '';

        const prev = createPagerButton('‹', currentPage === 1, function () {
            if (currentPage > 1) {
                currentPage--;
                renderTicketPage();
            }
        });
        pager.appendChild(prev);

        for (let i = 1; i <= totalPages; i++) {
            const btn = createPagerButton(String(i), false, function () {
                currentPage = i;
                renderTicketPage();
            });

            if (i === currentPage) {
                btn.classList.add('is-active');
            }

            pager.appendChild(btn);
        }

        const next = createPagerButton('›', currentPage === totalPages, function () {
            if (currentPage < totalPages) {
                currentPage++;
                renderTicketPage();
            }
        });
        pager.appendChild(next);
    }

    function createPagerButton(text, isDisabled, onClick) {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'contact-page-btn' + (isDisabled ? 'is-disabled' : '');

        if (isDisabled) {
            btn.className = 'contact-page-btn is-disabled';
        }

        btn.textContent = text;

        if (!isDisabled) {
            btn.addEventListener('click', onClick);
        }

        return btn;
    }

    async function markTicketSeen(id) {
        try {
            const response = await fetch(`/User/api/home/support-mark-seen/${id}`, {
                method: 'POST'
            });

            const result = await response.json();

            if (!response.ok || !result.success) {
                alert(result.message || 'Không thể xác nhận đã xem.');
                return;
            }

            await loadMyTickets();
            window.dispatchEvent(new CustomEvent('support-notification-updated'));
        } catch (error) {
            console.error(error);
            alert('Có lỗi xảy ra khi xác nhận thông báo.');
        }
    }

    function getTicketBadge(status, isUserSeen) {
        if (status === 'Đã xử lý' && !isUserSeen) {
            return {
                text: 'Thông báo mới',
                className: 'contact-ticket-badge-new'
            };
        }

        if (status === 'Đã xử lý') {
            return {
                text: 'Đã xử lý',
                className: 'contact-ticket-badge-done'
            };
        }

        if (status === 'Đang xử lý' || status === 'Đã đọc') {
            return {
                text: 'Đang xử lý',
                className: 'contact-ticket-badge-processing'
            };
        }

        return {
            text: 'Mới',
            className: 'contact-ticket-badge-new'
        };
    }

    function showResult(message, isSuccess) {
        const resultBox = document.getElementById('contactResult');
        if (!resultBox) return;

        resultBox.style.display = 'block';
        resultBox.className = `contact-result-box ${isSuccess ? 'contact-result-success' : 'contact-result-error'}`;
        resultBox.textContent = message;
    }

    function setText(id, value) {
        const element = document.getElementById(id);
        if (element) {
            element.textContent = value;
        }
    }

    function getValue(id) {
        const element = document.getElementById(id);
        return element ? String(element.value || '').trim() : '';
    }

    function formatDateTime(value) {
        if (!value) return '-';
        return new Date(value).toLocaleString('vi-VN');
    }

    function escapeHtml(text) {
        return String(text || '')
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#039;');
    }

    window.TramSacContactPageActions = {
        markTicketSeen: markTicketSeen
    };
})();