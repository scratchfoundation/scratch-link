
const sessionTabMap = new Map();

browser.runtime.onMessage.addListener((request, sender, sendResponse) => {
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
        sendResponse(response);
    });
    // asynchronous response expected?
    return (id !== undefined);
});

const port = browser.runtime.connectNative("application.id");
port.onMessage.addListener(message => {
    const tabID = sessionTabMap.get(message.userInfo.session);
    if (tabID !== undefined) {
        browser.tabs.sendMessage(tabID, { 'from-scratch-link': message.userInfo });
    }
});
