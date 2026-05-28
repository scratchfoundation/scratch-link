
// This file is only used for type-checking in playground.js

declare class AluxLabsLinkSafariSocket {
    constructor(type: string);
    static isSafariHelperCompatible(): boolean;

    close(): void;
    isOpen(): boolean;
    open(): void;
    sendMessage(message: object): void;
    setHandleMessage(fn: (message: object) => void): void;
    setOnClose(fn: (e: Error) => void): void;
    setOnError(fn: (e: Error) => void): void;
    setOnOpen(fn: () => void): void;
}

declare global {
    class JSONRPC {
        constructor();
        didReceiveCall(method: string, params?: object): void;
        sendRemoteNotification(method: string, params?: object): void;
        sendRemoteRequest(method: string, params?: object): Promise<any>;
        _handleMessage(jsonMessageObject: object): void;
        _sendMessage(jsonMessageObject: object): void;
    }

    class AluxLabsLinkWebSocket {
        constructor(type: string);

        close(): void;
        isOpen(): boolean;
        open(): void;
        sendMessage(message: object): void;
        setHandleMessage(fn: (message: object) => void): void;
        setOnClose(fn: (e: Error) => void): void;
        setOnError(fn: (e: Error) => void): void;
        setOnOpen(fn: () => void): void;
    }

    // This isn't proper TS but it seems to work for type checking in playground.js
    var AluxLabs = {
        BLE: new AluxLabsBLE,
        BT: new AluxLabsBT,
        AluxLabsLinkSafariSocket
    };
}
export {};
