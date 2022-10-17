import "../../src/components.js";
import { expect } from 'https://jspm.dev/@esm-bundle/chai';
import { fixture } from 'https://jspm.dev/@open-wc/testing-helpers';

describe("Component Tests", () => {
    it('should render <my-element></my-element>', async () => {
        /**
         * @type {import('../../src/components.js').MyElement}
         */
        const element = await fixture("<my-element></my-element>");

        expect(element).to.not.be.undefined;
        expect(element).to.not.be.null;
    });

    it('should have an inner div with [data-value=10]', async () => {
        /**
         * @type {import('../../src/components.js').MyElement}
         */
        const element = await fixture("<my-element></my-element>");
        let innerDiv = element.shadowRoot.querySelector("div");
        expect(innerDiv.getAttribute("data-value")).to.equal("10");
    });
});
