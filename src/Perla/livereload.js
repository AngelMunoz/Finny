//@ts-check

const worker = new Worker("/~perla~/worker.js");
worker.postMessage({ event: "connect" });

function replaceCssContent({ oldName, oldPath, name, content, url }) {
  const css = content?.replace(/(?:\\r\\n|\\r|\\n)/g, "\n") || "";
  const findBy = oldPath || url || name;

  const style = document.querySelector(`[url="${findBy}"]`);

  if (!style) {
    console.warn("Unable to find", oldName, name);
    console.warn("Reloading in 1.5s...");
    setTimeout(() => {
      window.location.reload();
    }, 1500);
    return;
  }

  style.innerHTML = css;
  style.setAttribute("url", oldPath ? url : findBy);
}

function showOverlay({ error }) {
  console.log("show overlay", error);
}

worker.addEventListener("message", function ({ data }) {
  switch (data?.event) {
    case "reload":
      return window.location.reload();
    case "replace-css":
      return replaceCssContent(data);
    case "compile-err":
      return showOverlay(data);
    default:
      return console.log("Unknown message:", data);
  }
});
