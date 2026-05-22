# Scratch Link fork + Serial extension — 작업 계획

작성: 2026-05-22
대상 브랜치: `feature/scratch-link-websocket`
관련 코드: [libs/scratch-link-client/](../libs/scratch-link-client/), [libs/virtual-machine/src/io/web-socket-link.ts](../libs/virtual-machine/src/io/web-socket-link.ts), [libs/virtual-machine/src/blocks/extensions/codetinker/codetinker-peripheral.ts](../libs/virtual-machine/src/blocks/extensions/codetinker/codetinker-peripheral.ts)

이 문서는 Link transport 전략 전환 (자체 link 폐기 → 원본 Scratch Link fork) 의 **본 저장소 측 작업 계획** 과
**fork 가 노출해야 할 WebSocket 프로토콜 경계** 를 정의한다.

fork 내부 (C# 구현, 시리얼 드라이버, 세션 매니저, 인증서 흐름 등) 는 별도 저장소에서 작업하며 본 문서의 범위가 아니다.
fork 작업은 본 문서가 정의한 **WebSocket-level 계약** 만 충족하면 된다.

---

## 1. 배경

### 1.1 전략 전환

- **변경 전**: 자체 link 프로그램 `aluxcoding-scratch-link` 신규 개발.
    - 자체 RPC: `listSerialPorts` / `openSerial` / `closeSerial` / `write` + `sessionId` 모델.
    - 엔드포인트 `wss://localhost:28347`.
    - 본 저장소의 [libs/scratch-link-client/](../libs/scratch-link-client/) 와 [io/web-socket-link.ts](../libs/virtual-machine/src/io/web-socket-link.ts) 는 이 RPC 에 맞춰져 있음.
- **변경 후**: 원본 Scratch Link git 을 fork 해서 거기에 **Serial extension** 을 BLE/BT 와 동형으로 추가.
    - 클라이언트는 원본 Scratch Link 프로토콜 패턴 (`discover` → `didDiscoverPeripheral` → `connect` → `write` / `startNotifications`) 을 그대로 따름.
    - 원본의 세션/세팅 흐름 재활용 (loopback `ws://localhost:20111`, JSON-RPC 2.0). 1.3 시대의 wss/인증서/device-manager DNS 는 2.x 에서 폐기되어 본 작업에서도 사용하지 않는다.

### 1.2 왜 fork 인가

- 자체 link 신규 개발 시간 + 배포 문제.
- 원본 scratch-link 는 이미 검증된 세션 처리 + 다중 클라이언트 정책을 갖춤.
- BLE/BT 와 동일한 호출 패턴을 따르면 클라이언트 측 추상화가 단순해진다 (peripheral 추상화의 BLE/BT/Serial 모드 분기).
- loopback `ws://` 모델이라 인증서 발급/배포 문제도 자연 소거된다.

---

## 2. 책임 경계

이 작업은 두 저장소에 걸쳐 진행된다. 본 문서는 **본 저장소 (aluxcoding-scratch)** 의 작업과
**두 저장소 사이의 WebSocket 계약** 만 다룬다.

| 영역 | 책임 저장소 | 본 문서가 다루는가 |
|---|---|---|
| WebSocket 엔드포인트 (loopback `ws://`) | fork (별도) | ✗ (요구사항만 명시) |
| JSON-RPC 메시지 schema (메서드명, 파라미터, 결과) | 양쪽 공유 (계약) | ✓ §4 |
| Serial 드라이버, COM 포트 열거, 세션 관리 | fork (별도) | ✗ |
| 페이로드 인코딩 (base64 byte 처리) | 양쪽 공유 (계약) | ✓ §4 |
| 클라이언트 (브라우저) JSON-RPC 처리, 재연결, 메시지 라우팅 | 본 저장소 | ✓ §5 |
| IO 어댑터 (`web-socket-link.ts`) | 본 저장소 | ✓ §5 |
| codetinker peripheral 의 Link 분기 (`_isLinkMode`, `_scanLink`, lifecycle override) | 본 저장소 | ✓ §6 |
| 다른 peripheral 의 Link 통합 | 본 저장소 (후속) | △ §8 |

---

## 3. 전체 로드맵

### Phase 0 — 폐기 대상 정리 (사전 작업, 진행 중)

- [x] 자체 link 전제로 작성된 문서 제거 (`scratch-link-protocol.md`, `scratch-link-codetinker-verification.md`).
- [ ] 본 문서 (`scratch-link-fork-plan.md`) 확정.

### Phase 1 — Serial RPC 계약 확정

- [ ] §4 의 RPC 메서드/notification 명세를 fork 측 담당자와 정합 (특히 메서드명, peripheral 식별자 형식).
- [ ] 엔드포인트 / 인증서 / 포트 정책 확정 (원본 scratch-link 와 동일 vs 변경 — §4.1 참조).
- [ ] §4 본문 갱신 + fork 저장소 README 에 동일 명세 미러링.

### Phase 2 — `scratch-link-client` 재작성

기존 self-link 호환 코드의 protocol/messages, capability probe, client 의 RPC layer 를 새 계약에 맞춰 재작성.
재사용 가능한 layer:

- [transport.ts](../libs/scratch-link-client/src/transport.ts) (WebSocket + 재연결 + state machine) — **재사용**. URL/포트만 교체.
- [codec.ts](../libs/scratch-link-client/src/protocol/codec.ts) (JSON encode/decode, base64) — **재사용**. base64 라이브러리만 유지.
- [client.ts](../libs/scratch-link-client/src/client.ts) — **부분 재사용**. JSON-RPC request/response 패턴은 유지, notification 라우팅과 메서드 enum 만 교체.
- [protocol/messages.ts](../libs/scratch-link-client/src/protocol/messages.ts) — **재작성**. `ListSerialPortsParams` 등 self-link RPC 타입 전면 폐기.
- [constants.ts](../libs/scratch-link-client/src/constants.ts) — **재작성**. 포트·호스트·메서드명 변경.
- [errors.ts](../libs/scratch-link-client/src/errors.ts) — **재사용**. 명칭만 유지 (LinkXxxError).
- [capability.ts](../libs/scratch-link-client/src/capability.ts) — **부분 재작성**. probe URL 만 새 엔드포인트로.

작업 단위:
- [ ] §4 계약을 `protocol/messages.ts` 의 타입과 `LinkMethods` enum 으로 옮긴다.
- [ ] `client.ts` 에 새 high-level helper 추가 (예: `discover(filters)`, `connect(peripheralId)`, `write(message)`, `startReading()`).
- [ ] 새 엔드포인트로 `LINK_HOST` / `LINK_PORT_WS` / `LINK_URL` 갱신.
- [ ] `capability.ts` 의 probe URL 갱신 + 응답 검사 (단순 WS open 만으로 부족하면 `getVersion` 호출 추가 — §4.5).

### Phase 3 — IO 어댑터 (`web-socket-link.ts`) 재작성

기존 [web-socket-link.ts](../libs/virtual-machine/src/io/web-socket-link.ts) 의 `sessionId` 모델 (openSerial → close → write) 을 폐기하고
`discover → connect → write/startReading` 모델로 교체 (Serial 전용 명명).

작업 단위:
- [ ] `WebSocketLink` API 시그니처 검토 — peripheral 측 lifecycle override (`scan`/`connect`/`write`/`disconnect`) 와 매칭되도록 유지하면서 내부는 새 RPC 로 교체.
- [ ] `subscribe` 콜백 (`onDataReceived`, `onConnected`, `onDisconnected`, `onReadingError`, `onConnectionCancelled`) **그대로 유지** — peripheral 측 변경 영향 최소화.
- [ ] `listPorts`/`addFilter` 는 `discover(filters)` + `didDiscoverPeripheral` notification 수집 모델로 교체.
- [ ] `write(data: Uint8Array)` 의 페이로드 인코딩 / RPC 메서드명만 갱신.
- [ ] `currentSessionId` 외부 노출 검토 — 새 모델에서는 `peripheralId` 기반이므로 명칭/시그니처 정리.

### Phase 4 — codetinker peripheral Link 분기 정합

[codetinker-peripheral.ts](../libs/virtual-machine/src/blocks/extensions/codetinker/codetinker-peripheral.ts) 의
`_isLinkMode` / `_scanLink` / `scan|connect|disconnect|isConnected` override / write 흐름이 §6 의 호출 패턴과 일치하는지 확인.

작업 단위:
- [ ] [codetinker-peripheral.ts:541-583](../libs/virtual-machine/src/blocks/extensions/codetinker/codetinker-peripheral.ts) `_scanLink()` 의 filter 등록 (`addFilter({ vendorId, productId })`) 이 새 `discover(filters)` 명세에 매칭되는지 검토.
- [ ] [codetinker-peripheral.ts:498-617](../libs/virtual-machine/src/blocks/extensions/codetinker/codetinker-peripheral.ts) lifecycle override 골격 유지, 내부 어댑터 호출만 교체.
- [ ] write 흐름 (큐 처리, immediate write) 의 어댑터 메서드 시그니처 변경 반영.

### Phase 5 — 시험·검증

fork 빌드와 본 저장소 dev 서버를 함께 실행해 단대단 확인. 절차는 fork 측 README + 본 문서 §7.

---

## 4. Serial extension — WebSocket 프로토콜 명세

본 절은 fork 와 클라이언트가 **반드시 동일하게** 따라야 하는 wire 계약이다.
변경은 양쪽 PR 을 동시에 진행한다.

### 4.1 엔드포인트

- `ws://localhost:20111/scratch/serial`
- TLS / 인증서 / DNS resolve 없음. scratch-link 2.x 가 loopback 평문 모델 ([ScratchLinkApp.cs:17](../../scratch-link/scratch-link-common/ScratchLinkApp.cs#L17), [Documentation/SerialTransport.md](../../scratch-link/Documentation/SerialTransport.md) §0) 이므로 그대로 따른다.
- 포트 20111 은 정품 scratch-link 와 동일하므로 동시 실행 불가. 정품과의 공존은 본 작업의 비목표.

### 4.2 메시지 프레이밍

- WebSocket text frame, UTF-8 JSON, JSON-RPC 2.0.
- 한 frame 당 한 JSON-RPC 메시지. batch 미지원 (원본 scratch-link 정책 동일).
- 바이너리 payload (USB 시리얼 byte stream) 는 base64 문자열로 인코딩 후 JSON 필드에 담는다.

### 4.3 메서드 (client → server, request)

| 메서드 | 파라미터 | 결과 | 설명 |
|---|---|---|---|
| `getVersion` | `{}` | `{ protocol: string }` | 핸드셰이크. 클라이언트는 연결 직후 1회 호출해 호환성 검사. |
| `discover` | `{ filters: ReadonlyArray<{ usbVendorId?: number; usbProductId?: number; pathHint?: string }> }` | `{}` (빈 객체, 즉시 응답) | filter 매칭 포트 열거 시작. 결과는 `didDiscoverPeripheral` notification 으로 streaming. `usbVendorId` / `usbProductId` 는 10진수 정수. |
| `connect` | `{ peripheralId: string; baudRate: number; dataBits?: number; parity?: "none" \| "even" \| "odd"; stopBits?: "one" \| "onePointFive" \| "two"; flowControl?: "none" \| "rtsCts" \| "xonXoff" }` | `{}` | 특정 포트 연결. `peripheralId` 는 직전 `didDiscoverPeripheral` 의 식별자. baudRate 외 옵션은 생략 시 기본값 (`dataBits=8`, `parity="none"`, `stopBits="one"`, `flowControl="none"`). |
| `write` | `{ message: string; encoding: "base64" }` | `{ sentBytes: number }` | 직렬화된 byte stream 송신. `sentBytes` 는 실제 송신 byte 수. partial write 는 fork 가 보장하지 않음 (전체 송신 후 응답). |
| `startReading` | `{}` | `{}` | RX 수신 활성화. **참고**: scratch-link 측 (SerialTransport.md §4.3) 은 `connect` 직후 자동으로 RX 가 활성화되므로 본 메서드는 명시 토글이 필요한 경우에만 호출. RX byte 는 `serialDidReceiveData` notification 으로 push. (BLE 의 `startNotifications` 와 별개의 Serial 전용 명명.) |
| `stopReading` | `{}` | `{}` | RX 수신 일시 중지. write 는 계속 가능. |
| `disconnect` | `{}` | `{}` | 세션 종료 + 포트 해제. 정상 종료 시 notification 없음. |

### 4.4 notification (server → client)

| 메서드 | 파라미터 | 설명 |
|---|---|---|
| `didDiscoverPeripheral` | `{ peripheralId: string; name: string; vendorId?: string; productId?: string; rssi?: number }` | discover 중 새 포트 발견 시. `vendorId` / `productId` 는 16진 문자열 (예: `"0x1A86"`). `rssi` 는 BLE 메시지 호환을 위해 0 으로 채워질 수 있음. 같은 discover 세션 동안 같은 peripheralId 의 중복 발행은 fork 가 억제. |
| `serialDidReceiveData` | `{ message: string; encoding: "base64" }` | RX byte 도착. `connect` 성공 직후부터 자동 발행 (scratch-link 측 SoT 가 자동 활성화 모델). `stopReading` 호출 시 일시 중지. **Serial 전용 명명** — BLE 의 `characteristicDidChange` / BT 의 `didReceiveMessage` 와 구분. |
| `serialDidDisconnect` | `{ reason?: "user" \| "device" \| "error" \| "shutdown"; message?: string }` | 외부 요인 (USB 분리, fork shutdown, 드라이버 오류) 으로 세션 종료. 클라이언트의 명시적 `disconnect` 응답으로는 발행 안 함. **Serial 전용 명명** — BLE/BT 의 `peripheralDidDisconnect` 와 구분. |

> **명명 결정**: 수신 / 분리 알림은 Serial 전용 명명 (`serialDidReceiveData` / `serialDidDisconnect`) 채택. BLE/BT 와 코드상에서 한눈에 구분되도록 한다. RX 토글 메서드도 `startReading` / `stopReading` 으로 BLE 의 `startNotifications` 와 분리. scratch-link 측 SoT ([SerialTransport.md](../../scratch-link/Documentation/SerialTransport.md) §1·§4.5·§4.6) 에 정렬 (2026-05-22 확정).

### 4.5 핸드셰이크 / liveness

1. 클라이언트가 WS 연결 → fork onopen.
2. 클라이언트 → `getVersion` 요청. fork → 응답 (`{ protocol: "1.0" }` 형태).
3. 응답 `protocol` 가 클라이언트 기대 버전과 mismatch 면 클라이언트가 disconnect.
4. liveness 는 WebSocket close detection 으로만 감지. 별도 ping notification 은 두지 않는다 (scratch-link 측 미지원, 원본 BLE/BT 와 동형).
5. 클라이언트는 별도 keepalive request 를 보내지 않는다 (원본 scratch-link 와 동형).

### 4.6 에러 처리

- JSON-RPC 2.0 에러 객체 (`{ code, message, data? }`) 를 사용.
- 코드 매핑 (권장):
    - `-32600`: invalid request
    - `-32601`: method not found
    - `-32602`: invalid params (filter 형식 오류, baudRate 범위 등)
    - `-32603`: internal error (드라이버 실패)
    - 애플리케이션 에러 (예: 연결 실패, 포트 점유) 는 `-32000 ~ -32099` 범위 사용. 구체적 코드는 Phase 1 에서 확정.

---

## 5. 본 저장소 측 변경 사항

### 5.1 폐기되는 코드 / 상수

본 저장소에서 self-link 가정으로 작성된 부분 — 재작성 또는 갱신:

- [libs/scratch-link-client/src/protocol/messages.ts](../libs/scratch-link-client/src/protocol/messages.ts) 의 `ListSerialPortsParams` / `OpenSerialParams` / `CloseSerialParams` / `WriteParams` (sessionId 모델) — **전부 폐기**.
- 같은 파일의 `LinkMethods` enum — **재정의**.
- [libs/scratch-link-client/src/constants.ts](../libs/scratch-link-client/src/constants.ts) 의 `LINK_PORT_WSS = 28347` → **`LINK_PORT_WS = 20111`** (TLS 폐기로 명명도 정정). `LINK_HOST = "localhost"`, 권장 URL 상수 `LINK_URL = "ws://localhost:20111/scratch/serial"`.
- [libs/scratch-link-client/src/client.ts](../libs/scratch-link-client/src/client.ts) 의 keepalive ping 로직 — 폐기 (§4.5). WebSocket close detection 으로 대체.
- [libs/virtual-machine/src/io/web-socket-link.ts](../libs/virtual-machine/src/io/web-socket-link.ts) 의 sessionId 추적 / `openSerial → closeSerial` 흐름 — **재작성**.

### 5.2 유지되는 코드 / 추상화

peripheral 측 영향을 최소화하기 위해 다음 외부 API 는 시그니처를 유지:

- `WebSocketLink` 의 subscriber callback (`onConnected`, `onDisconnected`, `onConnectionCancelled`, `onDataReceived`, `onReadingError`, `onDataSend`).
- `WebSocketLink.connect(baudRate, options)`, `disconnect()`, `write(data: Uint8Array)`, `writeImmediate(data: Uint8Array)`, `isConnected`, `dispose()`.
- `addFilter({ vendorId, productId })` — 내부적으로 `discover(filters)` 의 filter 로 매핑.

내부적으로는 sessionId → peripheralId 로 식별자 의미가 바뀐다. 외부 노출용 `currentSessionId` getter 는
`currentPeripheralId` 로 개명하거나 deprecate.

### 5.3 신설되는 RPC 헬퍼 (client.ts)

[client.ts](../libs/scratch-link-client/src/client.ts) 의 `request<T>(method, params)` 위에 high-level 헬퍼 추가:

- `getVersion(): Promise<{ protocol: string }>`
- `discover(filters): Promise<void>` — request 응답은 `{}`. discovery 결과는 `onDidDiscoverPeripheral` callback 으로 전달.
- `connect(peripheralId, options): Promise<void>`
- `write(bytes: Uint8Array): Promise<{ sentBytes: number }>` — base64 인코딩은 헬퍼 내부.
- `startReading(): Promise<void>` / `stopReading(): Promise<void>`
- `disconnect(): Promise<void>`

콜백 인터페이스 (`LinkClientCallbacks`) 도 갱신:
- `onDataReceived` 유지 (의미: `serialDidReceiveData` notification 처리).
- `onDeviceDisconnected` 유지 (의미: `serialDidDisconnect` notification 처리).
- `onDeviceError` 유지.
- `onDidDiscoverPeripheral` 신설.
- liveness 모니터링은 WebSocket close detection 으로 처리 — 별도 콜백 불필요.

---

## 6. codetinker peripheral 통합 영향

### 6.1 유지

[codetinker-peripheral.ts](../libs/virtual-machine/src/blocks/extensions/codetinker/codetinker-peripheral.ts) 의
Link transport 1~5 차 커밋 (`bbdf5a696 ~ bb61aa819`) 에서 추가한 main-side 골격은 그대로 유지:

- `_isLinkMode()` 분기 (전역 transport 선호도 기반).
- `_scanLink()` 의 WebSocketLink 인스턴스 생성, filter 등록, subscriber 등록 패턴.
- `scan()` / `connect()` / `disconnect()` / `isConnected()` lifecycle override.
- write/flushSendQueue 의 Link 분기.

### 6.2 변경

- scratch-link 측 SoT ([SerialTransport.md](../../scratch-link/Documentation/SerialTransport.md) §4.3) 가 `connect` 직후 자동 RX 활성화 모델이므로 별도 `startReading()` 호출은 **불필요**. 명시 토글 (한시적 RX 중지 등) 이 필요할 때만 `startReading` / `stopReading` 사용 (Serial 전용 명명).
- `addFilter` 호출은 그대로 (`{ vendorId, productId }`). 내부적으로 `discover(filters)` 의 `usbVendorId` / `usbProductId` 키로 매핑.
- 단일 포트 자동 선택 로직 ([web-socket-link.ts:108-120](../libs/virtual-machine/src/io/web-socket-link.ts)) 은 `didDiscoverPeripheral` 누적 + 첫 포트 선택으로 교체. 다중 포트 picker UI 는 후속 (§8).

### 6.3 다른 peripheral

CodingDrone / CodingRider 등 Link 미지원 peripheral 은 본 작업 범위 외.
같은 패턴을 따르면 추후 통합 가능하지만 현재는 USB Serial (codetinker) 만 대상.

---

## 7. 검증 절차 (Phase 5 채워질 자리)

Phase 1~4 완료 후 본 절을 구체화한다. 현재는 골격만 기재.

1. **fork 단독 시험**: fork 저장소의 자체 단위 테스트 + 통합 시험 (별도 저장소 책임).
2. **본 저장소 단위 테스트**: `scratch-link-client` 의 RPC 헬퍼·콜백 라우팅을 mock WS 로 검증.
3. **단대단 시험**:
    - fork 빌드 실행 → tray 또는 콘솔에서 `ws://localhost:20111` listening 확인.
    - `pnpm dev` → `http://localhost:4200/ko/codetinker` 접속.
    - stage-header transport 토글에서 **Link** 선택 → USB 버튼.
    - 콘솔 / Network tab 으로 RPC 흐름 (`getVersion → discover → didDiscoverPeripheral → connect → serialDidReceiveData / write`) 확인.
    - 미션 sb3 로드 → 컨트롤러 명령 전송 / 센서 RX 동작 확인.
4. **회귀 시험**: USB transport (`_isLinkMode() === false`) 가 영향 없는지 확인.

---

## 8. 미해결 결정 사항 (Phase 1 에서 닫는다)

- [x] ~~**DNS / 포트 정책**~~ — `ws://localhost:20111` 로 확정 (2026-05-22).
- [x] ~~**메서드명 최종 확정**~~ — Serial 전용 명명 `serialDidReceiveData` / `serialDidDisconnect`, RX 토글 `startReading` / `stopReading` 채택. BLE/BT 와 코드상 명확 구분 목적 (2026-05-22).
- [x] ~~**인증서 trust 자동화**~~ — TLS 미사용으로 항목 폐기.
- [ ] **discover 종료 신호**: discover 가 시간 제한 없이 streaming 인지, fork 가 timeout 후 `didDiscoverFinished` 같은 종료 notification 을 보내는지.
- [ ] **다중 포트 picker UI**: codetinker 가 유일 시리얼이 아닐 때의 선택 UI (이번 phase 의 범위 외이지만 결정 시점 명시).
- [ ] **다중 클라이언트 정책**: 같은 peripheralId 에 두 탭이 연결 시도 시 fork 의 거부/대기 정책 (원본 scratch-link 의 BLE 정책 차용 여부).

---

## 9. 관련 자료

- 메모리: `link-transport-strategy` (전략 전환 배경).
- 원본 scratch-link 저장소 (LLK/scratch-link) — fork 작업의 출발점. BLE/BT extension 의 메시지 schema 가 Serial extension 의 참조 자료.
- codetinker Link transport 1~5 차 커밋: `bbdf5a696` ~ `bb61aa819` (main-side override 골격, 본 작업으로 변경되지 않음).
