
/**
 * How long should we wait for a response to `open` before throwing a timeout?
 */
const openTimeout = 5000; // milliseconds

/**
 * Has the message listener been installed?
 */
let listenerInstalled = false;

/**
 * Map of session ID to session object.
 */
const sessions = new Map();

/**
 * Map of request ID to pending Promise.
 */
const openRequests = new Map();

/**
 * Find a randomized ID value which is not occupied in the given map.
 * @param {Map} map - the map to check for ID usage
 * @returns {*} - an unused ID for use in the given map
 */
const getUnusedID = function (map) {
    const candidate = new Uint32Array(1);
    do {
        crypto.getRandomValues(candidate);
    } while (map.has(candidate[0]));
    return candidate[0];
}

/**
 * Add response handlers to the pending request map and return a new ID to use for the request.
 * @param {Object} handlers - the functions to handle the result of the request
 * @property {function} resolve - the function to handle a response
 * @property {function} reject - the function to handle an error which prevented the request or its response
 * @returns - the ID under which the handlers were registered
 */
const registerResponseHandlers = function (handlers) {
    const id = getUnusedID(openRequests);
    openRequests.set(id, handlers);
    return id;
};

/**
 * Inspect a message event and, if it's relevant to us, dispatch it to the appropriate response handler or session.
 * @param {MessageEvent} event - the message event to handle
 */
const messageListener = function (event) {
    const message = event.data['from-scratch-link'];
    if (!message) return;
    if (message.id && openRequests.has(message.id)) {
        const handlers = openRequests.get(message.id);
        if (message.error) {
            handlers.reject(message.error);
        } else {
            handlers.resolve(message.result);
        }
        openRequests.delete(message.id);
    } else {
        const session = sessions.get(message.session);
        if (session) {
            session._handleMessageWrapper(message.data);
        }
    }
};

/**
 * Install the message listener if it hasn't been installed yet.
 */
const installListener = () => {
    if (listenerInstalled) return;
    self.addEventListener('message', messageListener);
    listenerInstalled = true;
};

/**
 * ScratchLinkSafariSocket class: represents a Scratch Link session socket using the Safari extension as transport.
 */
class ScratchLinkSafariSocket {

    /**
     * Check if it appears that the extension is active and this socket class can be used.
     * @returns {boolean} - true if it appears that the extension is installed, running, and compatible.
     */
    static isSafariHelperCompatible () {
        // Now that this script is imported straight from the extension, its existence implies compatibility.
        // In the future we might want to put some other checks in here.
        return true;
    }

    /**
     * Construct a new Scratch Link session socket.
     * @param {string} type - the type of session, like 'ble'
     */
    constructor (type) {
        this._type = type;

        this._onOpen = null;
        this._onClose = null;
        this._onError = null;
        this._handleMessage = null;
        this._id = null;
    }

    /**
     * Open communication with Scratch Link.
     * Calls the `onOpen` callback when the connection is established.
     */
    open () {
        const openParams = {};
        switch (this._type) {
        case 'BLE':
            openParams.type = 'ble';
            break;
        case 'BT':
            openParams.type = 'bt';
            break;
        default:
            throw new Error('Unknown session type: ' + this._type);
        }
        installListener();
        Promise.race([
            this._sendRequest('open', openParams),
            new Promise((resolve, reject) => {
                setTimeout(() => {
                    reject(new Event('error')) // mimic behavior in other browsers
                }, openTimeout);
            })
        ]).then(
            result => {
                this._id = result;
                sessions.set(this._id, this);
                this._onOpen();
            },
            error => {
                this._onError(error);
            }
        );
    }

    /**
     * @returns {boolean} - true if the socket is open, false otherwise
     */
    isOpen () {
        return !!this._id;
    }

    /**
     * Close the socket session.
     */
    close () {
        if (this.isOpen()) {
            this._sendNotify('close');
            this._close();
        }
    }

    /**
     * Internal method to clean up when the socket is closed.
     */
    _close () {
        if (this._id) {
            sessions.delete(this._id);
            this._onClose(new CloseEvent('close'));
            this._id = null;
        }
    }

    /**
     * Send a message to Scratch Link
     * @param {object} messageObject - a JSON-RPC 2.0 message object
     */
    sendMessage (messageObject) {
        if (this.isOpen()) {
            if (typeof messageObject.id === 'undefined') {
                this._sendNotify('send', messageObject);
            } else {
                this._sendRequest('send', messageObject).then(result => {
                    this._handleMessage(result);
                });
            }
        }
    }

    /**
     * @param {function} callback - the function to call when the socket is opened
     */
    setOnOpen (callback) {
        this._onOpen = callback;
    }

    /**
     * @param {function} callback - the function to call when the socket is closed
     */
    setOnClose (callback) {
        this._onClose = callback;
    }

    /**
     * @param {function} callback - the function to call when an error occurs
     */
    setOnError (callback) {
        this._onError = callback;
    }

    /**
     * @param {function} callback - the function to call when a message is received from Scratch Link
     */
    setHandleMessage (callback) {
        this._handleMessage = callback;
    }

    _handleMessageWrapper (message) {
        switch (message.method) {
        case 'sessionDidClose':
            return this._handleSessionDidClose();
        default:
            return this._handleMessage(message);
        }
    }

    _handleSessionDidClose () {
        console.error('session closed unexpectedly');
        // call `close()`, not `_onClose()`, because we need to notify the background script
        this.close();
    }

    _sendNotify (method, params = {}) {
        this._toScratchLink(method, params, false);
    }

    _sendRequest (method, params = {}) {
        return this._toScratchLink(method, params, true);
    }

    /**
     * Send a notification or request to Scratch Link.
     * @param {string} method - the method to call
     * @param {object} params - optional parameters for the call
     * @param {boolean} expectResponse - true to send a request, false for a notification
     * @returns {Promise|undefined} - a Promise for the result of the request, if expectResponse is true
     */
    _toScratchLink (method, params, expectResponse) {
        const message = {
            jsonrpc: '2.0',
            session: this._id,
            method
        };
        if (params) message.params = params;
        let responsePromise;
        if (expectResponse) {
            responsePromise = new Promise((resolve, reject) => {
                message.id = registerResponseHandlers({resolve, reject});
            });
        }
        self.postMessage({'to-scratch-link': message}, self.origin);
        return responsePromise;
    }
}

export { ScratchLinkSafariSocket };
