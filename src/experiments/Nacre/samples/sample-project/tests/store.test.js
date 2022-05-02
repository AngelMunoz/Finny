import { expect } from 'https://jspm.dev/@esm-bundle/chai';
import { Store } from "../src/store.js";

describe("Store Tests", () => {
    it('should set initial value to constructor arg', () => {
        const store = new Store(10);
        expect(store.value).to.be.equal(10);
    });

    it('should apply update function', () => {
        const store = new Store(10);
        store.update(current => current + 10);
        expect(store.value).to.be.equal(20);
    });

    it('should set a new value', () => {
        const store = new Store(10);
        store.setValue(100);
        expect(store.value).to.be.equal(100);
    });
});
