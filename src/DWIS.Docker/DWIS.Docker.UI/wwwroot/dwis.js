// Small helpers for the D-WIS Docker UI. Loaded as _content/DWIS.Docker.UI/dwis.js
window.dwis = {
    // Copies text to the clipboard. navigator.clipboard needs a secure context (https or localhost),
    // so fall back to a hidden textarea when the app is served over plain http from another machine.
    copyText: async function (text) {
        try {
            if (navigator.clipboard && window.isSecureContext) {
                await navigator.clipboard.writeText(text);
                return true;
            }
        } catch (e) {
            // fall through
        }
        try {
            const ta = document.createElement("textarea");
            ta.value = text;
            ta.setAttribute("readonly", "");
            ta.style.position = "fixed";
            ta.style.left = "-9999px";
            document.body.appendChild(ta);
            ta.select();
            const ok = document.execCommand("copy");
            document.body.removeChild(ta);
            return ok;
        } catch (e) {
            return false;
        }
    },

    scrollToBottom: function (id) {
        const el = document.getElementById(id);
        if (el) el.scrollTop = el.scrollHeight;
    }
};
