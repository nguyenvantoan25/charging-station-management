// Why changed: load live homepage metrics and lists without heavy backend changes.
(function () {
    document.addEventListener('DOMContentLoaded', function () {
        loadHomeDashboard();
    });

    async function loadHomeDashboard() {
        try {
            const response = await fetch('/User/api/home/live-news');
            if (!response.ok) {
                throw new Error('Không thể tải dữ liệu trang chủ.');
            }

            const result = await response.json();
            renderSummary(result);
            renderLatestReviews(result.latestReviews || []);
            renderFeaturedStations(result.featuredStations || []);
        } catch (error) {
            console.error(error);
            setText('homeLiveStatus', 'Chưa thể tải dữ liệu hệ thống lúc này.');
            document.getElementById('homeReviewList').innerHTML = '<div class="home-empty-state">Chưa thể tải đánh giá mới nhất.</div>';
            document.getElementById('homeFeaturedStationList').innerHTML = '<div class="home-empty-state">Chưa thể tải danh sách trạm nổi bật.</div>';
        }
    }

    function renderSummary(result) {
        const totalStations = Number(result.totalStations || 0);
        const activeStations = Number(result.activeStations || 0);
        const totalReviews = Number(result.totalReviews || 0);
        const featuredStations = Array.isArray(result.featuredStations) ? result.featuredStations.length : 0;

        setText('homeTotalStations', formatNumber(totalStations));
        setText('homeActiveStations', formatNumber(activeStations));
        setText('homeTotalReviews', formatNumber(totalReviews));
        setText('homeFeaturedCount', formatNumber(featuredStations));
        setText('homeLiveStatus', `Đã đồng bộ ${formatNumber(totalStations)} trạm và ${formatNumber(totalReviews)} lượt đánh giá.`);
    }

    function renderLatestReviews(items) {
        const container = document.getElementById('homeReviewList');

        if (!Array.isArray(items) || !items.length) {
            container.innerHTML = '<div class="home-empty-state">Chưa có đánh giá nào để hiển thị.</div>';
            return;
        }

        container.innerHTML = items.slice(0, 5).map(function (item) {
            const rating = Number(item.rating || 0);
            const stationName = escapeHtml(item.stationName || 'Trạm sạc');
            const comment = escapeHtml(item.comment || 'Người dùng chưa để lại bình luận chi tiết.');
            const author = escapeHtml(item.userName || 'Người dùng');
            const timeText = escapeHtml(formatDateTime(item.activityAt || item.updatedAt || item.createdAt));
            const editedText = item.isEdited ? ' · Đã chỉnh sửa' : '';

            return `
                <article class="home-review-item">
                    <div class="home-review-head">
                        <div class="home-review-station">${stationName}</div>
                        <div class="home-review-rating"><i class="fa-solid fa-star"></i><span>${rating.toFixed(1)}/5</span></div>
                    </div>
                    <div class="home-review-comment">${comment}</div>
                    <div class="home-review-meta">${author} · ${timeText}${editedText}</div>
                </article>
            `;
        }).join('');
    }

    function renderFeaturedStations(items) {
        const container = document.getElementById('homeFeaturedStationList');

        if (!Array.isArray(items) || !items.length) {
            container.innerHTML = '<div class="home-empty-state">Chưa có trạm nổi bật để hiển thị.</div>';
            return;
        }

        container.innerHTML = items.slice(0, 5).map(function (item) {
            const id = Number(item.id || 0);
            const name = escapeHtml(item.name || 'Trạm sạc');
            const address = escapeHtml(item.address || 'Chưa có địa chỉ');
            const status = escapeHtml(item.status || 'Chưa xác định');
            const reviewCount = Number(item.reviewCount || 0);
            const averageRating = Number(item.averageRating || 0);

            return `
                <article class="home-featured-item">
                    <div class="home-featured-head">
                        <div class="home-featured-name">${name}</div>
                        <div class="home-featured-rating"><i class="fa-solid fa-star"></i><span>${averageRating.toFixed(1)}</span></div>
                    </div>
                    <div class="home-featured-address">${address}</div>
                    <div class="home-featured-meta">
                        <span class="home-featured-chip">${status}</span>
                        <span class="home-featured-chip">${formatNumber(reviewCount)} đánh giá</span>
                    </div>
                    <a class="home-featured-link" href="/User/Station/Details?id=${id}">Xem chi tiết <i class="fa-solid fa-arrow-right"></i></a>
                </article>
            `;
        }).join('');
    }

    function setText(id, value) {
        const element = document.getElementById(id);
        if (element) {
            element.textContent = value;
        }
    }

    function formatNumber(value) {
        return new Intl.NumberFormat('vi-VN').format(Number(value || 0));
    }

    function formatDateTime(value) {
        if (!value) {
            return '-';
        }

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
})();
