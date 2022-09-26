self.addEventListener('connect', function(e) {
    console.log('connected');
});

let source;

const tryParse = string => {
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
    source.addEventListener("open", function(event) {
        if(needsReload) {
            console.log("Reconnected to server")
            self.postMessage({
                event: 'reload'
            });
            needsReload = false;
            return;
        }
        console.log("Connected");
    });

    source.addEventListener('error', function (event) {
        if(event.target.readyState === EventSource.CONNECTING) {
            needsReload = true;
        }
    })

    source.addEventListener("reload", function(event) {
        console.log("Reloading, file changed: ", event.data);
        self.postMessage({
            event: 'reload'
        });
    });
    source.addEventListener("replace-css", function(event) {
        const {
            oldName,
            name,
            content } = tryParse(event.data);
        console.log(`Css Changed: ${oldName ? oldName : name}`);
        self.postMessage({
            event: 'replace-css',
            oldName,
            name,
            content
        });
    });

    source.addEventListener("compile-err", function(event) {
        const { error } = tryParse(event.data);
        console.error(error);
        self.postMessage({
            event: 'compile-err',
            error
        });
    });
}

self.addEventListener('message', function({ data }) {
    if (data?.event === 'connect') {
        connectToSource();
    }
});
