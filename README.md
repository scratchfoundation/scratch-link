# Alux Scratch Link

Alux Scratch Link는 Scratch 3.0과 PC에 연결된 하드웨어 주변기기를 중계하는 도우미 앱입니다.
[scratchfoundation/scratch-link](https://github.com/scratchfoundation/scratch-link)의 **Windows 전용 포크**이며,
원본의 AGPL-3.0-only 라이선스를 그대로 따릅니다.

원본 Scratch Link와의 차이:

- **Windows 전용** — macOS 빌드와 Safari 확장은 빼고 Windows 패키징에 집중합니다.
- **Serial 전송 추가** — 기존 BLE / Bluetooth Classic에 더해 USB 시리얼(CDC/CH340 등) 장치를
  `/scratch/serial` JSON-RPC 엔드포인트로 지원합니다. 구현은 `scratch-link-common/Serial/`과
  `scratch-link-win/Serial/`을 참고하세요.
- **포트 20211 사용** — 원본 Scratch Link(20110/20111)와 한 PC에서 공존할 수 있도록 별도 포트를 씁니다.

## 시스템 요구사항

| | 최소 사양 |
| --- | --- |
| Windows | Windows 10 build 17763 |

Windows App Runtime 1.2가 필요하며 가능한 경우 자동 설치됩니다. 수동 설치가 필요하면 아키텍처에 맞게 받으세요:

* https://aka.ms/windowsappsdk/1.2/latest/windowsappruntimeinstall-x64.exe
* https://aka.ms/windowsappsdk/1.2/latest/windowsappruntimeinstall-x86.exe
* https://aka.ms/windowsappsdk/1.2/latest/windowsappruntimeinstall-ARM64.exe

## Scratch 3.0과 함께 쓰기

1. Alux Scratch Link 설치 후 실행
2. [Scratch 3.0](https://scratch.mit.edu) 열기
3. 블록 카테고리 아래쪽의 "확장 기능 추가" 버튼(블록 모양 + 아이콘) 선택
4. micro:bit, LEGO EV3 같은 지원 확장 선택
5. 안내에 따라 주변기기 연결
6. 새 블록으로 프로젝트 작성. Alux Scratch Link가 Scratch와 하드웨어 사이의 통신을 중계합니다.

## 개발

### 문서

전반적인 네트워크 프로토콜과 지원 하드웨어 프로토콜은 `Documentation/` 아래에 마크다운으로 정리되어 있습니다
(Architecture, Bluetooth, BluetoothLE, NetworkProtocol, TestPlans). 프로토콜 호환성/안정성은
중요한 우선순위이므로, 프로토콜을 바꾸는 PR은 충분한 정당화와 문서 갱신이 동반되어야 합니다.

문서 PR을 보내기 전 [markdownlint](https://www.npmjs.com/package/markdownlint)로 점검해주세요.

### 버전 번호

이 포크는 [SharedProps/ScratchVersion.targets](SharedProps/ScratchVersion.targets)에서 base 버전을
`1.0.0`으로 고정해두고, 빌드 번호는 git commit 수에서 가져옵니다. 결과 4-part 버전은
`1.0.0.<commits>` 형태로 EXE 파일 속성과 트레이 메뉴에 노출됩니다.

정식 릴리즈를 끊을 때는 `git tag v1.1.0`처럼 semver 태그를 찍으세요. GitInfo가 태그를 감지하면
위의 1.0.0 고정 로직이 자동으로 비켜나 태그값을 따라갑니다.

확장 버전 정보(`git describe`와 유사한 상세 문자열)는 트레이 메뉴의 버전 항목을 클릭해
클립보드로 복사할 수 있습니다.

### 브랜드 자산

앱/트레이/MSIX에 쓰이는 모든 아이콘은 [brand/alux-l.svg](brand/alux-l.svg) 하나에서 파생됩니다.
SVG가 갱신되면 다음 명령으로 ICO/PNG를 재생성하세요:

```
pip install Pillow            # 최초 1회
python brand/build_icons.py
```

생성물은 모두 커밋되어 있어 일반 빌드 시에는 이 스크립트를 돌릴 필요가 없습니다.

### Windows 패키징과 설치 파일 크기

`PublishReadyToRun`(R2R) 설정은 ahead-of-time(AOT) 컴파일을 활성화합니다(반대는 JIT). 시작 시간 등
성능에는 유리하지만, [R2R 바이너리는 IL 코드와 네이티브 코드를 모두 포함하기 때문에
크기가 더 커집니다](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run).

.NET 5.0 이상에서는 설정에 따라 "Framework-Dependent Application" 또는 "Self-Contained Application"으로
빌드할 수 있습니다.

* **Self-contained** — .NET 런타임을 함께 번들합니다. 플랫폼별(x86/x64/ARM64) `dotnet.exe`가
  포함되어야 해서 빌드 결과가 커집니다.
  * 네이티브 런타임 일부를 포함하므로 "AnyCPU"로는 빌드할 수 없습니다.
  * 앱이 쓰는 부분만 남기는 "trimming"이 가능하지만, 그래도 framework-dependent보다는 큽니다.
* **Framework-dependent** — 런타임을 포함하지 않으며 별도 설치가 필요합니다.
  * 생성된 MSIX는 필요 시 자동 설치를 트리거합니다(인터넷 연결 필요).
  * 네이티브 부분이 없으므로 "AnyCPU"로 빌드할 수 있습니다.
  * 원하면 특정 CPU로도 빌드 가능합니다.
  * 디버깅 시에는 프로젝트 파일에서 `<WindowsPackageType>None</WindowsPackageType>` 설정이 필요합니다.

패키징 시:

* MSIX 파일(`*.msix`)은 한 번에 하나의 플랫폼(x86, x64, ARM64)만 담을 수 있습니다.
* MSIX 번들(`*.msixbundle`)은 여러 MSIX를 묶을 수 있어 플랫폼별 MSIX를 한 번에 배포하기 좋습니다.

이상적으로는 단일 "AnyCPU" 빌드를 stub MSIX와 함께 묶어 플랫폼별 런타임을 설치하게 하면
번들 크기를 최소화할 수 있습니다. 다만 이 구성은 추가 조사가 필요합니다.

대안으로, 플랫폼별 MSIX 안에 AnyCPU 빌드를 담을 수 있습니다. 이 경우 x86/x64/ARM64 세 카피를 묶어도
플랫폼별 self-contained 번들보다 훨씬 작습니다.

R2R을 끄고 AnyCPU 빌드를 묶은 결과는, 같은 플랫폼 세트의 self-contained 번들 대비 약 12% 크기였습니다.
