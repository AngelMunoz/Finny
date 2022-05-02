export async function sendMessage(content) {
    console.log(content);
    await fetch('/~nacre~/messages', {
        body: JSON.stringify(content),
        method: 'POST'
    }).then(res => res.ok ? res.json() : Promise.reject(new Error(`${res.status} - ${res.statusText}`)));
}

export async function sendMessageWaitForResponse(content) {
    console.log(content);
    return { executed: false, result: {} };
}