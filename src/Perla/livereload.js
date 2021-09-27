const worker = new Worker("/~perla~/worker.js");
worker.postMessage({ event: "connect" });
worker.addEventListener("message", function({ data }) {
  if (data?.event === "reload") {
    window.location.reload();
  }
});
