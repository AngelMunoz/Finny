self.addEventListener('connect', function(e) {
    console.log('connected');
});

let source;

function connectToSource() {
    if (source) return;
    source = new EventSource("/~perla~/sse");
    source.addEventListener("open", function(event) {
        console.log("Connected");
    });

    source.addEventListener("reload", function(event) {
        console.log("Reloading, file changed: ", event.data);
        self.postMessage({
            event: 'reload'
        });
    });
}

self.addEventListener('message', function({ data }) {
    if (data?.event === 'connect') {
        connectToSource();
    }
});
