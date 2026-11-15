// Tela cheia via Fullscreen API (com prefixo webkit para Safari/iPadOS).
(function () {
    const doc = document;
    const root = doc.documentElement;

    const supported = !!(root.requestFullscreen || root.webkitRequestFullscreen);
    const current = () => doc.fullscreenElement || doc.webkitFullscreenElement;

    function sync() {
        root.classList.toggle('is-fullscreen', !!current());
    }

    async function toggle() {
        try {
            if (current()) {
                await (doc.exitFullscreen || doc.webkitExitFullscreen).call(doc);
            } else {
                await (root.requestFullscreen || root.webkitRequestFullscreen).call(root);
            }
        } catch (e) {
            console.warn('Tela cheia indisponível', e);
        }
    }

    root.classList.toggle('fullscreen-supported', supported);
    doc.addEventListener('fullscreenchange', sync);
    doc.addEventListener('webkitfullscreenchange', sync);

    // Delegação: o botão é renderizado pelo Blazor e pode ser recriado a qualquer momento.
    doc.addEventListener('click', e => {
        if (e.target.closest('[data-fullscreen-toggle]')) toggle();
    });
})();

// Usado pela busca: tira o foco do campo para o teclado do celular fechar.
window.binderBlur = el => el && el.blur();

// Avisa o app quando ele volta para a tela ou a internet volta, para reler a coleção do gist.
window.binderWatchVisibility = ref => {
    const ping = () => { if (!document.hidden) ref.invokeMethodAsync('OnVisible'); };
    document.addEventListener('visibilitychange', ping);
    window.addEventListener('online', ping);
    window.addEventListener('focus', ping);
};
