console.log("Loading editor functions...");

function getEditor() {
    return document.getElementById("editor");
}

function getSelection(editor) {
    const start = editor.selectionStart ?? 0;
    const end = editor.selectionEnd ?? 0;
    return { start, end };
}

function replaceRange(editor, start, end, newText, placeCursorInsidePrefixLen = null) {
    const before = editor.value.substring(0, start);
    const after = editor.value.substring(end);

    editor.value = before + newText + after;

    let cursor = (before + newText).length;
    if (placeCursorInsidePrefixLen !== null) cursor = before.length + placeCursorInsidePrefixLen;

    editor.selectionStart = cursor;
    editor.selectionEnd = cursor;
    editor.focus();
    editor.dispatchEvent(new Event("input"));
}

// “Word-like” selection: selects letters/numbers/_ (not punctuation)
function getWordOrSelection(editor) {
    const text = editor.value;
    let { start, end } = getSelection(editor);

    if (start !== end) return { start, end, text: text.substring(start, end) };

    const isWordChar = (ch) => /[\p{L}\p{N}_]/u.test(ch); // Unicode letters/numbers [web:26]

    // If cursor is between non-word chars, treat as empty selection
    const at = start < text.length ? text[start] : "";
    const before = start > 0 ? text[start - 1] : "";
    if (!isWordChar(at) && !isWordChar(before)) {
        return { start, end, text: "" };
    }

    let left = start;
    while (left > 0 && isWordChar(text[left - 1])) left--;

    let right = start;
    while (right < text.length && isWordChar(text[right])) right++;

    return { start: left, end: right, text: text.substring(left, right) };
}

function getCurrentLineRange(editor) {
    const text = editor.value;
    const pos = editor.selectionStart ?? 0;

    let lineStart = pos;
    while (lineStart > 0 && text[lineStart - 1] !== "\n") lineStart--;

    let lineEnd = pos;
    while (lineEnd < text.length && text[lineEnd] !== "\n") lineEnd++;

    return { lineStart, lineEnd, line: text.substring(lineStart, lineEnd) };
}

window.editorFormat = function (command) {
    const editor = getEditor();
    if (!editor) return;

    // line-based formatting
    if (command === "h1" || command === "h2" || command === "ul" || command === "ol") {
        const { lineStart, lineEnd, line } = getCurrentLineRange(editor);

        if (command === "h1" || command === "h2") {
            const clean = line.replace(/^#{1,6}\s+/, "");
            const prefix = command === "h1" ? "# " : "## ";
            replaceRange(editor, lineStart, lineEnd, prefix + clean);
            return;
        }

        if (command === "ul") {
            const clean = line.replace(/^(\s*)(-|\d+\.)\s+/, "$1");
            replaceRange(editor, lineStart, lineEnd, "- " + clean);
            return;
        }

        if (command === "ol") {
            let clean = line.replace(/^(\s*)([-*]|\d+\.)\s+/, "$1");

            const text = editor.value;
            const prevLineEnd = Math.max(0, lineStart - 1);
            let prevLineStart = prevLineEnd;
            while (prevLineStart > 0 && text[prevLineStart - 1] !== "\n") prevLineStart--;
            const prevLine = text.substring(prevLineStart, prevLineEnd);

            const prevMatch = prevLine.match(/^(\s*)(\d+)\.\s+/);
            if (prevMatch) {
                const indent = prevMatch[1] ?? "";
                const prevNum = parseInt(prevMatch[2], 10);
                const nextNum = isNaN(prevNum) ? 1 : prevNum + 1;
                replaceRange(editor, lineStart, lineEnd, `${indent}${nextNum}. ${clean}`);
                return;
            }

            const indentMatch = line.match(/^(\s*)/);
            const indent = indentMatch ? indentMatch[1] : "";
            replaceRange(editor, lineStart, lineEnd, `${indent}1. ${clean}`);
            return;
        }
    }

    // inline formatting
    const w = getWordOrSelection(editor);
    const selected = w.text;
    const isEmpty = !selected || selected.trim().length === 0;

    const wrap = (prefix, suffix = prefix) => {
        if (isEmpty) {
            replaceRange(editor, w.start, w.end, prefix + suffix, prefix.length);
            return;
        }
        replaceRange(editor, w.start, w.end, `${prefix}${selected}${suffix}`);
    };

    switch (command) {
        case "bold":
            wrap("**"); // common Markdown strong syntax [web:57]
            break;

        case "italic":
            wrap("*"); // common Markdown emphasis syntax [web:57]
            break;

        case "underline":
            wrap("<u>", "</u>"); // works if renderer allows inline HTML
            break;

        case "link": {
            // Markdown link: [text](url) [web:40]
            if (isEmpty) {
                replaceRange(editor, w.start, w.end, "[](https://)", 1);
                return;
            }
            const md = `[${selected}](https://)`;
            replaceRange(editor, w.start, w.end, md, (`[${selected}](`).length);
            break;
        }
    }
};

window.getEditorContent = function () {
    const editor = getEditor();
    return editor ? editor.value : "";
};

window.setEditorContent = function (content) {
    const editor = getEditor();
    if (!editor) return;
    editor.value = content ?? "";
    editor.dispatchEvent(new Event("input"));
};

window.continueListIfNeeded = function () {
    const editor = getEditor();
    if (!editor) return;

    const pos = editor.selectionStart ?? 0;
    const text = editor.value;

    let lineStart = pos;
    while (lineStart > 0 && text[lineStart - 1] !== "\n") lineStart--;

    const lineToCursor = text.substring(lineStart, pos);

    const bulletMatch = lineToCursor.match(/^(\s*)-\s+/);
    if (bulletMatch) {
        const indent = bulletMatch[1] ?? "";
        replaceRange(editor, pos, pos, "\n" + indent + "- ");
        return;
    }

    const orderedMatch = lineToCursor.match(/^(\s*)(\d+)\.\s+/);
    if (orderedMatch) {
        const indent = orderedMatch[1] ?? "";
        const num = parseInt(orderedMatch[2], 10);
        const next = isNaN(num) ? 1 : num + 1;
        replaceRange(editor, pos, pos, "\n" + indent + next + ". ");
        return;
    }
};

window.downloadFile = function (fileName, contentType, content, isBase64) {
    let blob;

    if (isBase64) {
        // Allow both raw base64 and data URLs: data:...;base64,xxxx
        const b64 = (content && content.includes("base64,"))
            ? content.split("base64,")[1]
            : content;

        const byteCharacters = atob((b64 ?? "").replace(/\s/g, ""));
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        blob = new Blob([byteArray], { type: contentType });
    } else {
        blob = new Blob([content], { type: contentType });
    }

    const url = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
};

window.getThemeMode = function () {
    const html = document.documentElement;
    return html.classList.contains("mud-dark-theme");
};

window.setThemeMode = function (isDark) {
    const html = document.documentElement;
    if (isDark) html.classList.add("mud-dark-theme");
    else html.classList.remove("mud-dark-theme");

    window.dispatchEvent(new CustomEvent("themechange", { detail: { isDark } }));
};
