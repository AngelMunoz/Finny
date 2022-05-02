export function add(a, b) {
    if (typeof a !== 'number' || typeof b !== 'number') {
        throw new TypeError("Both parameters must be numbers");
    }
    return (+a) + (+b);
}
