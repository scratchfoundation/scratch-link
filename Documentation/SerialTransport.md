# Serial Transport 설계 문서

본 문서는 scratch-link 의 fork (aluxcoding 배포본) 에 **USB Serial 트랜스포트**를 추가하기 위한 설계를 기술한다. 기존 BLE / BT(Classic) 트랜스포트는 변경하지 않고, 동일한 패턴으로 새 세션 타입을 추가하는 형태이다.

1차 타겟 칩셋은 **CH340 (VID `0x1A86`, PID `0x7523`)**, 기본 보드레이트 1,000,000 bps. OS 는 Windows 만 고려한다. 후속으로 CP210x / FTDI / CH341 등을 동일 트랜스포트 위에 얹는 것을 염두에 둔다.

연결 상대 Scratch 는 `../aluxcoding-scratch` 모노레포의 `scratch-link-client` 라이브러리를 사용하며, 본 문서가 단일 출처(SoT)가 된다. 클라이언트 측은 본 문서의 포트/경로/메서드 명세에 맞춰 정렬되어야 한다.

## 결정 사항 요약

| 항목 | 결정 |
| --- | --- |
| WebSocket URL | `ws://localhost:20111/scratch/serial` (기존 포트/평문 유지) |
| TLS | 사용하지 않음 (loopback) |
| 프로토콜 | JSON-RPC 2.0. **수신 / 분리 알림은 Serial 전용 명명** (`serialDid...`) 으로 BT/BLE 와 코드상 구분. 요청 메서드(`discover`/`connect`/`write`)는 일반 동사라 공통 사용 |
| Serial API | `System.IO.Ports.SerialPort` (저사양 안정성 우선) |
| VID/PID 열거 | WMI (`Win32_PnPEntity`) |
| 1차 칩셋 | CH340 `VID_1A86 & PID_7523`, baud 1,000,000 |
| OS | Windows (`scratch-link-win`) 만 |

### `System.IO.Ports.SerialPort` 채택 근거

`Windows.Devices.SerialCommunication.SerialDevice` (WinRT) 는 비동기 I/O 효율과 capability 모델 정합성에서 우월하지만, 일부 구형 / 저사양 PC 에서 `DeviceWatcher` 이벤트 누락과 WinRT 초기화 오버헤드가 보고된다. 본 fork 의 배포 대상이 저사양 교육용 PC 를 포함하므로, 동작이 단순하고 디버깅이 용이한 `System.IO.Ports.SerialPort` 를 1차 채택한다. `SerialPort` 의 알려진 단점인 USB surprise removal 시 hang 은 [§ 5.3](#53-분리surprise-removal-감지)의 이중 방어로 보완한다. 미래에 WinRT API 로 전환할 수 있도록 추상화 인터페이스([§ 3.1](#31-serialsessiontport-common-추상))는 API 중립으로 설계한다.

## 1. 클라이언트 정합성 가정

`scratch-link-client` 라이브러리가 본 문서의 명세에 맞춰지도록, 다음 항목이 클라이언트 측에서 정렬되어야 한다 (참고용 — 클라이언트 작업 항목).

- WebSocket 엔드포인트: `ws://localhost:20111/scratch/serial`
- 요청 메서드: `getVersion`, `discover`, `connect`, `write`, `disconnect`, `startReading`, `stopReading`
- 서버→클라 알림: `didDiscoverPeripheral`, `serialDidReceiveData`, `serialDidDisconnect`
- 페이로드 바이너리 인코딩: base64

## 2. 변경 지점 개요

| 위치 | 변경 |
| --- | --- |
| `scratch-link-common/Serial/SerialSession.cs` | **신규** 크로스플랫폼 추상 세션 |
| `scratch-link-common/Serial/DiscoveredSerialPort.cs` | **신규** 발견 결과 DTO |
| `scratch-link-common/Serial/SerialDiscoveryFilter.cs` | **신규** discover 파라미터 모델 |
| `scratch-link-win/Serial/WinSerialSession.cs` | **신규** Windows 구현 |
| `scratch-link-win/Serial/WinSerialPortEnumerator.cs` | **신규** WMI 기반 포트 열거 |
| `scratch-link-win/WinSessionManager.cs` | `/scratch/serial` 라우팅 추가 |
| `scratch-link-common/scratch-link-common.projitems` | 신규 .cs 파일 등록 |
| `scratch-link-win/scratch-link-win.csproj` | `System.Management` NuGet 의존성 추가 |
| `scratch-link-win-msix/Package.appxmanifest` | `serialcommunication` capability + VID/PID 항목 |
| `Documentation/NetworkProtocol.md` | Serial 섹션 추가 (별도 PR 가능) |

기존 BLE / BT 코드는 건드리지 않는다.

## 3. 클래스 구조

```
Session                                ← scratch-link-common/Session.cs (그대로)
└─ PeripheralSession<TDiscovered, TAddress>   ← 그대로
   ├─ BLESession<…>                    (기존)
   ├─ BTSession<…>                     (기존)
   └─ SerialSession<TPort>             ★ 신규 (common, 추상)
      └─ WinSerialSession              ★ 신규 (windows, 구체 구현)
```

### 3.1 `SerialSession<TPort>` (common, 추상)

책임:

- JSON-RPC 핸들러 등록 (`getVersion`, `discover`, `connect`, `write`, `disconnect`, `startReading`, `stopReading`)
- 발견 결과 캐시 및 연결 상태 머신 관리
- 쓰기 데이터 base64 디코드, 수신 알림 base64 인코드
- 수신 응집(coalesce) 윈도우 적용 후 알림 발사

추상 멤버 (플랫폼 구현이 채움):

```csharp
protected abstract Task<IReadOnlyList<TPort>> EnumeratePortsAsync(
    SerialDiscoveryFilter filter, CancellationToken ct);

protected abstract Task ConnectAsync(TPort port, SerialOpenParams open, CancellationToken ct);

protected abstract Task WriteAsync(byte[] data, CancellationToken ct);

protected abstract Task DisconnectAsync();

// 수신 루프에서 발사. SerialSession 이 base64 인코딩 후 클라이언트로 알림 전송.
protected event Action<byte[]> DataReceived;
```

### 3.2 `WinSerialSession : SerialSession<WinSerialPortInfo>`

- `EnumeratePortsAsync` → `WinSerialPortEnumerator.QueryAsync(filter)` 호출 ([§ 5.1](#51-포트-열거--vidpid-필터링))
- `ConnectAsync` → `System.IO.Ports.SerialPort` 인스턴스 생성·구성·`Open()`
- `WriteAsync` → `BaseStream.WriteAsync`
- 수신 루프 → 백그라운드 `Task` + `BaseStream.ReadAsync` + `CancellationToken` ([§ 5.2](#52-수신-루프-패턴))
- 분리 감지 → IOException 캐치 + WMI `__InstanceDeletionEvent` 백업 ([§ 5.3](#53-분리surprise-removal-감지))

## 4. JSON-RPC 프로토콜 (`/scratch/serial`)

기존 BLE 메서드명을 그대로 차용해 클라이언트 라이브러리의 트랜스포트 어댑터가 BLE/Serial 분기를 최소화하도록 한다.

### 4.1 `discover` (요청)

```json
{
  "jsonrpc": "2.0", "id": 1, "method": "discover",
  "params": {
    "filters": [
      { "usbVendorId": 6790, "usbProductId": 29987 }
    ]
  }
}
```

- 응답은 즉시 (`{}`). 발견된 디바이스는 `didDiscoverPeripheral` 알림으로 push.
- `usbVendorId` / `usbProductId` 는 10진수 정수.
- 필터가 비어 있으면 모든 USB Serial 디바이스를 반환.

### 4.2 `didDiscoverPeripheral` (서버→클라 알림)

```json
{
  "jsonrpc": "2.0", "method": "didDiscoverPeripheral",
  "params": {
    "peripheralId": "COM7",
    "name": "USB-SERIAL CH340 (COM7)",
    "vendorId": "0x1A86",
    "productId": "0x7523",
    "rssi": 0
  }
}
```

- `peripheralId` 는 COM 포트명을 그대로 사용 → `connect` 시 동일 값으로 전달받음.
- `rssi` 는 BLE 와의 메시지 호환을 위해 항상 0 으로 채움.

### 4.3 `connect` (요청)

```json
{
  "jsonrpc": "2.0", "id": 2, "method": "connect",
  "params": {
    "peripheralId": "COM7",
    "baudRate": 1000000,
    "dataBits": 8,
    "parity": "none",
    "stopBits": "one",
    "flowControl": "none"
  }
}
```

- `baudRate` 외 파라미터는 모두 선택. 기본값: `dataBits=8`, `parity="none"`, `stopBits="one"`, `flowControl="none"`.
- 연결 성공 시 수신 알림이 자동으로 활성화된다. 명시적 토글이 필요하면 `startReading` / `stopReading` 메서드 사용 (BLE 의 `startNotifications` 와 별개의 Serial 전용 명명).

### 4.4 `write` (요청)

```json
{
  "jsonrpc": "2.0", "id": 3, "method": "write",
  "params": { "message": "<base64>", "encoding": "base64" }
}
```

응답: 송신된 바이트 수 (정수).

### 4.5 `serialDidReceiveData` (수신 알림)

Serial 전용 명명. BLE 의 `characteristicDidChange` / BT 의 `didReceiveMessage` 와 명확히 구분하기 위해 별도 명명을 쓴다 — 클라이언트 코드에서 BT/BLE 와 혼동 없이 즉시 식별 가능.

```json
{
  "jsonrpc": "2.0", "method": "serialDidReceiveData",
  "params": { "message": "<base64>", "encoding": "base64" }
}
```

### 4.6 분리 / 오류

- `serialDidDisconnect` 알림 — Serial 전용 명명. BLE/BT 의 `peripheralDidDisconnect` 와 구분.

    ```json
    {
      "jsonrpc": "2.0", "method": "serialDidDisconnect",
      "params": { "reason": "device", "message": "USB device removed" }
    }
    ```

    - `reason`: `"user"` | `"device"` | `"error"` | `"shutdown"` (선택 필드, 없으면 `"error"` 로 해석).
    - `message`: 사람이 읽을 수 있는 보조 메시지 (선택).
- 표준 JSON-RPC 2.0 error 응답 (코드/메시지).

## 5. Windows 구현 세부

### 5.1 포트 열거 + VID/PID 필터링

`System.IO.Ports.SerialPort.GetPortNames()` 만으로는 VID/PID 를 얻을 수 없으므로 **WMI** 로 보완한다.

```sql
SELECT DeviceID, PNPDeviceID, Caption
FROM Win32_PnPEntity
WHERE PNPClass = 'Ports' AND PNPDeviceID LIKE 'USB%'
```

- `PNPDeviceID` 예: `USB\VID_1A86&PID_7523\6&1A2B3C4D&0&5`
- 정규식 `VID_([0-9A-F]{4})&PID_([0-9A-F]{4})` 로 VID/PID 추출.
- `Caption` 끝의 `(COMx)` 에서 COM 번호 캡쳐.

필터 매칭에 성공한 항목을 [§ 4.2](#42-diddiscoverperipheral-서버클라-알림) 형식으로 1건씩 알림 발사.

> `.NET 6` 에서는 `System.Management` 가 NuGet 패키지로 분리되어 있으므로 `scratch-link-win.csproj` 에 다음을 추가한다:
>
> ```xml
> <PackageReference Include="System.Management" Version="6.0.*" />
> ```

### 5.2 수신 루프 패턴

```csharp
_rxCts = new CancellationTokenSource();
_rxLoop = Task.Run(async () =>
{
    var buf = new byte[4096];
    try
    {
        while (!_rxCts.IsCancellationRequested)
        {
            int n = await _port.BaseStream.ReadAsync(buf, 0, buf.Length, _rxCts.Token);
            if (n > 0)
            {
                RaiseDataReceived(new ReadOnlySpan<byte>(buf, 0, n).ToArray());
            }
        }
    }
    catch (OperationCanceledException) { /* 정상 종료 */ }
    catch (IOException) { OnSurpriseRemoval(); }
});
```

- `SerialPort.DataReceived` 이벤트는 **사용하지 않는다.** 콜백이 ThreadPool 에서 호출되며 OS 버퍼링이 비결정적이라 1Mbps 환경에서 드롭 사례가 보고된다.
- `ReadTimeout` / `WriteTimeout` 은 무한 (`InfiniteTimeout`) 으로 두고 취소는 `CancellationToken` 으로 제어한다.
- 수신 직후 즉시 알림을 발사하면 메시지 폭주가 가능 → 1\~5 ms 의 **응집 윈도우** 를 두고 모은 바이트를 한 번에 알림으로 전송. 기본값은 1 ms 로 시작해 실제 디바이스로 측정 후 조정.

### 5.3 분리(Surprise Removal) 감지

`SerialPort` 는 USB 분리 시 `ReadAsync` 가 즉시 예외를 던지지 않고 hang 하는 사례가 있어 이중 방어한다.

1. **WMI 이벤트** 구독:

    ```sql
    SELECT * FROM __InstanceDeletionEvent WITHIN 2
    WHERE TargetInstance ISA 'Win32_PnPEntity'
    ```

    이벤트의 `TargetInstance.PNPDeviceID` 가 현재 연결된 디바이스와 일치하면 `_rxCts.Cancel()` + `_port.Dispose()` + `serialDidDisconnect` 알림 (`reason: "device"`).
2. `ReadAsync` 캐치블록의 `IOException` / `OperationCanceledException` 처리에서도 동일 정리 경로를 호출. WMI 가 누락되어도 자가 복구되도록 한다.

### 5.4 MSIX manifest

`scratch-link-win-msix/Package.appxmanifest` 의 `<Capabilities>` 에 추가:

```xml
<Capabilities>
  <DeviceCapability Name="serialcommunication">
    <Device Id="vidpid:1A86 7523">
      <Function Type="name:serialPort" />
    </Device>
  </DeviceCapability>
</Capabilities>
```

> 본 구현은 `System.IO.Ports.SerialPort` 를 사용하므로 WinRT capability 가 엄격히 필수는 아니다. 그러나 (a) 미래의 WinRT API 전환 여지와 (b) MSIX 사용자에게 명시적 권한 동의를 받기 위해 등록을 권장한다.

후속 칩셋(CP210x, FTDI 등)을 지원할 때는 `<Device>` 항목만 추가하면 된다.

### 5.5 라우팅

`scratch-link-win/WinSessionManager.cs` 의 `MakeNewSession` switch 에 한 줄 추가:

```csharp
case "/scratch/serial":
    return new WinSerialSession(webSocket);
```

## 6. 시퀀스 (CH340 예)

```mermaid
sequenceDiagram
    autonumber
    participant GUI as Scratch GUI
    participant Link as scratch-link
    participant Port as CH340 (COM7)

    GUI->>Link: ws://localhost:20111/scratch/serial
    GUI->>Link: discover { filters:[{usbVendorId:0x1A86, usbProductId:0x7523}] }
    Link-->>GUI: { } (ok)
    Link->>GUI: didDiscoverPeripheral { peripheralId:"COM7", vendorId:"0x1A86", ... }
    GUI->>Link: connect { peripheralId:"COM7", baudRate:1000000 }
    Link->>Port: SerialPort.Open()
    Link-->>GUI: { } (ok)

    GUI->>Link: write { message:"<b64>" }
    Link->>Port: BaseStream.WriteAsync
    Link-->>GUI: { sentBytes: N }

    Port-->>Link: (bytes available)
    Link->>GUI: serialDidReceiveData { message:"<b64>" }

    Note over Port: USB 분리
    Link->>GUI: serialDidDisconnect { reason:"device" }
```

## 7. 식별 / 브랜딩 점검 (배포 전)

- `scratch-link-win-msix/Package.appxmanifest` 의 `Identity Name`, `PublisherDisplayName`, 트레이 아이콘 리소스, 자동 시작 `StartupTask` ID 를 aluxcoding 배포본 브랜드로 교체.
- 라이센스: 본 fork 의 베이스는 `develop` 브랜치 머지된 **AGPL-3.0-only** (커밋 `18ee302`). 자체 배포 시 AGPL 의무 (소스 공개) 가 발생하므로 배포 정책 사전 확인 필요.

## 8. 미해결 / 후속 결정 사항

1. **수신 응집 윈도우 기본값**: 1 ms vs 5 ms. 실제 디바이스에서 측정 후 결정.
2. **다중 동시 연결**: 한 세션당 단일 포트 (BLE 패턴과 동일). 다중이 필요해지면 별도 세션을 여는 방식으로 운영.
3. **타 칩셋 추가**: CH341 / CP210x / FTDI 는 동일 `SerialSession` 을 그대로 쓰고 manifest 의 VID/PID 항목과 `discover.filters` 호출만 다르게.
4. **로깅**: 기존 scratch-link 의 로깅 채널에 `SerialSession` 로그를 동일 레벨로 통합.
5. **테스트 디바이스 확보**: CH340 보드 1대 + 분리/재삽입 + 1Mbps 연속 송수신 테스트.
6. **클라이언트 라이브러리 정렬 PR**: `aluxcoding-scratch` 의 `scratch-link-client` 가 본 문서 § 1 의 명세대로 변경되어야 함.
