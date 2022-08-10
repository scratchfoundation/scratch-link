(function() {
    const injectScratchLinkScript = () => {
        const extensionScriptNode = document.getElementById("scratch-link-extension-script");
        if (extensionScriptNode) {
            extensionScriptNode.innerHTML = ""; // make it safe to change "type"
            extensionScriptNode.type = "module";
            extensionScriptNode.innerHTML = [
               `import("${browser.runtime.getURL("web/ScratchLinkSafariSocket.mjs")}").then(`,
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

    // forward 'to-scratch-link' messages to the background script
    self.addEventListener("message", event => {
        const message = event.data["to-scratch-link"];
        if (message) {
            browser.runtime.sendMessage(message).then(response => {
                self.postMessage({"from-scratch-link": response}, event.origin);
            });
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
})();
