// Remembers how far the Library page was scrolled so Back can put it there again.
// A capture-phase click handler runs before a link navigates away, while the page is still laid out.
let offset = 0;

document.addEventListener("click", () => {
    const main = document.querySelector("main");
    if (main && main.querySelector("[data-remember-scroll]")) {
        offset = main.scrollTop;
    }
}, true);

export function restore() {
    const main = document.querySelector("main");
    if (!main) {
        return;
    }

    // Two frames: the page-enter animation and the first layout of a long list must be done.
    requestAnimationFrame(() => requestAnimationFrame(() => {
        main.scrollTop = offset;
    }));
}
