// Em desenvolvimento não guarda nada: sempre busca a versão nova.
// A versão publicada (com cache para abrir offline) é service-worker.published.js.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('fetch', () => { });
