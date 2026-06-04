(function() {
    // If the native app sends a message to the background script, the Safari window pops to the front and steals focus.
    // Also, Safari unloads the native app if comms are idle for ~5 seconds.
    // We can solve both of these problems by polling for messages from AluxLabs Link.
    // Scratch projects generally run at 30 Hz max, so keep pollFrequency >= 30.
    // The browser will enforce some minimum amount of time (4 ms or more by spec), so at some point making this "faster" won't work.
    // TODO: consider polling for all sessions in this context at once instead of each independently.
    const pollFrequency = 60; // Hz

    const pageSessions = new Map();

    /**
     * Check for an element in the document with id='aluxlabs-link-extension-script'.
     * If found, inject the socket script into it.
     */
    const injectAluxLabsLinkScript = () => {
        const extensionScriptNode = document.getElementById("aluxlabs-link-extension-script");
        if (extensionScriptNode) {
            extensionScriptNode.innerHTML = ""; // make it safe to change "type"
            extensionScriptNode.type = "module";
            extensionScriptNode.innerHTML = [
               `import("${browser.runtime.getURL("web/aluxlabs-link-safari-socket.mjs")}").then(`,
               "    module => {",
               "        self.Scratch = self.Scratch || {};",
               "        self.Scratch.AluxLabsLinkSafariSocket = module.AluxLabsLinkSafariSocket;",
               "    }",
               ");"
               ].join("\n");
        }
    }

    // This content script runs at "document_idle" (Document.readyState == complete)
    // so a static page should have this element ready by now.
    // If the script element is added dynamically, send the script injection message (see below).
    injectAluxLabsLinkScript();

    // handle messages from the page
    self.addEventListener("message", event => {
        const message = event.data["to-aluxlabs-link"];
        if (message) {
            onMessageToAluxLabsLink(message, event.origin);
        } else if (event.data["inject-aluxlabs-link-script"]) {
            injectAluxLabsLinkScript();
        }
    });

    // handle messages from the background script
    browser.runtime.onMessage.addListener((outerMessage, sender, response) => {
        const message = outerMessage["from-aluxlabs-link"];
        if (message) {
            // the client/page script needs the outerMessage so it can tell the message is from AluxLabs Link
            self.postMessage(outerMessage, self.origin);
        }
    });

    /**
     * Handles a message sent by the page and intended for AluxLabs Link.
     * @param {object} messageToAluxLabsLink - the "to-aluxlabs-link" message from the page.
     * @param {string} origin - the origin of the page that sent the message.
     */
    const onMessageToAluxLabsLink = async (messageToAluxLabsLink, origin) => {
        if (messageToAluxLabsLink.method == "open") {
            const openResponse = await browser.runtime.sendMessage(messageToAluxLabsLink);
            onSessionOpened(openResponse, origin);
        } else {
            const sessionId = messageToAluxLabsLink.session;
            const port = pageSessions.get(sessionId);
            if (port) {
                port.postMessage(messageToAluxLabsLink);
            } else {
                console.error("AluxLabs Link extension failed to find port for session", sessionId);
            }
        }
    };

    const onSessionOpened = async (response, origin) => {
        // check for an error or otherwise bad response
        const sessionId = response.session;
        if (response.error || !sessionId || response.result !== sessionId) {
            console.error("AluxLabs Link extension failed to open a session", response);
            return;
        }

        // connect the port and store it in the session set
        const port = browser.runtime.connect({name: sessionId.toString()});
        pageSessions.set(sessionId, port);

        // set up polling for messages from AluxLabs Link
        // we can reuse the same message repeatedly to save on GC
        const pollMessageId = 'web-extension-poll';
        const sessionPollMessage = {method: 'poll', session: sessionId, id: pollMessageId};

        let pollPending = 0;

        const pollForMessages = () => {
            // if there's no limit here, poll calls can stack up and eventually cause a crash
            // allowing 1 extra request to be inflight might offer a slightly better chance of keeping both ends busy
            if (pollPending > 1) return;
            ++pollPending;
            port.postMessage(sessionPollMessage);
        };

        const pollInterval = setInterval(
            pollForMessages,
            1000 / pollFrequency
        );

        // clean up on disconnect
        port.onDisconnect.addListener(() => {
            console.log("AluxLabs Link extension disconnected a session", sessionId);
            clearInterval(pollInterval);
            pageSessions.delete(response.session);
        });

        // forward messages from AluxLabs Link to the page
        const onMessageFromAluxLabsLink = messageFromAluxLabsLink => {
            switch (messageFromAluxLabsLink.id) {
                case pollMessageId:
                    --pollPending;
                    handlePollResults(sessionId, messageFromAluxLabsLink.result);
                    break;
                default:
                    self.postMessage({"from-aluxlabs-link": messageFromAluxLabsLink}, origin);
                    break;
            }
        };
        port.onMessage.addListener(onMessageFromAluxLabsLink);

        // now that all the plumbing is ready, forward the 'open' message response to the page
        onMessageFromAluxLabsLink(response);
    };

    const handlePollResults = (sessionId, messages) => {
        if (!messages) {
            return;
        }

        for (let message of messages) {
            self.postMessage({'from-aluxlabs-link': {
                session: sessionId,
                data: message
            }}, origin);
        }
    };

    // if this script is about to be unloaded, tell the background script to clean up our sessions
    window.addEventListener('unload', () => {
        browser.runtime.sendMessage('unload');
    });
})();
