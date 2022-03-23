browser.runtime.onMessage.addListener((request, sender, sendResponse) => {
    console.log("Received request: ", request, " from ", sender);
    
    // sendResponse must be called synchronously, before returning from the event handler
    if (request.greeting === "hello")
        sendResponse({ farewell: "goodbye" });

    const senderTabId = sender.tab.id;
    setTimeout(() => {
        console.log("background sending delayed message to tab", senderTabId);
        browser.tabs.sendMessage(senderTabId, "delayed message from background script");
    }, 5 * 1000);
});

browser.runtime.sendNativeMessage("application.id", {message: "Hello from background page"}, function(response) {
    console.log("Received sendNativeMessage response:");
    console.log(response);
});

const port = browser.runtime.connectNative("application.id");
port.onMessage.addListener(message => {
    console.log("background port received: ", message);
});
