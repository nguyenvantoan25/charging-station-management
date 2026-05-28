window.TramSacMap4D = (function () {
    let sdkPromise = null;

    function ensureSdk(apiKey) {
        if (window.map4d) {
            return Promise.resolve(window.map4d);
        }

        if (sdkPromise) {
            return sdkPromise;
        }

        sdkPromise = new Promise((resolve, reject) => {
            if (!apiKey) {
                reject(new Error('Thiếu Map4D API key.'));
                return;
            }

            window.__tramSacMap4dLoaded = function () {
                resolve(window.map4d);
            };

            const existing = document.getElementById('tramsac-map4d-sdk');
            if (existing) {
                existing.addEventListener('error', () => reject(new Error('Không tải được SDK Map4D.')), { once: true });
                return;
            }

            const script = document.createElement('script');
            script.id = 'tramsac-map4d-sdk';
            script.defer = true;
            script.src = `https://api.map4d.vn/sdk/map/js?version=3.0&key=${encodeURIComponent(apiKey)}&callback=__tramSacMap4dLoaded`;
            script.onerror = function () {
                reject(new Error('Không tải được SDK Map4D.'));
            };

            document.head.appendChild(script);
        });

        return sdkPromise;
    }

    function createMarker(map, options) {
        const marker = new map4d.Marker(options || {});
        marker.setMap(map);
        return marker;
    }

    function clearObject(mapObject) {
        if (mapObject && typeof mapObject.setMap === 'function') {
            mapObject.setMap(null);
        }
    }

    function fitBounds(map, points, fallbackCenter, fallbackZoom) {
        if (!map || !window.map4d || !Array.isArray(points) || points.length === 0) {
            return;
        }

        if (points.length === 1) {
            focus(map, points[0], fallbackZoom || 15);
            return;
        }

        try {
            if (typeof map4d.LatLngBounds === 'function') {
                const bounds = new map4d.LatLngBounds();
                points.forEach(point => bounds.extend(point));
                map.fitBounds(bounds, { padding: 60 });
                return;
            }
        } catch (error) {
            console.error('Map4D fitBounds error:', error);
        }

        if (fallbackCenter) {
            focus(map, fallbackCenter, fallbackZoom || 6);
        }
    }

    // FIX: thay vì chỉ setCenter/setZoom, tạo bounds nhỏ quanh vị trí để Map4D chắc chắn zoom.
    function focus(map, position, zoom) {
        if (!map || !position) {
            return;
        }

        const targetZoom = Number(zoom || 15);
        const lat = Number(position.lat);
        const lng = Number(position.lng);

        const apply = function () {
            try {
                const latDelta = Math.max(0.0025, 0.03 / Math.max(targetZoom, 1));
                const lngDelta = Math.max(0.0025, 0.03 / Math.max(targetZoom, 1));

                if (typeof map4d !== 'undefined' && typeof map4d.LatLngBounds === 'function' && typeof map.fitBounds === 'function') {
                    const bounds = new map4d.LatLngBounds();
                    bounds.extend({ lat: lat - latDelta, lng: lng - lngDelta });
                    bounds.extend({ lat: lat + latDelta, lng: lng + lngDelta });
                    map.fitBounds(bounds, { padding: 40 });
                    return;
                }

                if (typeof map.setCenter === 'function') {
                    map.setCenter({ lat, lng });
                }
                if (typeof map.setZoom === 'function') {
                    map.setZoom(targetZoom);
                }
            } catch (error) {
                console.error('Map4D focus error:', error);
            }
        };

        apply();
        window.requestAnimationFrame(apply);
        window.setTimeout(apply, 120);
        window.setTimeout(apply, 320);
    }

    return {
        ensureSdk,
        createMarker,
        clearObject,
        fitBounds,
        focus
    };
})();
