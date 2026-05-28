# AluxLabs Link — Claude 행동 규칙

Windows-only desktop app. Scratch 3.0 ↔ 하드웨어(BLE, Bluetooth Classic, USB Serial) 중계.
C# / .NET 8 + WinUI 3, Visual Studio Solution.

---

## 1. 최우선 규칙 (위반 시 즉시 수정)

- **`Console.*` 사용 금지** → `Trace.WriteLine()` (`System.Diagnostics`) 사용 (§4 참조)
- **`aluxlabs-link-common`이 `aluxlabs-link-win`을 참조 금지** → 의존 방향은 `aluxlabs-link-win → aluxlabs-link-common` 단방향 (§8 참조)
- **`SharedProps/*.props` / `*.targets` 직접 편집 시 주의** → 전체 빌드에 영향. 개별 csproj에서 중복 선언 금지
- **기존 아키텍처·패턴을 우회하는 수정 금지** (§8 참조) → 새 코드·수정 코드에 적용. 기존 위반은 명시 요청 없이 건드리지 않는다. 단, 수정 범위 안에서 우회 코드를 발견하면 사용자에게 알리고 처리 여부를 묻는다.

## 2. Using 지시문 규칙

- `using` 선언은 반드시 **namespace 내부**에 배치 (`.editorconfig` 강제)
- 멤버 접근 시 `this.` 한정자 필수: 필드(`this.field`), 메서드(`this.Method()`), 프로퍼티(`this.Property`), 이벤트(`this.Event`)
- 불필요한 `using` 추가 금지 — 미사용 using은 컴파일 경고 원인

## 3. 코드 스타일

- 인덴트: 4 스페이스 (탭 금지)
- 줄 끝: LF (XML / CSPROJ / WAPPROJ 제외 — CRLF)
- 인코딩: UTF-8
- StyleCop Analyzers 준수 필수
- `interface` 명명: `I` 접두사 (예: `ISession`, `IPeripheralSession`)
- 미사용 변수·using 금지 (`CS8600`, `IDE0005` 등 경고 발생)

## 4. 금지 패턴

- **`Console.*` 사용 금지** → `Trace.WriteLine()` 사용
    - **예외 — 임시 진단용**: `System.Diagnostics.Debug.WriteLine("[DEBUG-XXX] ...")` 형태로 한정 허용. `[DEBUG-XXX]` 식별 prefix 필수 (grep 가능). **커밋 전 반드시 전부 제거**.
- **`dynamic` 타입 남용 금지** → 구체적 타입 또는 제네릭 사용
- **`#pragma warning disable` 무분별 사용 금지** → 근본 원인 해결 우선
- **`types.cs` / `type.cs` 파일 생성 금지** → 타입·인터페이스는 사용하는 클래스 파일 안에 함께 정의 (코로케이션)
- **일반 주석(`//`)은 한 줄 max, "WHY"만** → 기본은 주석 없음. WHY가 비자명할 때만 한 줄 추가. WHAT 설명 / 현재 작업·callers 참조 금지. 설계 의도는 commit message / PR description으로.
- **XML doc(`///`)은 public API에 한해 단일 `<summary>` 허용** → 단, caller 참조(`"Used by X"`) 및 코드에서 자명한 WHAT 설명 금지. 비자명한 동작·제약·사이드이펙트만 기술한다.
- **공용 코드 (`aluxlabs-link-common`) 의 주석·식별자에서 특정 프로토콜 구현 세부 언급 금지** → 일반화된 패턴 설명으로 표현하고, 구체적 예시는 해당 세션 클래스 안에서만 든다.

## 5. 작업 전 확인사항

코드 작성·수정 전에 반드시 확인:

1. 수정 대상 파일을 먼저 읽고 기존 패턴을 파악한다
2. **`aluxlabs-link-common` vs `aluxlabs-link-win` 중 어느 쪽에 위치해야 하는지 판단한다** — 플랫폼 API(Windows.Devices.*, WinUI) 없이 동작 가능하면 common, 그렇지 않으면 win
3. 관련 타입·인터페이스가 이미 있는지 확인한다
4. `SharedProps/`에 이미 선언된 속성·패키지 참조인지 확인한다

## 6. 작업 후 검증 체크리스트

- [ ] `Console.*`를 사용하지 않았는가? (임시 `[DEBUG-*]` 로그 전부 제거 확인)
- [ ] StyleCop 오류가 없는가?
- [ ] `using` 선언이 namespace 내부에 있는가?
- [ ] 멤버 접근에 `this.` 한정자를 사용했는가?
- [ ] 미사용 변수·using이 없는가?
- [ ] 의존 방향 규칙을 지켰는가? (`aluxlabs-link-common`이 `aluxlabs-link-win`을 참조하지 않음)
- [ ] `SharedProps/`에 이미 선언된 속성을 개별 csproj에 중복 선언하지 않았는가?
- [ ] 일반 주석(`//`)이 WHY만 담고 있는가? (WHAT / caller 참조 없음)
- [ ] XML doc(`///`)이 있다면 public API이고, caller 참조 및 자명한 WHAT 설명이 없는가?
- [ ] 기존 코드 패턴과 일관성이 있는가?

## 7. 커밋 규칙

```
feat(serial): USB Serial 트랜스포트 세션 추가
fix(ble): BLE 연결 재시도 로직 오류 수정
refactor(common): JSON-RPC 핸들러 구조 개선
docs(architecture): Serial 프로토콜 설계 문서 업데이트
```

- 접두사: `feat`, `fix`, `refactor`, `docs`, `chore`, `style`, `test`, `ci`
- scope 예시: `ble`, `bt`, `serial`, `common`, `win`, `jsonrpc`, `msix`
- 설명·본문·꼬리말은 한국어

## 8. 프로젝트 구조 핵심

- `aluxlabs-link-common/` — 플랫폼 공통 C# Shared Project (BLE/BT/Serial 프로토콜 추상화, JSON-RPC 2.0, WebSocket 핸들링). **재사용 가능한 단위로 분리**, Windows API에 종속되지 않는다.
- `aluxlabs-link-win/` — WinUI 3 플랫폼 구현 (Windows.Devices.* API 연동, 트레이 아이콘, 앱 진입점). **비즈니스 로직은 최소화**, 가능한 한 `aluxlabs-link-common`으로 위임.
- `aluxlabs-link-win-msix/` — MSIX 패키징 프로젝트. 직접 코드 편집 대상이 아니다.
- `SharedProps/` — MSBuild 공유 속성 (SDK 버전, NuGet 패키지 참조, 버전 자동화). 개별 csproj에서 중복 선언 금지.
- `Documentation/` — 아키텍처·프로토콜 설계 문서. 관련 코드 변경 시 함께 업데이트한다.
- **의존 방향: `aluxlabs-link-win` → `aluxlabs-link-common` 단방향** (§1 절대 규칙). 이유: common이 win을 참조하면 플랫폼 독립성이 깨지고 순환 의존이 발생한다.

## 9. 개발 명령어

```bash
# 권장: Visual Studio 2022+에서 aluxlabs-link.sln 열기
# 빌드 구성: Debug_Win / Release_Win

dotnet build -c Debug_Win                                    # 디버그 빌드
dotnet build -c Release_Win                                  # 릴리즈 빌드
dotnet run --project aluxlabs-link-win -c Debug_Win           # 실행

# 아이콘 생성 (cairosvg, ImageMagick, optipng 필요)
make icons
```
