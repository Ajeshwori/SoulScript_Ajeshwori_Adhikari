console.log("Loading editor functions from file...");

window.editorFormat = function (command) {
    console.log("Formatting:", command);
    if (command === "h1" || command === "h2") {
        document.execCommand("formatBlock", false, command);
    } else {
        document.execCommand(command, false, null);
    }
};

window.getEditorContent = function () {
    console.log("Getting editor content...");
    const editor = document.getElementById("editor");
    return editor ? editor.innerHTML : "";
};

window.setEditorContent = function (content) {
    const editor = document.getElementById("editor");
    if (editor) {
        editor.innerHTML = content;
    }
};

console.log("Editor functions loaded from file!");