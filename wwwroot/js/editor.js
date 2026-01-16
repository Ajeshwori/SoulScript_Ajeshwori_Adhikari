console.log("Loading editor functions...");

function getEditor() {
    return document.getElementById("editor");
}


  // Rich Text Editor

window.editorFormat = function (command) {
    const editor = getEditor();
    if (!editor) return;

    editor.focus();

    switch (command) {
        case "bold":
        case "italic":
        case "underline":
            document.execCommand(command);
            break;

        case "h1":
            document.execCommand("formatBlock", false, "h1");
            break;

        case "h2":
            document.execCommand("formatBlock", false, "h2");
            break;

        case "ul":
            document.execCommand("insertUnorderedList");
            break;

        case "ol":
            document.execCommand("insertOrderedList");
            break;

        case "link":
            const selection = window.getSelection();
            if (!selection || selection.toString().trim() === "") {
                alert("Select text first");
                return;
            }

            const url = prompt("Enter the link URL:", "https://");
            if (!url) return;

            document.execCommand("createLink", false, url.trim());
            break;
    }

    // Notify Blazor that content changed
    editor.dispatchEvent(new Event("input"));
};

window.getEditorContent = function () {
    const editor = getEditor();
    return editor ? editor.innerHTML : "";
};

window.setEditorContent = function (content) {
    const editor = getEditor();
    if (editor) editor.innerHTML = content ?? "";
};


  // File Download Helper
 
if (typeof window.downloadFile !== "function") {
    window.downloadFile = function (fileName, contentType, content, isBase64) {
        let blob;

        if (isBase64) {
            const b64 = content.includes("base64,")
                ? content.split("base64,")[1]
                : content;

            const bytes = atob(b64 || "");
            const buffer = new Uint8Array(bytes.length);

            for (let i = 0; i < bytes.length; i++) {
                buffer[i] = bytes.charCodeAt(i);
            }

            blob = new Blob([buffer], { type: contentType });
        } else {
            blob = new Blob([content], { type: contentType });
        }

        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");

        a.href = url;
        a.download = fileName;
        a.click();

        URL.revokeObjectURL(url);
    };

    console.log("Injected downloadFile fallback.");
}


  // Theme Handling

window.getThemeMode = function () {
    return document.documentElement.classList.contains("mud-dark-theme");
};

window.setThemeMode = function (isDark) {
    document.documentElement.classList.toggle("mud-dark-theme", isDark);

    window.dispatchEvent(
        new CustomEvent("themechange", { detail: { isDark } })
    );
};

/* =======================
   Keyboard Listener
======================= */
window.globalKeyHandler = null;

window.addKeyboardListener = function (dotnetHelper) {
    window.removeKeyboardListener();

    window.globalKeyHandler = (e) => {
        if ((e.key >= "0" && e.key <= "9") || e.key === "Backspace") {
            dotnetHelper.invokeMethodAsync("HandleKeyPress", e.key);
        }
    };

    document.addEventListener("keydown", window.globalKeyHandler);
};

window.removeKeyboardListener = function () {
    if (window.globalKeyHandler) {
        document.removeEventListener("keydown", window.globalKeyHandler);
        window.globalKeyHandler = null;
    }
};
