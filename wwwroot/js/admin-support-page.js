// Why changed: move admin support logic into a dedicated file with better UX and light ticket classification.
(function () {
    let allSupportRequests = [];
    let supportPager = null;
    let currentSupportId = null;
    const supportToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    document.addEventListener('DOMContentLoaded', function () {
        supportPager = TramSacPagination.createPager({
            pageSize: 15,
            paginationContainer: 'supportPager',
            infoContainer: 'supportPageInfo',
            onPageChange: renderSupportTable
        });

        bindSupportEvents();
        loadSupportRequests();
    });

    function bindSupportEvents() {
        document.getElementById('supportKeyword')?.addEventListener('input', applySupportFilters);
        document.getElementById('supportStatus')?.addEventListener('change', applySupportFilters);
        document.getElementById('supportRead')?.addEventListener('change', applySupportFilters);
        document.getElementById('supportCategory')?.addEventListener('change', applySupportFilters);

        document.getElementById('supportReset')?.addEventListener('click', function () {
            setValue('supportKeyword', '');
            setValue('supportStatus', '');
            setValue('supportRead', '');
            setValue('supportCategory', '');
            applySupportFilters();
        });

        document.getElementById('supportModalReadBtn')?.addEventListener('click', async function () {
            if (!currentSupportId) {
                return;
            }

            await markSupportRead(currentSupportId, true);
        });

        document.getElementById('supportModalResolveBtn')?.addEventListener('click', async function () {
            if (!currentSupportId) {
                return;
            }

            const reply = getValue('supportAdminReply') || 'Yêu cầu của bạn đã được xử lý.';
            await markSupportResolved(currentSupportId, reply, true);
        });

        document.getElementById('supportModalDeleteBtn')?.addEventListener('click', async function () {
            if (!currentSupportId) {
                return;
            }

            await deleteSupport(currentSupportId, true);
        });
    }

    async function loadSupportRequests() {
        try {
            const response = await fetch('/Admin/Support/ListData');
            if (!response.ok) {
                throw new Error('Không thể tải dữ liệu hỗ trợ.');
            }

            const data = await response.json();
            allSupportRequests = (Array.isArray(data) ? data : []).map(augmentSupportItem);

            populateCategoryOptions(allSupportRequests);
            updateSummaryCards(allSupportRequests);
            updateUnreadUserCounter(allSupportRequests);
            applySupportFilters();
        } catch (error) {
            console.error(error);
            document.getElementById('supportTableBody').innerHTML = `
                <tr>
                    <td colspan="6" class="support-table-empty text-danger">Không thể tải dữ liệu hỗ trợ.</td>
                </tr>
            `;
            setText('supportResultCount', '0 kết quả');
        }
    }

    function augmentSupportItem(item) {
        const category = classifyTicket(item);
        const priority = detectPriority(item);
        const searchText = [
            item.fullName,
            item.email,
            item.subject,
            item.message,
            item.adminReply,
            item.senderUserName,
            category,
            priority
        ].join(' ').toLowerCase();

        return {
            ...item,
            category,
            priority,
            searchText
        };
    }

    function populateCategoryOptions(items) {
        const select = document.getElementById('supportCategory');
        if (!select) {
            return;
        }

        const currentValue = select.value;
        const categories = Array.from(new Set((items || []).map(function (item) {
            return item.category;
        }).filter(Boolean))).sort();

        select.innerHTML = '<option value="">Tất cả</option>' + categories.map(function (category) {
            return `<option value="${escapeHtml(category)}">${escapeHtml(category)}</option>`;
        }).join('');

        if (categories.includes(currentValue)) {
            select.value = currentValue;
        }
    }

    function updateSummaryCards(items) {
        const total = items.length;
        const totalNew = items.filter(function (item) { return item.status === 'Mới'; }).length;
        const totalProcessing = items.filter(function (item) { return item.status === 'Đã đọc'; }).length;
        const totalUnreadByUser = items.filter(function (item) { return item.status === 'Đã xử lý' && !item.isUserSeen; }).length;

        setText('supportSummaryTotal', String(total));
        setText('supportSummaryNew', String(totalNew));
        setText('supportSummaryProcessing', String(totalProcessing));
        setText('supportSummaryUnreadByUser', String(totalUnreadByUser));
    }

    function updateUnreadUserCounter(items) {
        const count = items.filter(function (item) {
            return item.status === 'Đã xử lý' && !item.isUserSeen;
        }).length;

        setText('supportUnreadUserCount', `${count} user chưa xem`);
    }

    function applySupportFilters() {
        const keyword = getValue('supportKeyword').toLowerCase();
        const status = getValue('supportStatus');
        const readStatus = getValue('supportRead');
        const category = getValue('supportCategory');

        const filtered = allSupportRequests.filter(function (item) {
            const matchKeyword = !keyword || item.searchText.includes(keyword);
            const matchStatus = !status || item.status === status;
            const matchRead = !readStatus || (readStatus === 'yes' ? item.isRead : !item.isRead);
            const matchCategory = !category || item.category === category;
            return matchKeyword && matchStatus && matchRead && matchCategory;
        });

        setText('supportResultCount', `${filtered.length} kết quả`);
        if (supportPager) {
            supportPager.setItems(filtered);
        }
    }

    function renderSupportTable(items) {
        const tbody = document.getElementById('supportTableBody');
        if (!tbody) {
            return;
        }

        if (!items.length) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="support-table-empty">Không có yêu cầu hỗ trợ nào phù hợp.</td>
                </tr>
            `;
            return;
        }

        tbody.innerHTML = items.map(function (item) {
            const statusBadge = renderStatusBadge(item.status);
            const userSeenBadge = item.status === 'Đã xử lý' && !item.isUserSeen
                ? '<span class="support-user-seen-badge support-user-seen-no">User chưa xem</span>'
                : '<span class="support-user-seen-badge support-user-seen-yes">User đã xem</span>';

            return `
                <tr>
                    <td>
                        <div class="support-sender-name">${escapeHtml(item.fullName || '-')}</div>
                        <div class="support-sender-meta">${escapeHtml(item.email || '-')}</div>
                        <div class="support-sender-meta">${escapeHtml(item.senderUserName || 'Khách / Người dùng')}</div>
                    </td>
                    <td>
                        <div class="support-subject-title">${escapeHtml(item.subject || '-')}</div>
                        <div class="support-message-snippet">${escapeHtml(shortText(item.message || '', 130))}</div>
                        <div class="support-row-meta mt-2">${item.adminReply ? `Phản hồi: ${escapeHtml(shortText(item.adminReply, 80))}` : 'Chưa có phản hồi admin'}</div>
                    </td>
                    <td>
                        <div class="support-chip-stack">
                            <span class="support-chip support-chip-category">${escapeHtml(item.category)}</span>
                            <span class="support-chip ${priorityClassName(item.priority)}">${escapeHtml(item.priority)}</span>
                        </div>
                    </td>
                    <td>
                        ${statusBadge}
                        <div>${userSeenBadge}</div>
                    </td>
                    <td>
                        <div class="support-row-meta">Tạo: ${formatDateTime(item.createdAt)}</div>
                        <div class="support-row-meta">Cập nhật: ${formatDateTime(item.lastStatusChangedAt || item.createdAt)}</div>
                    </td>
                    <td>
                        <div class="support-action-stack">
                            <button type="button" class="support-action-btn" title="Chi tiết" onclick="window.TramSacAdminSupport.openSupportDetail(${Number(item.id || 0)})">
                                <i class="fas fa-eye"></i>
                            </button>
                            <button type="button" class="support-action-btn" title="Đánh dấu đã đọc" onclick="window.TramSacAdminSupport.markSupportRead(${Number(item.id || 0)})">
                                <i class="fas fa-envelope-open-text"></i>
                            </button>
                            <button type="button" class="support-action-btn" title="Xóa" onclick="window.TramSacAdminSupport.deleteSupport(${Number(item.id || 0)})">
                                <i class="fas fa-trash-alt"></i>
                            </button>
                        </div>
                    </td>
                </tr>
            `;
        }).join('');
    }

    async function openSupportDetail(id) {
        try {
            const response = await fetch(`/Admin/Support/DetailsData?id=${id}`);
            const result = await response.json();

            if (!response.ok || !result.success) {
                alert(result.message || 'Không thể tải chi tiết yêu cầu hỗ trợ.');
                return;
            }

            const item = augmentSupportItem(result.item || {});
            currentSupportId = Number(item.id || 0);
            setValue('supportAdminReply', item.adminReply || '');

            document.getElementById('supportDetailBody').innerHTML = `
                <div class="support-detail-box">
                    <div class="support-detail-label">Người gửi</div>
                    <div class="support-detail-value">${escapeHtml(item.fullName || '-')}</div>
                    <div class="support-row-meta mt-1">${escapeHtml(item.senderUserName || 'Khách / Người dùng')}</div>
                </div>

                <div class="support-detail-box">
                    <div class="support-detail-label">Email / Điện thoại</div>
                    <div class="support-detail-value">${escapeHtml(item.email || '-')}</div>
                    <div class="support-row-meta mt-1">${escapeHtml(item.phoneNumber || 'Chưa có số điện thoại')}</div>
                </div>

                <div class="support-detail-box">
                    <div class="support-detail-label">Tiêu đề</div>
                    <div class="support-detail-value">${escapeHtml(item.subject || '-')}</div>
                </div>

                <div class="support-detail-box">
                    <div class="support-detail-label">Phân loại / Ưu tiên</div>
                    <div class="support-chip-stack">
                        <span class="support-chip support-chip-category">${escapeHtml(item.category)}</span>
                        <span class="support-chip ${priorityClassName(item.priority)}">${escapeHtml(item.priority)}</span>
                    </div>
                    <div class="support-row-meta mt-2">${statusPlainText(item)}</div>
                </div>

                <div class="support-detail-box full">
                    <div class="support-detail-label">Nội dung người dùng gửi</div>
                    <div class="support-detail-value">${escapeHtml(item.message || '-')}</div>
                </div>

                <div class="support-detail-box">
                    <div class="support-detail-label">Thời gian tạo</div>
                    <div class="support-detail-value">${formatDateTime(item.createdAt)}</div>
                </div>

                <div class="support-detail-box">
                    <div class="support-detail-label">Cập nhật gần nhất</div>
                    <div class="support-detail-value">${formatDateTime(item.lastStatusChangedAt || item.createdAt)}</div>
                </div>

                <div class="support-detail-box full">
                    <div class="support-detail-label">Trạng thái user đã xem</div>
                    <div class="support-detail-value">${item.status === 'Đã xử lý' && !item.isUserSeen ? 'User chưa xem phản hồi.' : 'User đã xem hoặc ticket chưa cần phản hồi.'}</div>
                </div>
            `;

            $('#supportDetailModal').modal('show');
        } catch (error) {
            console.error(error);
            alert('Có lỗi khi tải chi tiết yêu cầu hỗ trợ.');
        }
    }

    async function markSupportRead(id, keepModalOpen) {
        const success = await postSupportAction(`/Admin/Support/MarkRead/${id}`);
        if (success && !keepModalOpen) {
            return;
        }
        if (success && keepModalOpen) {
            await reopenCurrentDetail();
        }
    }

    async function markSupportResolved(id, adminReply, closeModalOnSuccess) {
        const formData = new FormData();
        formData.append('adminReply', adminReply || '');
        const success = await postSupportAction(`/Admin/Support/MarkResolved/${id}`, formData);

        if (success) {
            if (closeModalOnSuccess) {
                $('#supportDetailModal').modal('hide');
            } else {
                await reopenCurrentDetail();
            }
        }
    }

    async function deleteSupport(id, closeModalOnSuccess) {
        if (!confirm('Bạn có chắc muốn xóa yêu cầu hỗ trợ này không?')) {
            return false;
        }

        const success = await postSupportAction(`/Admin/Support/Delete/${id}`);
        if (success && closeModalOnSuccess) {
            $('#supportDetailModal').modal('hide');
        }
        return success;
    }

    async function postSupportAction(url, body) {
        if (!supportToken) {
            alert('Thiếu anti-forgery token.');
            return false;
        }

        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: {
                    RequestVerificationToken: supportToken
                },
                body: body || null
            });

            const result = await response.json();
            if (!response.ok || !result.success) {
                alert(result.message || 'Không thể cập nhật dữ liệu hỗ trợ.');
                return false;
            }

            await loadSupportRequests();
            return true;
        } catch (error) {
            console.error(error);
            alert('Có lỗi xảy ra khi cập nhật dữ liệu hỗ trợ.');
            return false;
        }
    }

    async function reopenCurrentDetail() {
        if (currentSupportId) {
            await openSupportDetail(currentSupportId);
        }
    }

    function classifyTicket(item) {
        const text = `${item.subject || ''} ${item.message || ''}`.toLowerCase();

        if (containsAny(text, ['đăng nhập', 'mật khẩu', 'tài khoản', 'account', 'register', 'login'])) {
            return 'Tài khoản';
        }
        if (containsAny(text, ['thanh toán', 'payment', 'hoá đơn', 'hóa đơn', 'giao dịch'])) {
            return 'Thanh toán';
        }
        if (containsAny(text, ['bản đồ', 'map', 'lộ trình', 'chỉ đường', 'google maps'])) {
            return 'Bản đồ / chỉ đường';
        }
        if (containsAny(text, ['trạm', 'sạc', 'trụ', 'charger'])) {
            return 'Trạm sạc';
        }
        if (containsAny(text, ['khiếu nại', 'phản hồi', 'lỗi', 'bug', 'không hoạt động', 'hỏng', 'sự cố'])) {
            return 'Sự cố / khiếu nại';
        }
        if (containsAny(text, ['góp ý', 'đề xuất', 'đề nghị', 'feature'])) {
            return 'Góp ý / đề xuất';
        }

        return 'Khác';
    }

    function detectPriority(item) {
        const text = `${item.subject || ''} ${item.message || ''}`.toLowerCase();

        if (containsAny(text, ['khẩn', 'gấp', 'urgent', 'không hoạt động', 'không sạc được', 'lỗi', 'sự cố'])) {
            return 'Ưu tiên cao';
        }

        if (item.status === 'Mới' || item.status === 'Đã đọc') {
            return 'Ưu tiên trung bình';
        }

        return 'Ưu tiên thấp';
    }

    function containsAny(text, keywords) {
        return keywords.some(function (keyword) {
            return text.includes(keyword);
        });
    }

    function renderStatusBadge(status) {
        if (status === 'Đã xử lý') {
            return '<span class="support-status-badge support-status-done">Đã xử lý</span>';
        }
        if (status === 'Đã đọc') {
            return '<span class="support-status-badge support-status-processing">Đã đọc</span>';
        }
        return '<span class="support-status-badge support-status-new">Mới</span>';
    }

    function priorityClassName(priority) {
        if (priority === 'Ưu tiên cao') {
            return 'support-chip-priority-high';
        }
        if (priority === 'Ưu tiên trung bình') {
            return 'support-chip-priority-medium';
        }
        return 'support-chip-priority-low';
    }

    function statusPlainText(item) {
        const userSeenText = item.status === 'Đã xử lý' && !item.isUserSeen ? 'User chưa xem.' : 'User đã xem hoặc chưa cần xem.';
        return `Trạng thái: ${item.status || '-'} · ${userSeenText}`;
    }

    function shortText(text, maxLength) {
        const value = String(text || '').trim();
        if (value.length <= maxLength) {
            return value;
        }
        return value.substring(0, maxLength).trim() + '...';
    }

    function formatDateTime(value) {
        if (!value) {
            return '-';
        }
        return new Date(value).toLocaleString('vi-VN');
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

    function setValue(id, value) {
        const element = document.getElementById(id);
        if (element) {
            element.value = value;
        }
    }

    function escapeHtml(text) {
        return String(text || '')
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#039;');
    }

    window.TramSacAdminSupport = {
        openSupportDetail: openSupportDetail,
        markSupportRead: function (id) { return markSupportRead(id, false); },
        deleteSupport: function (id) { return deleteSupport(id, false); }
    };
})();
