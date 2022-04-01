
const sessionTabMap = new Map();

browser.runtime.onMessage.addListener((request, sender, sendResponse) => {
    console.log("Received request: ", request, " from ", sender);

    const {session, method, params, id} = request;

    browser.runtime.sendNativeMessage('application.id', {
        session,
        method,
        params,
        id
    }, response => {
        if (method === 'open') {
            sessionTabMap.set(response.result, sender.tab.id);
        }
        console.log("Sending response: ", response);
        sendResponse(response);
    });
    // asynchronous response expected?
    return (id !== undefined);
});

const port = browser.runtime.connectNative("application.id");
port.onMessage.addListener(message => {
    console.log("background port received: ", message);
    const tabID = sessionTabMap.get(message.userInfo.session);
    if (tabID !== undefined) {
        browser.tabs.sendMessage(tabID, message.userInfo.message);
    }
});
