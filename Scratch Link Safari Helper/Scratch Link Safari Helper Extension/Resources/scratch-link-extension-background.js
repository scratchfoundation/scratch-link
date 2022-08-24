
// this ID is ignored: the background script is only allowed to communicate with its associated app
const appId = 'application.id';

// map of session ID to {tabId, options} where options = {frameId}
// see also browser.tabs.sendMessage
const sessionTabMap = new Map();

// handle a message from a content script
browser.runtime.onMessage.addListener((request, sender, sendResponse) => {

    // handle any special messages that don't fit the JSON-RPC format
    switch (request) {
        // the content script reports that its window / tab / frame is being unloaded
        case 'unload':
            onSenderUnload(sender);
            return false; // no, don't expect an async call to sendResponse

        default:
            // fall through
    }

    // unpack a JSON-RPC-like message
    const {session, method, params, id} = request;

    // forward the message to the native app
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

    // if the message contained an ID, tell the browser we're expecting an asynchronous call to sendResponse
    return (id !== undefined);
});

// handle a connection coming from a content script
browser.runtime.onConnect.addListener(port => {
    // handle a message from the content script session associated with this port
    port.onMessage.addListener(messageToScratchLink => {
        const {session, method, params, id} = messageToScratchLink;

        // forward the message to the native app (which will forward it to Scratch Link)
        browser.runtime.sendNativeMessage(appId, {
            session,
            method,
            params,
            id
        }, responseFromScratchLink => {
            // send the native app's response back to the content script session
            port.postMessage(responseFromScratchLink);
        } );
    } );
});

// handle a client unloading
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
