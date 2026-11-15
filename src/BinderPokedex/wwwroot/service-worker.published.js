// Guarda o app no aparelho para abrir sem internet. A coleção em si fica no localStorage
// (cópia do gist) e as imagens dos Pokémon ficam num cache próprio conforme são vistas.
self.importScripts('./service-worker-assets.js');

const cacheNamePrefix = 'binder-app-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const spriteCache = 'binder-sprites-v1';
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff2?$/, /\.png$/, /\.svg$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/];
const offlineAssetsExclude = [/^service-worker\.js$/];
const baseUrl = new URL('./', self.location).href;
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);
const spriteHost = 'raw.githubusercontent.com';

self.addEventListener('install', event => event.waitUntil(onInstall()));
self.addEventListener('activate', event => event.waitUntil(onActivate()));
self.addEventListener('fetch', event => {
    const url = new URL(event.request.url);
    if (event.request.method !== 'GET') return;
    if (url.origin === self.location.origin) event.respondWith(onFetchApp(event.request));
    else if (url.hostname === spriteHost) event.respondWith(onFetchSprite(event.request));
    // Chamadas à API do GitHub passam direto (nunca do cache).
});

async function onInstall() {
    const requests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(p => p.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(p => p.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    const cache = await caches.open(cacheName);
    await cache.addAll(requests);
    await self.skipWaiting(); // versão nova vale a partir da próxima vez que o app abrir
}

async function onActivate() {
    const keys = await caches.keys();
    await Promise.all(keys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
    await self.clients.claim();
}

async function onFetchApp(request) {
    // Rotas do app (/binder/41, /pokemon/6…) não são arquivos: todas abrem o index.html.
    const isNavigation = request.mode === 'navigate' && !manifestUrlList.includes(request.url);
    const cache = await caches.open(cacheName);
    const cached = await cache.match(isNavigation ? new URL('index.html', baseUrl).href : request);
    return cached || fetch(request);
}

async function onFetchSprite(request) {
    const cache = await caches.open(spriteCache);
    const cached = await cache.match(request.url);
    if (cached) return cached;
    // Busca com CORS para guardar uma resposta "normal" (respostas opacas ocupam muito espaço na cota).
    const response = await fetch(request.url, { mode: 'cors' });
    if (response.ok) cache.put(request.url, response.clone());
    return response;
}
