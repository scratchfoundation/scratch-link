const extensionVersion = "1.0.0";

(() => {
    const detectionNode = document.getElementById("scratch-link-extension-detect");
    if (detectionNode) {
        detectionNode.innerText = extensionVersion;
    }
})();

// forward 'to-scratch-link' messages to the background script
self.addEventListener("message", event => {
    const message = event.data["to-scratch-link"];
    if (message) {
        console.log("content self.addEventListener('message'): ", event.data);
        browser.runtime.sendMessage(message).then(response => {
            console.log("content response from background: ", response);
            self.postMessage({"from-scratch-link": response}, event.origin);
        });
    }
});

browser.runtime.onMessage.addListener((outerMessage, sender, response) => {
    const message = outerMessage["from-scratch-link"];
    if (message) {
        // the client/page script needs the outerMessage so it can tell the message is from Scratch Link
        self.postMessage(outerMessage, self.origin);
    }
});
