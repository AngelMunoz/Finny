const worker = new Worker("/~perla~/worker.js");
worker.postMessage({ event: "connect" });

function replaceCssContent({ oldName, name, content }) {
    const css = content?.replace(/(?:\\r\\n|\\r|\\n)/g, '\n') || "";
    const findBy = oldName || name;

    const style = document.querySelector(`[filename=${findBy}]`);
    if (!style) {
        console.warn("Unable to find", oldName, name);
        return;
    }

    style.innerHTML = css;
    style.setAttribute("data-filename", name);
}

worker.addEventListener("message", function({ data }) {
    switch (data?.event) {
        case "reload":
            return window.location.reload();
        case "replace-css":
            return replaceCssContent(data);
        default:
            return console.log('Unknown message:', data);
    }
});
