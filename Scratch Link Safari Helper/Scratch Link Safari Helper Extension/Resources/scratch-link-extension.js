(function() {

    const pageSessions = new Map();

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

    self.addEventListener("message", event => {
        const message = event.data["to-scratch-link"];
        if (message) {
            onMessageToScratchLink(message, event.origin);
        } else if (event.data["inject-scratch-link-script"]) {
            injectScratchLinkScript();
        }
    });

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
    const onMessageToScratchLink = (messageToScratchLink, origin) => {
        if (messageToScratchLink.method == "open") {
            browser.runtime.sendMessage(messageToScratchLink).then(response => {
                const sessionId = response.session;
                if (!response.error && sessionId && response.result === sessionId) {
                    const port = browser.runtime.connect({name: sessionId.toString()});
                    port.onDisconnect.addListener(() => {
                        console.log("Scratch Link extension disconnected a session", sessionId);
                        pageSessions.delete(response.session);
                    });
                    const onMessageFromScratchLink =
                        messageFromScratchLink => self.postMessage({"from-scratch-link": messageFromScratchLink}, origin);
                    port.onMessage.addListener(onMessageFromScratchLink);
                    pageSessions.set(sessionId, port);
                    console.log("Scratch Link extension opened a session", sessionId);
                    onMessageFromScratchLink(response);
                } else {
                    console.error("Scratch Link extension failed to open a session", response);
                }
            });
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
})();
