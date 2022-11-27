import { BehaviorSubject } from "https://jspm.dev/rxjs";

/**
 * @template T Type
 */
export class Store {
    /**
     * 
     * @type {BehaviorSubject<T>}
     */
    #subject;
    /**
     * 
     * @type {Observable<T>}
     */
    #obs;
    /**
     * 
     * @param {T} state
     */
    constructor(state) {
        this.#subject = new BehaviorSubject(state);
        this.#obs = this.#subject.asObservable();
    }

    /**
     * 
     * @returns {T}
     */
    get value() {
        return this.#subject.getValue();
    }

    /**
     * 
     * @returns {Observable<T>}
     */
    get state() {
        return this.#obs;
    }

    /**
     * 
     * @param {(param: T) => T} updateFn
     */
    update(updateFn) {
        const value = updateFn(this.value);
        this.#subject.next(value);
    }

    /**
     * 
     * @param {T} value
     */
    setValue(value) {
        this.#subject.next(value);
    }
}