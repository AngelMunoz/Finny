const worker = new Worker("/~scripts~/worker.js");
worker.postMessage({ event: "connect" });
worker.addEventListener("message", function({ data }) {
  if (data?.event === "reload") {
    window.location.reload();
  }
});
