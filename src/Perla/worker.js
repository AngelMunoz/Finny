//@ts-check

self.addEventListener("connect", function (e) {
  console.log("connected");
});

let source;

const tryParse = (string) => {
  try {
    return JSON.parse(string) || {};
  } catch (err) {
    return {};
  }
};

function connectToSource() {
  if (source) return;
  let needsReload = false;
  source = new EventSource("/~perla~/sse");
  source.addEventListener("open", function (event) {
    if (needsReload) {
      console.log("Reconnected to server");
      self.postMessage({
        event: "reload",
      });
      needsReload = false;
      return;
    }
    console.log("Connected");
  });

  source.addEventListener("error", function (event) {
    //@ts-ignore
    if (event.target.readyState === EventSource.CONNECTING) {
      needsReload = true;
    }
  });

  source.addEventListener("reload", function (event) {
    const eventData = tryParse(event.data);
    console.log("Reloading, file changed: ", eventData?.name);
    self.postMessage({
      event: "reload",
    });
  });
  source.addEventListener("replace-css", function (event) {
    const data = tryParse(event.data);
    console.log(
      `Css Changed: ${data.oldName ? data.oldName : data?.url ?? data.name}`
    );
    self.postMessage({ event: "replace-css", ...data });
  });

  source.addEventListener("compile-err", function (event) {
    const { error } = tryParse(event.data);
    console.error(error);
    self.postMessage({
      event: "compile-err",
      error,
    });
  });
}

self.addEventListener("message", function ({ data }) {
  if (data?.event === "connect") {
    connectToSource();
  }
});
