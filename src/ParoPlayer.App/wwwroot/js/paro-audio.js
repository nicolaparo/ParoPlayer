// The audio element behind BrowserPlayerService (browser session, #26). The server drives it through JS interop
// and polls state() for the position, so nothing here calls back into .NET.
const audio = new Audio();
audio.preload = "auto";

window.paroAudio = {
    load(url, play) {
        audio.src = url;
        if (play) {
            return audio.play().catch(() => { });
        }

        audio.load();
    },
    play() {
        return audio.play().catch(() => { });
    },
    pause() {
        audio.pause();
    },
    seek(seconds) {
        audio.currentTime = seconds;
    },
    stop() {
        audio.pause();
        audio.removeAttribute("src");
        audio.load();
    },
    state() {
        return {
            position: audio.currentTime || 0,
            duration: Number.isFinite(audio.duration) ? audio.duration : 0,
            paused: audio.paused,
            ended: audio.ended,
        };
    },
};
