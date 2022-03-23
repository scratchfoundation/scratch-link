// web page -> background script
self.addEventListener("message", event => {
    const message = event.data["to-scratch-link"];
    if (message) {
        console.log("content received from page: ", message);
        browser.runtime.sendMessage(message).then(response => {
            console.log("content response from background: ", response);
            self.postMessage({"from-scratch-link": response}, event.origin);
        });
    }
});

// background script -> web page
browser.runtime.onMessage.addListener((request, sender, sendResponse) => {
    console.log("content received from background: ", request);
    self.postMessage(request, self.origin);
});
