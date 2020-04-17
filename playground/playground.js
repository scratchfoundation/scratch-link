self.Scratch = self.Scratch || {};

/*
 ******** common helpers
 */

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

const follow = document.getElementById('follow');
const log = document.getElementById('log');
const logDisplay = new LogDisplay(log);
function addLine(text) {
    logDisplay.addLine(text);
    logDisplay._follow = follow.checked;
}

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

/*
 ******** Bluetooth Low Energy (BLE)
 */

function bleInit() {
    const BLE = new ScratchBLE();
    BLE.addEventListener(BLE.EVENT_didDiscoverPeripheral, event => {
        console.dir(event);
        if (event.detail && event.detail.peripheralId) {
            document.getElementById('blePeripheralId').value = event.detail.peripheralId;
        }
    });
    self.Scratch.BLE = BLE;
}

const bleFilterInputs = [];
function bleAddFilter() {
    const filter = {};
    bleFilterInputs.push(filter);

    const fieldset = document.createElement('fieldset');

    const legend = document.createElement('legend');
    legend.appendChild(document.createTextNode('Filter ' + bleFilterInputs.length));
    fieldset.appendChild(legend);

    const name = makeFilterInput('Discover peripherals with exact name: ', {placeholder: 'Name'});
    const namePrefix = makeFilterInput('Discover peripherals with name prefix: ', {placeholder: 'Name Prefix'});

    const requiredServicesInput = document.createElement('textarea');
    requiredServicesInput.placeholder = 'Required services, if any, separated by whitespace';
    requiredServicesInput.style = 'width:20rem;height:3rem;';
    const requiredServicesDiv = makeFilterEntry(
        'Discover peripherals with these services:', requiredServicesInput, true);

    const addManufacturerDataFilterButton = document.createElement('button');
    addManufacturerDataFilterButton.appendChild(document.createTextNode('Add data filter'));
    const manufacturerDataDiv = makeFilterEntry(
        'Discover peripherals with this manufacturer data:', addManufacturerDataFilterButton, true);

    fieldset.appendChild(name.div);
    fieldset.appendChild(namePrefix.div);
    fieldset.appendChild(requiredServicesDiv);
    fieldset.appendChild(manufacturerDataDiv);

    filter.nameInput = name.input;
    filter.namePrefixInput = namePrefix.input;
    filter.servicesInput = requiredServicesInput;

    const filtersParent = document.getElementById('bleFilterInputs');
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

function bleDiscover() {
    const filters = [];
    for (const filterInputs of bleFilterInputs) {
        const filter = {};
        if (filterInputs.nameInput.value) filter.name = filterInputs.nameInput.value;
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

    const bleOptionalServices = document.getElementById('bleOptionalServices');
    if (bleOptionalServices.value.trim()) deviceDetails.optionalServices = bleOptionalServices.value.trim().split(/\s+/);

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

function bleConnect() {
    // this should really be implicit in `requestDevice` but splitting it out helps with debugging
    Scratch.BLE.sendRemoteRequest(
        'connect',
        {peripheralId: document.getElementById('blePeripheralId').value}
    ).then(
        x => {
            addLine(`connect resolved to: ${stringify(x)}`);
        },
        e => {
            addLine(`connect rejected with: ${stringify(e)}`);
        }
    );
}

function bleGetServices() {
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

function bleSetServiceMicroBit() {
    if (bleFilterInputs.length < 1) bleAddFilter();
    const bleOptionalServices = document.getElementById('bleOptionalServices');
    bleOptionalServices.value = null;
    bleFilterInputs[0].namePrefixInput.value = null;
    bleFilterInputs[0].nameInput.value = null;
    bleFilterInputs[0].servicesInput.value = '0xf005';
}

function bleReadMicroBit() {
    Scratch.BLE.read(0xf005, '5261da01-fa7e-42ab-850b-7c80220097cc', true).then(
        x => {
            addLine(`read resolved to: ${stringify(x)}`);
        },
        e => {
            addLine(`read rejected with: ${stringify(e)}`);
        }
    );
}

function bleWriteMicroBit() {
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

function bleSetServiceWeDo2() {
    if (bleFilterInputs.length < 1) bleAddFilter();
    const bleOptionalServices = document.getElementById('bleOptionalServices');
    bleOptionalServices.value = null;
    bleFilterInputs[0].namePrefixInput.value = null;
    bleFilterInputs[0].nameInput.value = null;
    bleFilterInputs[0].servicesInput.value = '00001523-1212-efde-1523-785feabcd123';
}

function bleSetGDXFOR() {
    if (bleFilterInputs.length < 1) bleAddFilter();
    const bleOptionalServices = document.getElementById('bleOptionalServices');
    bleOptionalServices.value = 'd91714ef-28b9-4f91-ba16-f0d9a604f112';
    bleFilterInputs[0].namePrefixInput.value = 'GDX';
    bleFilterInputs[0].nameInput.value = null;
    bleFilterInputs[0].servicesInput.value = null;
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

attachFunctionToButton('bleInit', bleInit);
attachFunctionToButton('bleClose', () => self.Scratch.BLE.dispose());
attachFunctionToButton('bleGetVersion', () => getVersion(self.Scratch.BLE));
attachFunctionToButton('blePing', () => ping(self.Scratch.BLE));
attachFunctionToButton('bleDiscover', bleDiscover);
attachFunctionToButton('bleConnect', bleConnect);
attachFunctionToButton('bleGetServices', bleGetServices);
attachFunctionToButton('bleAddFilter', bleAddFilter);

attachFunctionToButton('bleSetServiceMicroBit', bleSetServiceMicroBit);
attachFunctionToButton('bleReadMicroBit', bleReadMicroBit);
attachFunctionToButton('bleWriteMicroBit', bleWriteMicroBit);

attachFunctionToButton('bleSetServiceWeDo2', bleSetServiceWeDo2);

attachFunctionToButton('bleSetGDXFOR', bleSetGDXFOR);

/*
 ******** Bluetooth Classic (BT / RFCOMM)
 */

function btInit() {
    const BT = new ScratchBT();
    BT.addEventListener(BT.EVENT_didDiscoverPeripheral, event => {
        if (event.detail && event.detail.peripheralId) {
            document.getElementById('btPeripheralId').value = event.detail.peripheralId;
        }
    });
    self.Scratch.BT = BT;
}

function btDiscover() {
    const discoveryParameters = {};
    const btFilter = btFilterInputs[0]; // we currently only support one filter

    if (btFilter.majorDeviceClassInput.value !== null) {
        discoveryParameters.majorDeviceClass = btFilter.majorDeviceClassInput.value;
    }
    if (btFilter.minorDeviceClassInput.value !== null) {
        discoveryParameters.minorDeviceClass = btFilter.minorDeviceClassInput.value;
    }
    if (btFilter.nameInput.value) {
        discoveryParameters.name = btFilter.nameInput.value;
    }
    if (btFilter.namePrefixInput.value) {
        discoveryParameters.namePrefix = btFilter.namePrefixInput.value;
    }

    Scratch.BT.requestDevice(discoveryParameters).then(
        x => {
            addLine(`requestDevice resolved to: ${stringify(x)}`);
        },
        e => {
            addLine(`requestDevice rejected with: ${stringify(e)}`);
        }
    );
}

function btConnect() {
    Scratch.BT.connectDevice({
        peripheralId: document.getElementById('btPeripheralId').value,
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

function btSendMessage(message = null) {
    if (message === null) {
        message = document.getElementById('btMessageBody').value;
    }
    Scratch.BT.sendMessage({
        message,
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

const btFilterInputs = [];
function btAddFilter() {
    const filter = {};
    btFilterInputs.push(filter);

    const fieldset = document.createElement('fieldset');

    const legend = document.createElement('legend');
    legend.appendChild(document.createTextNode('Filter ' + btFilterInputs.length));
    fieldset.appendChild(legend);

    const majorDeviceClass = makeFilterInput('Major device class: ', {placeholder: '"Toy" is 8'});
    const minorDeviceClass = makeFilterInput('Minor device class: ', {placeholder: 'If "Toy", "Robot" is 1'});
    const name = makeFilterInput('Discover peripherals with exact name: ', {placeholder: 'Name'});
    const namePrefix = makeFilterInput('Discover peripherals with name prefix: ', {placeholder: 'Name Prefix'});

    fieldset.appendChild(majorDeviceClass.div);
    fieldset.appendChild(minorDeviceClass.div);
    fieldset.appendChild(name.div);
    fieldset.appendChild(namePrefix.div);

    filter.majorDeviceClassInput = majorDeviceClass.input;
    filter.minorDeviceClassInput = minorDeviceClass.input;
    filter.nameInput = name.input;
    filter.namePrefixInput = namePrefix.input;

    const filtersParent = document.getElementById('btFilterInputs');
    filtersParent.appendChild(fieldset);
}

// BT currently supports only one filter and might not ever support multiple
// Just go ahead and add one so we don't need to have an "Add Filter" button
btAddFilter();

function btSetForSpikePrime() {
    if (btFilterInputs.length < 1) btAddFilter();
    document.getElementById('btPeripheralPIN').value = null;
    btFilterInputs[0].majorDeviceClassInput.value = '8';
    btFilterInputs[0].minorDeviceClassInput.value = '1';
    btFilterInputs[0].nameInput.value = null;
    btFilterInputs[0].namePrefixInput.value = 'LEGO Hub@';
}

function btSetForEV3() {
    if (btFilterInputs.length < 1) btAddFilter();
    document.getElementById('btPeripheralPIN').value = '1234';
    btFilterInputs[0].majorDeviceClassInput.value = '8';
    btFilterInputs[0].minorDeviceClassInput.value = '1';
    btFilterInputs[0].nameInput.value = null;
    btFilterInputs[0].namePrefixInput.value = null;
}

function btBeepEV3() {
    document.getElementById('btMessageBody').value = 'DwAAAIAAAJQBgQKC6AOC6AM=';
}

attachFunctionToButton('btInit', btInit);
attachFunctionToButton('btClose', () => self.Scratch.BT.dispose());
attachFunctionToButton('btGetVersion', () => getVersion(self.Scratch.BT));
attachFunctionToButton('btPing', () => ping(self.Scratch.BT));
attachFunctionToButton('btDiscover', btDiscover);
attachFunctionToButton('btConnect', btConnect);
attachFunctionToButton('btSendMessage', btSendMessage);

attachFunctionToButton('btSetForSpikePrime', btSetForSpikePrime);

attachFunctionToButton('btSetForEV3', btSetForEV3);
attachFunctionToButton('btBeepEV3', btBeepEV3);
