(function() {
    // If the native app sends a message to the background script, the Safari window pops to the front and steals focus.
    // Also, Safari unloads the native app if comms are idle for ~5 seconds.
    // We can solve both of these problems by polling for messages from Scratch Link.
    // Scratch projects generally run at 30 Hz max, so keep pollFrequency >= 30.
    // The browser will enforce some minimum amount of time (4 ms or more by spec), so at some point making this "faster" won't work.
    // TODO: consider polling for all sessions in this context at once instead of each independently.
    const pollFrequency = 60; // Hz

    const pageSessions = new Map();

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
            // the client/page script needs the outerMessage so it can tell the message is from Scratch Link
            self.postMessage(outerMessage, self.origin);
        }
    });

    /**
     * Handles a message sent by the page and intended for Scratch Link.
     * @param {object} messageToScratchLink - the "to-scratch-link" message from the page.
     * @param {string} origin - the origin of the page that sent the message.
     */
    const onMessageToScratchLink = async (messageToScratchLink, origin) => {
        if (messageToScratchLink.method == "open") {
            const openResponse = await browser.runtime.sendMessage(messageToScratchLink);
            onSessionOpened(openResponse, origin);
        } else {
            const sessionId = messageToScratchLink.session;
            const port = pageSessions.get(sessionId);
            if (port) {
                port.postMessage(messageToScratchLink);
            } else {
                console.error("Scratch Link extension failed to find port for session", sessionId);
            }
        }
    };

    const onSessionOpened = async (response, origin) => {
        // check for an error or otherwise bad response
        const sessionId = response.session;
        if (response.error || !sessionId || response.result !== sessionId) {
            console.error("Scratch Link extension failed to open a session", response);
            return;
        }

        // connect the port and store it in the session set
        const port = browser.runtime.connect({name: sessionId.toString()});
        pageSessions.set(sessionId, port);

        // set up polling for messages from Scratch Link
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
            console.log("Scratch Link extension disconnected a session", sessionId);
            clearInterval(pollInterval);
            pageSessions.delete(response.session);
        });

        // forward messages from Scratch Link to the page
        const onMessageFromScratchLink = messageFromScratchLink => {
            switch (messageFromScratchLink.id) {
                case pollMessageId:
                    --pollPending;
                    handlePollResults(sessionId, messageFromScratchLink.result);
                    break;
                default:
                    self.postMessage({"from-scratch-link": messageFromScratchLink}, origin);
                    break;
            }
        };
        port.onMessage.addListener(onMessageFromScratchLink);

        // now that all the plumbing is ready, forward the 'open' message response to the page
        onMessageFromScratchLink(response);
    };

    const handlePollResults = (sessionId, messages) => {
        if (!messages) {
            return;
        }

        for (let message of messages) {
            self.postMessage({'from-scratch-link': {
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
