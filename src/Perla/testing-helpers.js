export const PERLA_SESSION_START = Symbol('session-start');
export const PERLA_SUITE_START = Symbol('suite-start');
export const PERLA_SUITE_END = Symbol('suite-end');
export const PERLA_TEST_PASS = Symbol('test-pass');
export const PERLA_TEST_FAILED = Symbol('test-failed');
export const PERLA_SESSION_END = Symbol('session-end');
export const PERLA_TEST_IMPORT_FAILED = Symbol('test-import-failed');
export const PERLA_TEST_RUN_FINISHED = Symbol('test-run-finished');

/**
 * @param { PERLA_SESSION_START
 * | PERLA_SUITE_START
 * | PERLA_SUITE_END
 * | PERLA_TEST_PASS
 * | PERLA_TEST_FAILED
 * | PERLA_SESSION_END
 * | PERLA_TEST_IMPORT_FAILED
 * | PERLA_TEST_RUN_FINISHED } event
 * @param {{ stats?: object; test?: object; suite?: object, message?: string, stack?: string }} payload
 * @returns {Promise<void>}
 */
export async function postEvent(event, payload) {
    try {
        await fetch('/~perla~/testing/events', {
            method: 'POST', body: JSON.stringify({event: event.description, ...payload})
        })
            .then(res => !res.ok ? Promise.reject(res.status) : undefined)
    } catch (err) {
        console.error("Failed to notify events to the dev server, this is likely a bug in Perla", err)
    }
}

export async function getFileList() {
    try {
        const result =
            await fetch('/~perla~/testing/files')
                .then(res => res.ok ? res.json() : Promise.reject(res.status))
        return result ?? [];
    } catch (err) {
        console.error("Failed to request the testing settings to the dev server, this is likely a bug in Perla", err)
        return [];
    }
}

export async function getTestSettings() {
    try {
        const result =
            await fetch('/~perla~/testing/settings')
                .then(res => res.ok ? res.json() : Promise.reject(res.status))
        return result ?? {};
    } catch (err) {
        console.error("Failed to request the testing settings to the dev server, this is likely a bug in Perla", err)
    }
}
