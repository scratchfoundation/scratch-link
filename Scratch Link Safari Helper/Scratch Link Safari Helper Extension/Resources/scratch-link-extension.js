(function() {
    // Safari unloads the native app if comms are idle for ~5 seconds
    // Set this value to less than half that, so we can miss one and still have some wiggle room
    const keepaliveTime = 2000; // milliseconds
    let mostRecentActivity;

    const pageSessions = new Map();

    /**
     * Refresh the keepalive timer. Call this when any message goes to or comes from the native app.
     * Do not call this for activity handled completely in JavaScript, such as script injection.
     */
    const keepaliveRefresh = () => {
        mostRecentActivity = Date.now();
    };

    /**
     * Check if the last activity was so long ago that we need to send a keepalive message.
     */
    const isKeepaliveExpired = () => {
        return Date.now() > mostRecentActivity + keepaliveTime;
    };

    /**
     * Check for an element in the document with id='scratch-link-extension-script'.
     * If found, inject the socket script into it.
     */
    const injectScratchLinkScript = () => {
        const extensionScriptNode = document.getElementById("scratch-link-extension-script");
        if (extensionScriptNode) {
            extensionScriptNode.innerHTML = ""; // make it safe to change "type"
            extensionScriptNode.type = "module";
            extensionScriptNode.innerHTML = [
               `import("${browser.runtime.getURL("web/scratch-link-safari-socket.mjs")}").then(`,
               "    module => {",
               "        self.Scratch = self.Scratch || {};",
               "        self.Scratch.ScratchLinkSafariSocket = module.ScratchLinkSafariSocket;",
               "    }",
               ");"
               ].join("\n");
        }
    }

    // This content script runs at "document_idle" (Document.readyState == complete)
    // so a static page should have this element ready by now.
    // If the script element is added dynamically, send the script injection message (see below).
    injectScratchLinkScript();

    // handle messages from the page
    self.addEventListener("message", event => {
        const message = event.data["to-scratch-link"];
        if (message) {
            onMessageToScratchLink(message, event.origin);
        } else if (event.data["inject-scratch-link-script"]) {
            injectScratchLinkScript();
        }
    });

    // handle messages from the background script
    browser.runtime.onMessage.addListener((outerMessage, sender, response) => {
        const message = outerMessage["from-scratch-link"];
        if (message) {
            keepaliveRefresh();
            // the client/page script needs the outerMessage so it can tell the message is from Scratch Link
            self.postMessage(outerMessage, self.origin);
        }
    });

    /**
     * Handles a message sent by the page and intended for Scratch Link.
     * @param {object} messageToScratchLink - the "to-scratch-link" message from the page.
     * @param {string} origin - the origin of the page that sent the message.
     */
    const onMessageToScratchLink = (messageToScratchLink, origin) => {
        if (messageToScratchLink.method == "open") {
            keepaliveRefresh();
            browser.runtime.sendMessage(messageToScratchLink).then(response => {
                const sessionId = response.session;
                if (!response.error && sessionId && response.result === sessionId) {
                    const port = browser.runtime.connect({name: sessionId.toString()});
                    port.onDisconnect.addListener(() => {
                        console.log("Scratch Link extension disconnected a session", sessionId);
                        pageSessions.delete(response.session);
                    });
                    const onMessageFromScratchLink = messageFromScratchLink => {
                        keepaliveRefresh();
                        self.postMessage({"from-scratch-link": messageFromScratchLink}, origin);
                    };
                    port.onMessage.addListener(onMessageFromScratchLink);
                    pageSessions.set(sessionId, port);
                    onMessageFromScratchLink(response);
                } else {
                    console.error("Scratch Link extension failed to open a session", response);
                }
            });
        } else {
            const sessionId = messageToScratchLink.session;
            const port = pageSessions.get(sessionId);
            if (port) {
                keepaliveRefresh();
                port.postMessage(messageToScratchLink);
            } else {
                console.error("Scratch Link extension failed to find port for session", sessionId);
            }
        }
    };

    window.addEventListener('unload', () => {
        browser.runtime.sendMessage('unload');
    });

    // We only need one keepalive even if we have a bunch of sessions
    // but if we don't have any sessions at all, we don't need a keepalive
    // also, if there's already activity going on then a keepalive is unnecessary and might even hurt throughput slightly
    const keepaliveMessage = {method: 'keepalive'};
    const keepaliveInterval = setInterval(() => {
        if (pageSessions.size > 0 && isKeepaliveExpired()) {
            keepaliveRefresh();
            browser.runtime.sendMessage(keepaliveMessage);
        }
    }, keepaliveTime);
})();
