
const appId = 'application.id';

// map of session ID to {tabId, options} where options = {frameId}.
// see also browser.tabs.sendMessage
const sessionTabMap = new Map();

browser.runtime.onMessage.addListener((request, sender, sendResponse) => {
    if (request === 'unload') {
        return onSenderUnload(sender);
    }

    const {session, method, params, id} = request;

    browser.runtime.sendNativeMessage(appId, {
        session,
        method,
        params,
        id
    }, response => {
        if (method === 'open') {
            sessionTabMap.set(response.result, {tabId: sender.tab.id, options: {frameId: sender.frameId}});
        }
        sendResponse(response);
    });
    // asynchronous response expected?
    return (id !== undefined);
});

const port = browser.runtime.connectNative(appId);
port.onMessage.addListener(message => {
    const clientInfo = sessionTabMap.get(message.userInfo.session);
    if (clientInfo) {
        browser.tabs.sendMessage(clientInfo.tabId, { 'from-scratch-link': message.userInfo }, clientInfo.options);
    }
});

browser.runtime.onConnect.addListener(port => {
    port.onMessage.addListener(messageToScratchLink => {
        const {session, method, params, id} = messageToScratchLink;

        browser.runtime.sendNativeMessage(appId, {
            session,
            method,
            params,
            id
        }, responseFromScratchLink => {
            port.postMessage(responseFromScratchLink);
        } );
    } );
});

const onSenderUnload = sender => {
    sessionTabMap.forEach((clientInfo, sessionId) => {
        if (sender.tab.id != clientInfo.tabId || sender.frameId != clientInfo.options.frameId) {
            return;
        }

        console.log('client went away: cleaning up session', sessionId);

        sessionTabMap.delete(sessionId);

        // tell the native app to close the session, which should close the corresponding port and socket
        browser.runtime.sendNativeMessage(appId, {
            session: sessionId,
            method: 'close'
        });
    });
};
