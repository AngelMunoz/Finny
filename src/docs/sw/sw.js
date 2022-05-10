importScripts(
    'https://storage.googleapis.com/workbox-cdn/releases/6.4.1/workbox-sw.js'
);

workbox.setConfig({ debug: true });
const { strategies, routing } = workbox;
const pathEndsWith = (url, ext) => url?.pathname?.endsWith(ext);
const hostContains = (url, ext) => url?.host?.includes(ext);
const isGet = (request) => request.method === 'GET';

routing.registerRoute(
    ({ event, request, url }) => {
        return isGet(request) && request.destination === 'image'
    },
    new strategies.CacheFirst({ cacheName: 'images' })
);

routing.registerRoute(
    ({ event, request, url }) => {
        return isGet(request) && pathEndsWith(url, ".md")
    },
    new strategies.CacheFirst({ cacheName: 'markdown' })
);

routing.registerRoute(
    ({event, request, url}) =>
        isGet(request)
        && request.destination === 'script'
        && !url?.pathname.includes("~perla~")
        && !(hostContains(url, "cdn.skypack.dev") || hostContains(url, "cdn.jsdelivr.net") || hostContains(url, "ga.jspm.io")),
    new strategies.StaleWhileRevalidate({ cacheName: 'scripts' })
);

routing.registerRoute(
    ({event, request, url}) =>
        isGet(request)
        && request.destination === 'script'
        && !url?.pathname.includes("~perla~")
        && (hostContains(url, "cdn.skypack.dev") || hostContains(url, "cdn.jsdelivr.net") || hostContains(url, "ga.jspm.io")),
    new strategies.CacheFirst({ cacheName: 'cdn-cache' })
);


routing.registerRoute(
    ({event, request, url}) => isGet(request) && request.destination === 'style',
    new strategies.StaleWhileRevalidate({ cacheName: 'styles' })
);
