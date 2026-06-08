# scratch-vm 측 연동 식별자 변경 안내

본 문서는 `feature/rebrand-aluxlabs-link` 브랜치에서 진행된 AluxLabs Link 리브랜딩 과정에서 **scratch-vm 및 그 파생 라이브러리에 영향을 줄 수 있는 식별자 변경**을 정리한다. scratch-vm 측 (또는 자체 fork) 에서 호환 작업을 진행할 때 참고용.

## 1. 배경

기존 scratch-link 는 브라우저/Scratch 에디터 측 코드가 다음 전역 식별자를 통해 link 와 통신했다:

- `ScratchLinkWebSocket` (전역 클래스, WebSocket 트랜스포트)
- `ScratchLinkSafariSocket` (전역 클래스, Safari WebExtension 트랜스포트)
- `Scratch` (전역 컨테이너 객체, `Scratch.BLE`, `Scratch.BT` 형태로 link 클라이언트 인스턴스 보관)
- `<script id="scratch-link-extension-script">` (Safari 확장이 주입하는 DOM 노드 ID)

AluxLabs Link 로 제품명을 통일하면서, 위 식별자 이름이 옛 브랜드를 남기는 유일한 외부 노출 지점이 되었다. **브랜드 일관성**과 **scratch-link 와의 명확한 분리** (다른 클라이언트가 동시에 두 link 와 통신할 때 충돌 회피) 를 위해 식별자를 변경한다.

## 2. 변경된 식별자 (Old → New)

### 외부 노출 클래스 (scratch-vm 정의, 우리 측에서 호출)

| 옛 이름 | 새 이름 | 정의 위치 (scratch-vm) |
|---|---|---|
| `ScratchLinkWebSocket` | `AluxLabsLinkWebSocket` | `node_modules/scratch-vm/src/util/scratch-link-websocket.js` 계열 |
| `ScratchLinkSafariSocket` | `AluxLabsLinkSafariSocket` | Safari WebExtension 측 |

### 전역 컨테이너

| 옛 이름 | 새 이름 | 용도 |
|---|---|---|
| `self.Scratch` / `window.Scratch` | `self.AluxLabs` / `window.AluxLabs` | link 클라이언트 인스턴스 보관용 컨테이너 |
| `Scratch.BLE` | `AluxLabs.BLE` | BLE 클라이언트 인스턴스 |
| `Scratch.BT` | `AluxLabs.BT` | BT 클라이언트 인스턴스 |

### 우리 측 자체 클래스 (참고용, scratch-vm 영향 없음)

| 옛 이름 | 새 이름 |
|---|---|
| `ScratchLinkClient` | `AluxLabsLinkClient` |
| `ScratchBLE` | `AluxLabsBLE` |
| `ScratchBT` | `AluxLabsBT` |

### Safari WebExtension 주입 노드

| 옛 ID | 새 ID |
|---|---|
| `scratch-link-extension-script` | `aluxlabs-link-extension-script` |

## 3. scratch-vm 측 작업 요청 사항

### 3.1 필수 — WebSocket 클래스 재노출

기존:
```js
// scratch-vm/src/util/scratch-link-websocket.js (가칭)
class ScratchLinkWebSocket { /* ... */ }
module.exports = ScratchLinkWebSocket;
```

권장 변경 (둘 중 하나):

**옵션 A — 새 이름으로 export + 옛 이름 alias 유지 (호환):**
```js
class AluxLabsLinkWebSocket { /* ... */ }
module.exports = AluxLabsLinkWebSocket;
module.exports.ScratchLinkWebSocket = AluxLabsLinkWebSocket; // 임시 alias
```

**옵션 B — 새 이름으로만 export (clean break):**
```js
class AluxLabsLinkWebSocket { /* ... */ }
module.exports = AluxLabsLinkWebSocket;
```

### 3.2 필수 — Safari WebExtension 측 식별자

Safari 확장이 페이지에 주입하는 script 노드 ID 와 노출하는 전역 클래스 이름을 함께 변경:

- 주입 스크립트가 등록하는 클래스: `window.ScratchLinkSafariSocket` → `window.AluxLabsLinkSafariSocket`
- (또는 위 옵션 A 처럼 양쪽 등록)
- 주입 노드 식별자: `scratch-link-extension-script` → `aluxlabs-link-extension-script`

### 3.3 선택 — Scratch 컨테이너 객체 통합

scratch-vm 이 `self.Scratch` 컨테이너에 의존하는지 확인하고, 의존한다면:
- `self.Scratch` 와 `self.AluxLabs` 양쪽에 같은 참조를 보관 (단기 호환)
- 또는 `self.AluxLabs` 만 사용 (clean break)

playground 예시는 `self.AluxLabs.BLE`, `self.AluxLabs.BT` 만 사용하도록 이미 변경되었다.

## 4. 호환성 — 변경되지 않은 것

다음은 외부 호환을 위해 **그대로 유지**된다:

- **JSON-RPC 2.0 메서드 이름** — `discover`, `connect`, `read`, `write`, `send`, `getVersion`, `pingMe`, `didDiscoverPeripheral`, `didReceiveMessage` 등. 와이어 프로토콜 변경 없음.
- **WebSocket 엔드포인트 경로** — `/scratch/ble`, `/scratch/bt` 그대로. 클라이언트 코드 수정 불필요.
- **NetworkProtocol 버전 번호** — 변경 없음 (현재 `1.3`).
- **npm 패키지명** — 우리 측에서 scratch-vm 을 참조할 때 `./node_modules/scratch-vm/...` 경로를 그대로 사용. 패키지명 자체는 외부 의존성이므로 scratch-vm 측 결정 사항.

## 5. 우리 측 적용 위치 (참고)

`feature/rebrand-aluxlabs-link` 브랜치에서 변경된 파일:

- `global.d.ts` — TypeScript 타입 선언 갱신
- `playground.js` — 모든 식별자/클래스/컨테이너 참조 갱신
- `playground.html` — script ID, title, placeholder, 전역 컨테이너 초기화

서버 측 (C# / .NET) 은 JSON-RPC 와 WebSocket 경로만 의존하므로 본 변경의 영향 없음.

## 6. 단기 호환이 필요한 경우 — 클라이언트 측 alias

scratch-vm 측 수정이 완료될 때까지 임시로 양쪽 식별자를 함께 인식하려면 클라이언트 페이지에서:

```js
// 임시 호환 — scratch-vm 측이 정식 변경하면 제거
self.AluxLabs = self.AluxLabs || self.Scratch || {};
window.AluxLabsLinkWebSocket = window.AluxLabsLinkWebSocket || window.ScratchLinkWebSocket;
window.AluxLabsLinkSafariSocket = window.AluxLabsLinkSafariSocket || window.ScratchLinkSafariSocket;
```

scratch-vm 측이 새 이름으로 정식 export 하면 위 호환 코드는 삭제.
