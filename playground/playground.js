self.Scratch = self.Scratch || {};

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
 * Create a <div> element containing an arbitrary child element and a label for it.
 * @param {string} label - label to place before the child element
 * @param {Element} childElement - the element to place after the label (usually an input element)
 * @param {Boolean} [doLineBreak=false] - if true, place a line break between the label and the input
 * @returns {Element} - the new div element
 */
function makeFilterEntry(label, childElement, doLineBreak = false) {
    const div = document.createElement('div');
    div.appendChild(document.createTextNode(label));
    if (doLineBreak) {
        div.appendChild(document.createElement('br'));
    }
    div.appendChild(childElement);
    return div;
}

/**
 * Create a <div> element containing an input field and a label for it.
 * @param {string} label - label to place before the input
 * @param {object} options
 * @property {string} [type='text'] - the type of input
 * @property {string} [placeholder] - placeholder text to show on input when empty
 * @property {string} [style] - CSS styling to apply to the input element (for example, size specifications)
 * @property {Boolean} [doLineBreak=false] - if true, add a line break between the label and the input
 * @returns {object}
 * @property {Element} div - the new div element
 * @property {Element} input - the new input element (a child of the div)
 */
function makeFilterInput(label, {type, placeholder, style, doLineBreak} = {}) {
    const input = document.createElement('input');
    input.type = type;
    input.placeholder = placeholder;
    input.style = style;
    const div = makeFilterEntry(label, input, doLineBreak);
    return {div, input};
}

function getVersion(session) {
    session.sendRemoteRequest('getVersion').then(
        x => {
            addLine(`Version request resolved with: ${stringify(x)}`);
        },
        e => {
            addLine(`Version request rejected with: ${stringify(e)}`);
        }
    );
}

function ping(session) {
    session.sendRemoteRequest('pingMe').then(
        x => {
            addLine(`Ping request resolved with: ${stringify(x)}`);
        },
        e => {
            addLine(`Ping request rejected with: ${stringify(e)}`);
        }
    );
}

function initBLE() {
    self.Scratch.BLE = new ScratchBLE();
}

const filterInputsBLE = [];
function addFilterBLE() {
    const filter = {};
    filterInputsBLE.push(filter);

    const fieldset = document.createElement('fieldset');

    const legend = document.createElement('legend');
    legend.appendChild(document.createTextNode('Filter ' + filterInputsBLE.length));
    fieldset.appendChild(legend);

    const name = makeFilterInput('Discover peripherals with exact name: ', {placeholder: 'Name'});
    const namePrefix = makeFilterInput('Discover peripherals with name prefix: ', {placeholder: 'Name Prefix'});
    const requiredServices = makeFilterInput('Discover peripherals with these services:', {
        type: 'textarea',
        doLineBreak: true,
        placeholder: 'Required services, if any, separated by whitespace',
        style: 'width:20rem;height:3rem;',
    });

    const addManufacturerDataFilterButton = document.createElement('button');
    addManufacturerDataFilterButton.appendChild(document.createTextNode('Add data filter'));
    const manufacturerDataDiv = makeFilterEntry(
        'Discover peripherals with this manufacturer data:', addManufacturerDataFilterButton, true);

    fieldset.appendChild(name.div);
    fieldset.appendChild(namePrefix.div);
    fieldset.appendChild(requiredServices.div);
    fieldset.appendChild(manufacturerDataDiv);

    filter.exactNameInput = name.input;
    filter.namePrefixInput = namePrefix.input;
    filter.servicesInput = requiredServices.input;

    const filtersParent = document.getElementById('filtersBLE');
    filtersParent.appendChild(fieldset);

    const manufacturerDataFilterInputs = [];
    filter.manufacturerDataFilterInputs = manufacturerDataFilterInputs;
    addManufacturerDataFilterButton.onclick = () => {
        const manufacturerDataFilter = {};
        manufacturerDataFilterInputs.push(manufacturerDataFilter);
        const manufacturerDataFilterFields = document.createElement('fieldset');
        const manufacturerDataFilterLegend = document.createElement('legend');
        manufacturerDataFilterLegend.appendChild(document.createTextNode('Manufacturer Data Filter ' + manufacturerDataFilterInputs.length));
        manufacturerDataFilterFields.appendChild(manufacturerDataFilterLegend);

        const manufacturerId = makeFilterInput('Manufacturer ID: ', {type: 'number', placeholder: '65535'});
        const dataPrefix = makeFilterInput('Data Prefix: ', {placeholder: '1 2 3'});
        const dataMask = makeFilterInput('Data Mask: ', {placeholder: '255 15 255'});

        manufacturerDataFilterFields.appendChild(manufacturerId.div);
        manufacturerDataFilterFields.appendChild(dataPrefix.div);
        manufacturerDataFilterFields.appendChild(dataMask.div);

        manufacturerDataFilter.idInput = manufacturerId.input;
        manufacturerDataFilter.prefixInput = dataPrefix.input;
        manufacturerDataFilter.maskInput = dataMask.input;

        manufacturerDataDiv.appendChild(manufacturerDataFilterFields);
    };
}

function discoverBLE() {
    const filters = [];
    for (const filterInputs of filterInputsBLE) {
        const filter = {};
        if (filterInputs.exactNameInput.value) filter.name = filterInputs.exactNameInput.value;
        if (filterInputs.namePrefixInput.value) filter.namePrefix = filterInputs.namePrefixInput.value;
        if (filterInputs.servicesInput.value.trim()) filter.services = filterInputs.servicesInput.value.trim().split(/\s+/);
        if (filter.services) filter.services = filter.services.map(s => parseInt(s));

        let hasManufacturerDataFilters = false;
        const manufacturerDataFilters = {};
        for (manufacturerDataFilterInputs of filterInputs.manufacturerDataFilterInputs) {
            if (!manufacturerDataFilterInputs.idInput.value) continue;
            const id = manufacturerDataFilterInputs.idInput.value.trim();
            const manufacturerDataFilter = {};
            manufacturerDataFilters[id] = manufacturerDataFilter;
            hasManufacturerDataFilters = true;
            if (manufacturerDataFilterInputs.prefixInput.value) {
                manufacturerDataFilter.dataPrefix = manufacturerDataFilterInputs.prefixInput.value.trim().split(/\s+/).map(p => parseInt(p));
            }
            if (manufacturerDataFilterInputs.maskInput.value) {
                manufacturerDataFilter.mask = manufacturerDataFilterInputs.maskInput.value.trim().split(/\s+/).map(m => parseInt(m));
            }
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
    if (optionalServicesBLE.value.trim()) deviceDetails.optionalServices = optionalServicesBLE.value.trim().split(/\s+/);

    Scratch.BLE.requestDevice(
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
    Scratch.BLE.sendRemoteRequest(
        'connect',
        {peripheralId: Scratch.BLE.discoveredPeripheralId}
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
    Scratch.BLE.sendRemoteRequest(
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
    if (filtersBLE.length < 1) addFilterBLE();
    const optionalServicesBLE = document.getElementById('optionalServicesBLE');
    optionalServicesBLE.value = null;
    filterInputsBLE[0].namePrefixInput.value = null;
    filterInputsBLE[0].exactNameInput.value = null;
    filterInputsBLE[0].servicesInput.value = '0xf005';
}

function readMicroBit() {
    Scratch.BLE.read(0xf005, '5261da01-fa7e-42ab-850b-7c80220097cc', true).then(
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
    Scratch.BLE.write(0xf005, '5261da02-fa7e-42ab-850b-7c80220097cc', message, 'base64').then(
        x => {
            addLine(`write resolved to: ${stringify(x)}`);
        },
        e => {
            addLine(`write rejected with: ${stringify(e)}`);
        }
    );
}

function setServiceWeDo2() {
    if (filtersBLE.length < 1) addFilterBLE();
    const optionalServicesBLE = document.getElementById('optionalServicesBLE');
    optionalServicesBLE.value = null;
    filterInputsBLE[0].namePrefixInput.value = null;
    filterInputsBLE[0].exactNameInput.value = null;
    filterInputsBLE[0].servicesInput.value = '00001523-1212-efde-1523-785feabcd123';
}

function setGDXFOR() {
    if (filtersBLE.length < 1) addFilterBLE();
    const optionalServicesBLE = document.getElementById('optionalServicesBLE');
    optionalServicesBLE.value = 'd91714ef-28b9-4f91-ba16-f0d9a604f112';
    filterInputsBLE[0].namePrefixInput.value = 'GDX';
    filterInputsBLE[0].exactNameInput.value = null;
    filterInputsBLE[0].servicesInput.value = null;
}

// micro:bit base64 encoding
// https://github.com/LLK/scratch-microbit-firmware/blob/master/protocol.md
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
    return base64 = window.btoa(String.fromCharCode.apply(null, output2));
}

attachFunctionToButton('initBLE', initBLE);
attachFunctionToButton('getVersionBLE', () => getVersion(self.Scratch.BLE));
attachFunctionToButton('pingBLE', () => ping(self.Scratch.BLE));
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
    self.Scratch.BT = new ScratchBT();
}

function discoverBT() {
    Scratch.BT.requestDevice({
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
    Scratch.BT.connectDevice({
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

function sendMessage(message) {
    Scratch.BT.sendMessage({
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
    Scratch.BT.sendMessage({
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

const follow = document.getElementById('follow');
const log = document.getElementById('log');

const closeButton = document.getElementById('closeBT');
closeButton.onclick = () => {
    self.Scratch.BT.dispose();
};

attachFunctionToButton('initBT', initBT);
attachFunctionToButton('getVersionBT', () => getVersion(self.Scratch.BT));
attachFunctionToButton('pingBT', () => ping(self.Scratch.BT));
attachFunctionToButton('discoverBT', discoverBT);
attachFunctionToButton('connectBT', connectBT);
attachFunctionToButton('send', sendMessage);
attachFunctionToButton('beep', beep);

class LogDisplay {
    constructor(logElement, lineCount = 256) {
        this._logElement = logElement;
        this._lineCount = lineCount;
        this._lines = [];
        this._dirty = false;
        this._follow = true;
    }

    addLine(text) {
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

    _trim() {
        this._lines = this._lines.splice(-this._lineCount);
    }
}

const logDisplay = new LogDisplay(log);
function addLine(text) {
    logDisplay.addLine(text);
    logDisplay._follow = follow.checked;
}
