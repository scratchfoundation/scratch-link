
const sessionTabMap = new Map();

browser.runtime.onMessage.addListener((request, sender, sendResponse) => {
    console.log("Received request: ", request, " from ", sender);

    const {session, method, params, id} = request;

    if (session && (method === 'open')) {
        sessionTabMap.set(session, sender.tab.id);
    }

    browser.runtime.sendNativeMessage('application.id', {
        session,
        method,
        params,
        id
    }, response => {
        console.log("Sending response: ", response);
        sendResponse(response);
    });
    // asynchronous response expected?
    return (session !== undefined && id !== undefined);
});

const port = browser.runtime.connectNative("application.id");
port.onMessage.addListener(message => {
    console.log("background port received: ", message);
    const tabID = sessionTabMap.get(message.session);
    if (tabID !== undefined) {
        browser.tabs.sendMessage(tabID, message);
    }
});
