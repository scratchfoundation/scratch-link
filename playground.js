
// Expected globals:
// - JSONRPC
// - Scratch
// - ScratchLinkWebSocket
// - ScratchLinkSafariSocket (if Safari extension is present)
/// <reference path="global.d.ts"/>

class LogDisplay {
    /**
     * @param {HTMLElement} logElement
     */
    constructor (logElement, lineCount = 256) {
        this._logElement = logElement;
        this._lineCount = lineCount;
        this._lines = [];
        this._dirty = false;
        this._follow = true;
    }

    /**
     * @param {string} text
     */
    addLine (text) {
        this._lines.push(text);
        if (!this._dirty) {
            this._dirty = true;
            requestAnimationFrame(() => {
                this._trim();
                this._logElement.textContent = this._lines.join('\n');
                if (this._follow) {
                    this._logElement.scrollTop = this._logElement.scrollHeight;
                }
                this._dirty = false;
            });
        }
    }

    _trim () {
        this._lines = this._lines.splice(-this._lineCount);
    }
}

const log = document.getElementById('log');
const follow = document.getElementById('follow');
const logDisplay = new LogDisplay(log);

/**
 * @param {string} text
 */
function addLine (text) {
    logDisplay.addLine(text);
    logDisplay._follow = follow.checked;
}

/**
 * @param {object} o
 */
function stringify (o) {
    if (o instanceof Event) {
        if (o instanceof ErrorEvent) {
            return `${o.constructor.name} {error: ${stringify(o.error)}`;
        }
        return `${o.constructor.name} {type: "${o.type}"}`;
    }
    return JSON.stringify(o);//, o && Object.getOwnPropertyNames(o));
}

class DidReceiveCallEvent extends Event {
    constructor(method, params) {
        super(method); // this is probably not a good idea in untrusted environments but it works for this playground
        this.method = method;
        this.params = params;
    }
}

class ScratchLinkClient extends JSONRPC {
    /**
     * @param {string} type
     */
    constructor(type) {
        super();
        this._scratchLinkPeripheralType = type;
        this._events = new EventTarget();
        const ScratchLinkSafariSocket = self.Scratch && self.Scratch.ScratchLinkSafariSocket;
        const useSafariSocket = ScratchLinkSafariSocket && ScratchLinkSafariSocket.isSafariHelperCompatible();
        addLine(`Using ${useSafariSocket ? 'Safari WebExtension' : 'WebSocket'}`);
        this._socket = useSafariSocket ?
            new ScratchLinkSafariSocket(type) :
            new ScratchLinkWebSocket(type);
        addLine(`Socket created for ${type}`);
        this._socket.setOnClose(e => {
            addLine(`Socket closed: ${stringify(e)}`);
        });
        this._socket.setOnError(e => {
            addLine(`Socket error: ${stringify(e)}`);
        });
        this._socket.setHandleMessage(message => {
            addLine(`Received message: ${stringify(message)}`);
            this._handleMessage(message);
        });
        this._sendMessage = (/** @type {object} */ message) => {
            addLine(`Sending message: ${stringify(message)}`);
            return this._socket.sendMessage(message);
        };
    }

    /**
     * @returns {Promise<ScratchLinkClient>}
     */
    open () {
        return new Promise(resolve => {
            this._socket.setOnOpen(() => {
                addLine(`Socket opened for ${this._scratchLinkPeripheralType}`);
                resolve(this);
            });
            this._socket.open();
        });
    }
}

class ScratchBLE extends ScratchLinkClient {
    constructor() {
        super('BLE');

        this.discoveredPeripheralId = null;
    }

    /**
     * @param {object} options
     */
    requestDevice(options) {
        return this.sendRemoteRequest('discover', options);
    }

    /**
     * @param {any} method
     * @param {{ [paramName: string]: any; }} params
     */
    didReceiveCall(method, params) {
        addLine(`Received call to method: ${method}`);
        this._events.dispatchEvent(new DidReceiveCallEvent(method, params));
        switch (method) {
        case 'didDiscoverPeripheral':
            addLine(`Peripheral discovered: ${stringify(params)}`);
            this.discoveredPeripheralId = params['peripheralId'];
            break;
        case 'ping':
            addLine(`Responding to ping`);
            return 42;
        }
    }

    /**
     * @param {number | string} serviceId
     * @param {number | string} characteristicId
     */
    read(serviceId, characteristicId, optStartNotifications = false) {
        const params = {
            serviceId,
            characteristicId
        };
        if (optStartNotifications) {
            params.startNotifications = true;
        }
        return this.sendRemoteRequest('read', params);
    }

    /**
     * @param {number | string} serviceId
     * @param {number | string} characteristicId
     * @param {string} message
     */
    write(serviceId, characteristicId, message, encoding = null, withResponse = null) {
        const params = { serviceId, characteristicId, message };
        if (encoding) {
            params.encoding = encoding;
        }
        if (withResponse !== null) {
            params.withResponse = withResponse;
        }
        return this.sendRemoteRequest('write', params);
    }
}

class ScratchBT extends ScratchLinkClient {
    constructor() {
        super('BT');
    }

    /**
     * @param {object} options
     */
    requestDevice(options) {
        return this.sendRemoteRequest('discover', options);
    }

    /**
     * @param {object} options
     */
    connectDevice(options) {
        return this.sendRemoteRequest('connect', options);
    }

    /**
     * @param {object} options
     */
    sendMessage(options) {
        return this.sendRemoteRequest('send', options);
    }

    /**
     * @param {string} method
     * @param {{ [paramName: string]: any; }} params
     */
    didReceiveCall(method, params) {
        addLine(`Received call to method: ${method}`);
        this._events.dispatchEvent(new DidReceiveCallEvent(method, params));
        switch (method) {
            case 'didDiscoverPeripheral':
                addLine(`Peripheral discovered: ${stringify(params)}`);
                document.getElementById('peripheralId').value = params['peripheralId'];
                break;
            case 'didReceiveMessage':
                addLine(`Message received from peripheral: ${stringify(params)}`);
                break;
            default:
                return 'nah';
        }
    }
}

/**
 * @param {string} buttonId
 * @param {function} func
 */
function attachFunctionToButton(buttonId, func) {
    const button = document.getElementById(buttonId);
    button.onclick = () => {
        try {
            func();
        } catch (e) {
            addLine(`Button ${buttonId} caught exception: ${stringify(e)})`);
        }
    }
}

/**
 * @param {ScratchLinkClient} session
 */
function getVersion(session) {
    return session.sendRemoteRequest('getVersion').then(
        x => {
            addLine(`Version request resolved with: ${stringify(x)}`);
        },
        e => {
            addLine(`Version request rejected with: ${stringify(e)}`);
        }
    );
}

/**
 * @param {ScratchLinkClient} session
 */
function pingMe (session) {
    return session.sendRemoteRequest('pingMe').then(
        x => {
            addLine(`Ping request resolved with: ${stringify(x)}`);
        },
        e => {
            addLine(`Ping request rejected with: ${stringify(e)}`);
        }
    );
}

function initBLE() {
    if (self.Scratch.BLE) {
        self.Scratch.BLE._socket.close();
    }
    addLine('Connecting...');
    self.Scratch.BLE = new ScratchBLE();
    return self.Scratch.BLE.open();
}

const filterInputsBLE = [];
function addFilterBLE() {
    const filter = {};
    filterInputsBLE.push(filter);

    const fieldset = document.createElement('fieldset');

    const legend = document.createElement('legend');
    legend.appendChild(document.createTextNode('Filter ' + filterInputsBLE.length));
    fieldset.appendChild(legend);

    const exactNameDiv = document.createElement('div');
    exactNameDiv.appendChild(document.createTextNode('Discover peripherals with exact name: '));
    const exactNameInput = document.createElement('input');
    exactNameInput.type = 'text';
    exactNameInput.placeholder = 'Name';
    exactNameDiv.appendChild(exactNameInput);
    fieldset.appendChild(exactNameDiv);

    const namePrefixDiv = document.createElement('div');
    namePrefixDiv.appendChild(document.createTextNode('Discover peripherals with name prefix: '));
    const namePrefixInput = document.createElement('input');
    namePrefixInput.type = 'text';
    namePrefixInput.placeholder = 'Name Prefix';
    namePrefixDiv.appendChild(namePrefixInput);
    fieldset.appendChild(namePrefixDiv);

    const servicesDiv = document.createElement('div');
    servicesDiv.appendChild(document.createTextNode('Discover peripherals with these services:'));
    servicesDiv.appendChild(document.createElement('br'));
    const servicesInput = document.createElement('textarea');
    servicesInput.placeholder = 'Required services, if any, separated by whitespace';
    servicesInput.style.width = '20rem';
    servicesInput.style.height = '3rem';
    servicesDiv.appendChild(servicesInput);
    fieldset.appendChild(servicesDiv);

    const manufacturerDataDiv = document.createElement('div');
    manufacturerDataDiv.appendChild(document.createTextNode('Discover peripherals with this manufacturer data:'));
    manufacturerDataDiv.appendChild(document.createElement('br'));
    const addManufacturerDataFilterButton = document.createElement('button');
    addManufacturerDataFilterButton.appendChild(document.createTextNode('Add data filter'));
    const manufacturerDataFilterInputs = [];
    addManufacturerDataFilterButton.onclick = () => {
        const manufacturerDataFilter = {};
        manufacturerDataFilterInputs.push(manufacturerDataFilter);
        const manufacturerDataFilterFields = document.createElement('fieldset');
        const manufacturerDataFilterLegend = document.createElement('legend');
        manufacturerDataFilterLegend.appendChild(document.createTextNode('Manufacturer Data Filter ' + manufacturerDataFilterInputs.length));
        manufacturerDataFilterFields.appendChild(manufacturerDataFilterLegend);

        const manufacturerIdDiv = document.createElement('div');
        manufacturerIdDiv.appendChild(document.createTextNode('Manufacturer ID: '));
        const manufacturerIdInput = document.createElement('input');
        manufacturerIdInput.type = 'number';
        manufacturerIdInput.placeholder = '65535';
        manufacturerIdDiv.appendChild(manufacturerIdInput);
        manufacturerDataFilterFields.appendChild(manufacturerIdDiv);

        const manufacturerDataPrefixDiv = document.createElement('div');
        manufacturerDataPrefixDiv.appendChild(document.createTextNode('Data Prefix: '));
        const manufacturerDataPrefixInput = document.createElement('input');
        manufacturerDataPrefixInput.type = 'text';
        manufacturerDataPrefixInput.placeholder = '1 2 3';
        manufacturerDataPrefixDiv.appendChild(manufacturerDataPrefixInput);
        manufacturerDataFilterFields.appendChild(manufacturerDataPrefixDiv);

        const manufacturerDataMaskDiv = document.createElement('div');
        manufacturerDataMaskDiv.appendChild(document.createTextNode('Mask: '));
        const manufacturerDataMaskInput = document.createElement('input');
        manufacturerDataMaskInput.type = 'text';
        manufacturerDataMaskInput.placeholder = '255 15 255';
        manufacturerDataMaskDiv.appendChild(manufacturerDataMaskInput);
        manufacturerDataFilterFields.appendChild(manufacturerDataMaskDiv);

        manufacturerDataFilter.idInput = manufacturerIdInput;
        manufacturerDataFilter.prefixInput = manufacturerDataPrefixInput;
        manufacturerDataFilter.maskInput = manufacturerDataMaskInput;

        manufacturerDataDiv.appendChild(manufacturerDataFilterFields);
    };
    manufacturerDataDiv.appendChild(addManufacturerDataFilterButton);
    fieldset.appendChild(manufacturerDataDiv);

    filter.exactNameInput = exactNameInput;
    filter.namePrefixInput = namePrefixInput;
    filter.servicesInput = servicesInput;
    filter.manufacturerDataFilterInputs = manufacturerDataFilterInputs;

    const filtersParent = document.getElementById('filtersBLE');
    filtersParent.appendChild(fieldset);
}

function discoverBLE() {
    const filters = [];
    for (const filterInputs of filterInputsBLE) {
        const filter = {};
        if (filterInputs.exactNameInput.value) filter.name = filterInputs.exactNameInput.value;
        if (filterInputs.namePrefixInput.value) filter.namePrefix = filterInputs.namePrefixInput.value;
        if (filterInputs.servicesInput.value.trim()) filter.services = filterInputs.servicesInput.value.trim().split(/\s+/);
        if (filter.services) filter.services = filter.services.map((/** @type {string} */ s) => parseInt(s));

        let hasManufacturerDataFilters = false;
        const manufacturerDataFilters = {};
        for (let manufacturerDataFilterInputs of filterInputs.manufacturerDataFilterInputs) {
            if (!manufacturerDataFilterInputs.idInput.value) continue;
            const id = manufacturerDataFilterInputs.idInput.value.trim();
            const manufacturerDataFilter = {};
            manufacturerDataFilters[id] = manufacturerDataFilter;
            hasManufacturerDataFilters = true;
            if (manufacturerDataFilterInputs.prefixInput.value) manufacturerDataFilter.dataPrefix = manufacturerDataFilterInputs.prefixInput.value.trim().split(/\s+/).map((/** @type {string} */ p) => parseInt(p));
            if (manufacturerDataFilterInputs.maskInput.value) manufacturerDataFilter.mask = manufacturerDataFilterInputs.maskInput.value.trim().split(/\s+/).map((/** @type {string} */ m) => parseInt(m));
        }
        if (hasManufacturerDataFilters) {
            filter.manufacturerData = manufacturerDataFilters;
        }
        filters.push(filter);
    }

    const deviceDetails = {
        filters: filters
    };

    const optionalServicesBLE = document.getElementById('optionalServicesBLE');
    if (optionalServicesBLE.value.trim()) {
        deviceDetails.optionalServices = optionalServicesBLE.value.trim().split(/\s+/);
    }

    return Scratch.BLE.requestDevice(
        deviceDetails
    ).then(
        x => {
            addLine(`requestDevice resolved to: ${stringify(x)}`);
        },
        e => {
            addLine(`requestDevice rejected with: ${stringify(e)}`);
        }
    );
}

function connectBLE() {
    // this should really be implicit in `requestDevice` but splitting it out helps with debugging
    return Scratch.BLE.sendRemoteRequest(
        'connect',
        { peripheralId: Scratch.BLE.discoveredPeripheralId }
    ).then(
        x => {
            addLine(`connect resolved to: ${stringify(x)}`);
        },
        e => {
            addLine(`connect rejected with: ${stringify(e)}`);
        }
    );
}

function getServicesBLE() {
    return Scratch.BLE.sendRemoteRequest(
        'getServices'
    ).then(
        x => {
            addLine(`getServices resolved to: ${stringify(x)}`);
        },
        e => {
            addLine(`getServices rejected with: ${stringify(e)}`);
        }
    );
}

function setServiceMicroBit() {
    if (filterInputsBLE.length < 1) addFilterBLE();
    filterInputsBLE[0].namePrefixInput.value = null;
    filterInputsBLE[0].exactNameInput.value = null;
    filterInputsBLE[0].servicesInput.value = '0xf005';
}

function readMicroBit() {
    return Scratch.BLE.read(0xf005, '5261da01-fa7e-42ab-850b-7c80220097cc', true).then(
        x => {
            addLine(`read resolved to: ${stringify(x)}`);
        },
        e => {
            addLine(`read rejected with: ${stringify(e)}`);
        }
    );
}

function writeMicroBit() {
    const message = _encodeMessage('LINK');
    return Scratch.BLE.write(0xf005, '5261da02-fa7e-42ab-850b-7c80220097cc', message, 'base64').then(
        x => {
            addLine(`write resolved to: ${stringify(x)}`);
        },
        e => {
            addLine(`write rejected with: ${stringify(e)}`);
        }
    );
}

function setServiceWeDo2() {
    if (filterInputsBLE.length < 1) addFilterBLE();
    filterInputsBLE[0].namePrefixInput.value = null;
    filterInputsBLE[0].exactNameInput.value = null;
    filterInputsBLE[0].servicesInput.value = '00001523-1212-efde-1523-785feabcd123';
}

function setGDXFOR() {
    if (filterInputsBLE.length < 1) addFilterBLE();
    const optionalServicesBLE = document.getElementById('optionalServicesBLE');
    optionalServicesBLE.value = 'd91714ef-28b9-4f91-ba16-f0d9a604f112';
    filterInputsBLE[0].namePrefixInput.value = 'GDX';
    filterInputsBLE[0].exactNameInput.value = null;
    filterInputsBLE[0].servicesInput.value = null;
}

// micro:bit base64 encoding
// https://github.com/LLK/scratch-microbit-firmware/blob/master/protocol.md
/**
 * @param {string} message
 */
function _encodeMessage(message) {
    const output = new Uint8Array(message.length);
    for (let i = 0; i < message.length; i++) {
        output[i] = message.charCodeAt(i);
    }
    const output2 = new Uint8Array(output.length + 1);
    output2[0] = 0x81; // CMD_DISPLAY_TEXT
    for (let i = 0; i < output.length; i++) {
        output2[i + 1] = output[i];
    }
    return window.btoa(String.fromCharCode.apply(null, output2));
}

attachFunctionToButton('initBLE', initBLE);
attachFunctionToButton('getVersionBLE', () => getVersion(self.Scratch.BLE));
attachFunctionToButton('pingBLE', () => pingMe(self.Scratch.BLE));
attachFunctionToButton('discoverBLE', discoverBLE);
attachFunctionToButton('connectBLE', connectBLE);
attachFunctionToButton('getServicesBLE', getServicesBLE);

attachFunctionToButton('setServiceMicroBit', setServiceMicroBit);
attachFunctionToButton('readMicroBit', readMicroBit);
attachFunctionToButton('writeMicroBit', writeMicroBit);

attachFunctionToButton('setServiceWeDo2', setServiceWeDo2);

attachFunctionToButton('setGDXFOR', setGDXFOR);

attachFunctionToButton('addFilterBLE', addFilterBLE);

addFilterBLE();

function initBT() {
    if (self.Scratch.BT) {
        self.Scratch.BT._socket.close();
    }
    addLine('Connecting...');
    self.Scratch.BT = new ScratchBT();
    return self.Scratch.BT.open();
}

function discoverBT() {
    return Scratch.BT.requestDevice({
        majorDeviceClass: 8,
        minorDeviceClass: 1
    }).then(
        x => {
            addLine(`requestDevice resolved to: ${stringify(x)}`);
        },
        e => {
            addLine(`requestDevice rejected with: ${stringify(e)}`);
        }
    );
}

function connectBT() {
    return Scratch.BT.connectDevice({
        peripheralId: document.getElementById('peripheralId').value,
        pin: "1234"
    }).then(
        x => {
            addLine(`connectDevice resolved to: ${stringify(x)}`);
        },
        e => {
            addLine(`connectDevice rejected with: ${stringify(e)}`);
        }
    );
}

/**
 * @param {any} message
 */
function sendMessage(message) {
    return Scratch.BT.sendMessage({
        message: document.getElementById('messageBody').value,
        encoding: 'base64'
    }).then(
        x => {
            addLine(`sendMessage resolved to: ${stringify(x)}`);
        },
        e => {
            addLine(`sendMessage rejected with: ${stringify(e)}`);
        }
    );
}

function beep() {
    return Scratch.BT.sendMessage({
        message: 'DwAAAIAAAJQBgQKC6AOC6AM=',
        encoding: 'base64'
    }).then(
        x => {
            addLine(`sendMessage resolved to: ${stringify(x)}`);
        },
        e => {
            addLine(`sendMessage rejected with: ${stringify(e)}`);
        }
    );
}

const closeButton = document.getElementById('closeBT');
closeButton.onclick = () => {
    self.Scratch.BT.dispose();
};

attachFunctionToButton('initBT', initBT);
attachFunctionToButton('getVersionBT', () => getVersion(self.Scratch.BT));
attachFunctionToButton('discoverBT', discoverBT);
attachFunctionToButton('connectBT', connectBT);
attachFunctionToButton('send', sendMessage);
attachFunctionToButton('beep', beep);

/**
 * @param {string} type
 */
function makeStressTest(type) {
    const goButton = document.getElementById(`stress${type}`);
    if (!goButton) {
        addLine(`Could not activate stress test button for ${type}`);
        return;
    }
    goButton.onclick = () => runStressTest(type);
}

makeStressTest('BLE');
makeStressTest('BT');

/**
 * @param {number} seconds
 * @param {any} [returnVal]
 */
function sleep(seconds, returnVal) {
    return (new Promise(resolve => {
        setTimeout(() => { resolve(returnVal); }, 1000*seconds);
    }));
}

/**
 * @param {string} type
 */
async function runStressTest(type) {
    const doPingElement = document.getElementById(`stressPing${type}`);
    const doGetVersionElement = document.getElementById(`stressGetVersion${type}`);
    const doDiscoverElement = document.getElementById(`stressDiscover${type}`);
    const doConnectElement = document.getElementById(`stressConnect${type}`);

    const {init, connect, discover} = (() => {
        switch (type) {
            case 'BLE':
                return {
                    init: initBLE,
                    discover: discoverBLE,
                    connect: connectBLE,
                };
            case 'BT':
                return {
                    init: initBT,
                    discover: discoverBT,
                    connect: connectBT,
                };
            default:
                const message = `Don't know how to run a stress test for ${type}`;
                addLine(message);
                throw new Error(message);
        }
    })();

    while (true) {
        const session = await init(); // also disconnects previous session
        if (doPingElement.checked) await pingMe(session);
        if (doGetVersionElement.checked) await getVersion(session);
        if (doDiscoverElement.checked) {
            const discoverPromise = discover();
            const didDiscoverPromise = new Promise(resolve => {
                /**
                 * @param {Event} e
                 */
                function listener(e) {
                    resolve(e.params);
                    session._events.removeEventListener('didDiscoverPeripheral', listener);
                }
                session._events.addEventListener('didDiscoverPeripheral', listener);
            });
            await discoverPromise;
            await didDiscoverPromise;
        }
        if (doConnectElement.checked) await connect();
    }
}
