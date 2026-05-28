// Why changed: load homepage cards from real app data so the page feels less static and more user-facing.
(function () {
    const FALLBACK_STATION_IMAGE = '/user/assets/images/service2-image1.jpg';
    const FALLBACK_NEWS_IMAGE = '/user/assets/images/article-image1.jpg';

    document.addEventListener('DOMContentLoaded', () => {
        void initHomePage();
    });

    async function initHomePage() {
        await Promise.allSettled([
            loadLiveData(),
            loadExternalNews()
        ]);
    }

    async function loadLiveData() {
        try {
            const response = await fetch('/User/api/home/live-news', {
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                throw new Error('live-news request failed');
            }

            const data = await response.json();
            renderHeroStats(data);
            renderFeaturedStations(data.featuredStations || []);
            renderLatestReviews(data.latestReviews || []);
        } catch (error) {
            console.error(error);
            renderFeaturedStations([]);
            renderLatestReviews([]);
        }
    }

    async function loadExternalNews() {
        try {
            const response = await fetch('/User/api/home/external-ev-news', {
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                throw new Error('external-ev-news request failed');
            }

            const data = await response.json();
            renderNews(data.items || []);
        } catch (error) {
            console.error(error);
            renderNews([]);
        }
    }

    function renderHeroStats(data) {
        setText('heroLiveStationCount', formatNumber(data.activeStations));
        setText('heroReviewCount', formatNumber(data.totalReviews));
        setText('homeTotalStations', formatNumber(data.totalStations));
        setText('homeActiveStations', formatNumber(data.activeStations));
        setText('homeTotalReviews', formatNumber(data.totalReviews));
    }

    function renderFeaturedStations(items) {
        const host = document.getElementById('homeFeaturedStations');
        if (!host) {
            return;
        }

        if (!items.length) {
            host.innerHTML = [1, 2, 3].map(index => `
                <article class="home-station-card">
                    <div class="home-station-thumb">
                        <img src="${FALLBACK_STATION_IMAGE}" alt="Trạm sạc EV ${index}">
                    </div>
                    <div class="home-station-body">
                        <div class="home-station-status-row">
                            <span class="home-station-status">Gợi ý EV</span>
                        </div>
                        <h3>Trạm sạc đang được người dùng quan tâm</h3>
                        <p class="home-station-meta">Tra cứu để xem tình trạng hoạt động, vị trí và đánh giá thực tế.</p>
                        <a class="home-station-link" href="/User/Station/Index">Mở tra cứu <i class="fa-solid fa-arrow-right"></i></a>
                    </div>
                </article>
            `).join('');
            return;
        }

        host.innerHTML = items.slice(0, 3).map((item, index) => {
            const statusClass = normalizeStatus(item.status) === 'Đang hoạt động' ? '' : 'offline';
            const safeName = escapeHtml(item.name || 'Trạm sạc EV');
            const safeAddress = escapeHtml(item.address || 'Đang cập nhật địa chỉ');
            const averageRating = Number(item.averageRating || 0).toFixed(1);
            const reviewCount = formatNumber(item.reviewCount || 0);
            const imagePool = [
                '/user/assets/images/service2-image1.jpg',
                '/user/assets/images/service2-image2.jpg',
                '/user/assets/images/service2-image3.jpg'
            ];
            const imageUrl = imagePool[index % imagePool.length];

            return `
                <article class="home-station-card">
                    <div class="home-station-thumb">
                        <img src="${imageUrl}" alt="${safeName}">
                    </div>
                    <div class="home-station-body">
                        <div class="home-station-status-row">
                            <span class="home-station-status ${statusClass}">${escapeHtml(normalizeStatus(item.status))}</span>
                            <span class="home-rating"><i class="fa-solid fa-star"></i> ${averageRating} <span>(${reviewCount})</span></span>
                        </div>
                        <h3>${safeName}</h3>
                        <p class="home-station-meta">${safeAddress}</p>
                        <a class="home-station-link" href="/User/Station/Details/${Number(item.id || 0)}">Xem chi tiết <i class="fa-solid fa-arrow-right"></i></a>
                    </div>
                </article>
            `;
        }).join('');
    }

    function renderLatestReviews(items) {
        const host = document.getElementById('homeLatestReviews');
        const summaryNode = document.getElementById('homeReviewSummary');

        if (!host || !summaryNode) {
            return;
        }

        if (!items.length) {
            summaryNode.textContent = 'Dễ tra cứu, dễ đi, dễ chọn trạm.';
            host.innerHTML = [1, 2, 3].map(() => `
                <article class="home-review-card">
                    <div class="home-review-head">
                        <strong>Người dùng EV</strong>
                        <span class="home-review-badge">Review mới</span>
                    </div>
                    <div class="home-review-stars">★★★★★</div>
                    <p class="home-review-text">Trang chủ đang sẵn sàng hiển thị các đánh giá thực tế từ người dùng.</p>
                    <div class="home-review-date">Cập nhật khi có dữ liệu mới</div>
                </article>
            `).join('');
            return;
        }

        summaryNode.textContent = buildReviewSummary(items);

        host.innerHTML = items.slice(0, 6).map(item => {
            const safeUserName = escapeHtml(item.userName || 'Người dùng EV');
            const safeStationName = escapeHtml(item.stationName || 'Trạm sạc');
            const safeComment = escapeHtml(trimText(item.comment || 'Trải nghiệm đang được cập nhật.', 120));
            const rating = Math.max(1, Math.min(5, Number(item.rating || 0)));

            return `
                <article class="home-review-card">
                    <div class="home-review-head">
                        <strong>${safeUserName}</strong>
                        <span class="home-review-badge">${safeStationName}</span>
                    </div>
                    <div class="home-review-stars">${'★'.repeat(rating)}${'☆'.repeat(5 - rating)}</div>
                    <p class="home-review-text">${safeComment}</p>
                    <div class="home-review-date">${formatDate(item.activityAt || item.updatedAt || item.createdAt)}</div>
                </article>
            `;
        }).join('');
    }

    function renderNews(items) {
        const host = document.getElementById('homeEvNews');
        if (!host) {
            return;
        }

        if (!items.length) {
            host.innerHTML = [
                {
                    title: 'Nhịp sống EV đang thay đổi rất nhanh',
                    summary: 'Tin tức, xu hướng và cảm hứng mới cho người dùng xe điện.',
                    imageUrl: '/user/assets/images/article-image1.jpg'
                },
                {
                    title: 'Chủ động hơn trên mọi cung đường',
                    summary: 'Tra cứu trạm sạc tốt hơn giúp trải nghiệm lái xe mượt hơn.',
                    imageUrl: '/user/assets/images/article-image2.jpg'
                },
                {
                    title: 'Đi xanh nhưng vẫn phải tiện',
                    summary: 'Người dùng cần bản đồ rõ, review thật và gợi ý hợp nhu cầu.',
                    imageUrl: '/user/assets/images/article-image3.jpg'
                }
            ].map(createFallbackNewsCard).join('');
            return;
        }

        host.innerHTML = items.slice(0, 3).map(item => {
            const safeTitle = escapeHtml(trimText(item.title || 'Tin EV mới', 84));
            const safeSummary = escapeHtml(trimText(item.summary || 'Đang cập nhật nội dung.', 118));
            const safeSource = escapeHtml(item.source || 'EV News');
            const safeDate = formatDate(item.publishedAt);
            const imageUrl = safeUrl(item.imageUrl) || FALLBACK_NEWS_IMAGE;
            const linkUrl = safeUrl(item.link) || '/User/Home/About';

            return `
                <article class="home-news-card">
                    <div class="home-news-thumb">
                        <img src="${imageUrl}" alt="${safeTitle}">
                    </div>
                    <div class="home-news-body">
                        <div class="home-news-meta">${safeSource} • ${safeDate}</div>
                        <h3>${safeTitle}</h3>
                        <p class="home-station-meta">${safeSummary}</p>
                        <a class="home-news-link" href="${linkUrl}" target="_blank" rel="noopener noreferrer">Đọc nhanh <i class="fa-solid fa-arrow-right"></i></a>
                    </div>
                </article>
            `;
        }).join('');
    }

    function createFallbackNewsCard(item) {
        return `
            <article class="home-news-card">
                <div class="home-news-thumb">
                    <img src="${item.imageUrl}" alt="${escapeHtml(item.title)}">
                </div>
                <div class="home-news-body">
                    <div class="home-news-meta">EV Inspiration</div>
                    <h3>${escapeHtml(item.title)}</h3>
                    <p class="home-station-meta">${escapeHtml(item.summary)}</p>
                    <a class="home-news-link" href="/User/Home/About">Xem thêm <i class="fa-solid fa-arrow-right"></i></a>
                </div>
            </article>
        `;
    }

    function buildReviewSummary(items) {
        const comments = items
            .map(item => String(item.comment || '').toLowerCase())
            .filter(Boolean);

        if (!comments.length) {
            return 'Dễ tra cứu, dễ đi, dễ chọn trạm.';
        }

        const keywordGroups = [
            { label: 'dễ tìm', keys: ['dễ tìm', 'dễ kiếm', 'dễ thấy', 'vị trí', 'địa chỉ'] },
            { label: 'sạc ổn', keys: ['sạc nhanh', 'ổn định', 'ổn', 'ok', 'nhanh'] },
            { label: 'chỗ đậu thoáng', keys: ['chỗ đậu', 'đỗ xe', 'rộng', 'thoáng', 'dễ vào'] },
            { label: 'phục vụ tốt', keys: ['hỗ trợ', 'nhân viên', 'phục vụ', 'nhiệt tình'] },
            { label: 'giá hợp lý', keys: ['giá', 'chi phí', 'hợp lý'] }
        ];

        const ranked = keywordGroups
            .map(group => ({
                label: group.label,
                score: comments.reduce((total, comment) => total + (group.keys.some(key => comment.includes(key)) ? 1 : 0), 0)
            }))
            .filter(item => item.score > 0)
            .sort((a, b) => b.score - a.score)
            .slice(0, 3)
            .map(item => item.label);

        if (!ranked.length) {
            return 'Người dùng đang quan tâm nhiều đến độ dễ tìm trạm và trải nghiệm sạc thực tế.';
        }

        return ranked.join(', ');
    }

    function normalizeStatus(status) {
        const raw = String(status || '').trim().toLowerCase();
        if (!raw) {
            return 'Đang cập nhật';
        }

        if (raw.includes('hoạt động') || raw.includes('available') || raw.includes('active')) {
            return 'Đang hoạt động';
        }

        return 'Cần kiểm tra';
    }

    function formatNumber(value) {
        const number = Number(value || 0);
        return Number.isFinite(number) ? number.toLocaleString('vi-VN') : '--';
    }

    function formatDate(value) {
        const date = value ? new Date(value) : null;
        if (!date || Number.isNaN(date.getTime())) {
            return 'Mới cập nhật';
        }

        return new Intl.DateTimeFormat('vi-VN', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric'
        }).format(date);
    }

    function trimText(value, maxLength) {
        const text = String(value || '').replace(/\s+/g, ' ').trim();
        if (text.length <= maxLength) {
            return text;
        }

        return `${text.slice(0, maxLength).trim()}…`;
    }

    function setText(id, value) {
        const node = document.getElementById(id);
        if (node) {
            node.textContent = value;
        }
    }

    function escapeHtml(value) {
        return String(value || '')
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#39;');
    }

    function safeUrl(value) {
        const raw = String(value || '').trim();
        if (!raw) {
            return '';
        }

        try {
            const url = new URL(raw, window.location.origin);
            return url.href;
        } catch {
            return '';
        }
    }
})();
