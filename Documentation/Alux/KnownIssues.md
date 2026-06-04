# 알려진 이슈 — Deployment 관련

본 문서는 2026-05-26 리브랜드(`feature/rebrand-aluxlabs-link`) 시점에 deployment 검증 중 발견된 이슈와 검증 결과를 기록한다. **출하 형식·시점·코드 서명 정책 결정 시 참고용**.

마지막 검증 commit: `5209ce6 chore(msix): publish profile 을 self-contained 로 변경`
검증 환경: Windows 11 25H2 build 26200 (개발 PC, 정상 PC 동일 build).

---

## 1. Tooltip 미표시 (H.NotifyIcon.WinUI 라이브러리 이슈)

### 증상
트레이 아이콘 위에 마우스 호버 시 풍선 tooltip 이 표시되지 않는다.

### 영향 범위
**Unpackaged 모드 (F5 디버그 / Release portable EXE) 에서만 발생**. Packaged 모드 (MSIX-installed) 에서는 정상 동작.

원본 Scratch Link 가 packaged 형태로 MS Store 에서 배포되어 동작하는 것은 이 패턴과 일치.

### 검증 매트릭스
| 시나리오 | Tooltip 동작 |
|---|---|
| F5 디버그 (unpackaged) | ✗ 안 뜸 |
| Release self-contained portable EXE 직접 실행 | ✗ 안 뜸 |
| MSIX install (packaged) | (실행 자체 별도 이슈로 미검증, 원본 Scratch Link 는 정상 동작) |
| 7b4c335 (pre-.NET-8 시점) F5 빌드 | ✗ 안 뜸 — 우리 fork 변경 이전부터 동일 |
| 다른 PC 의 원본 Scratch Link 1.4.3.0 (MS Store install) | ✓ 정상 |

### 시도한 해결책 (모두 실패)
- XAML `ToolTipText` 명시 설정 — 이미 적용돼 있음. 반영 안 됨.
- 런타임에 `trayIcon.ToolTipText` 재할당 (`ForceCreate` 전/후 모두 시도). 값은 .NET property 레벨에서 변경되나 Windows shell 의 NOTIFYICONDATA 에는 반영 안 됨.
- `UpdateToolTip()` 메서드 호출 시도 — public API 에 노출되지 않음 (reflection 으로 메서드 목록 조사 완료, `UpdateIcon` 만 존재).
- NuGet 1.8.260508005 → 1.8.250907003 다운그레이드 — 효과 없음.
- Explorer.exe 재시작 (tray cache flush) — 효과 없음.
- Ghost 패키지 (`ScratchFoundation.1711508CFD202 1.0.0.0`) 제거 — 다른 이슈 해결됐으나 tooltip 영향 없음.

### 추정 원인
`H.NotifyIcon.WinUI` 2.0.108 의 ToolTipText property setter 가 Shell_NotifyIcon(NIM_MODIFY) 호출 시 `NIF_TIP` 플래그를 누락하거나, NIM_MODIFY 자체를 보내지 않는 라이브러리 버그.

### 출하 영향
**출하 차단 안 함**. 학원/학교 사용자에게 보이는 화면 (트레이 아이콘 우클릭 메뉴) 은 정상 동작하며, 메뉴의 첫 항목이 "AluxLabs Link <version>" 으로 표시되어 식별 가능.

### 향후 검토
- H.NotifyIcon.WinUI 2.x 의 더 새 버전 (2.0.110 이후) 시도 — 1809 호환 유지 확인 필수
- 라이브러리 교체 (H.NotifyIcon 의 `TrayToolTip` element 사용 또는 P/Invoke Shell_NotifyIcon 직접 호출)
- 사용자 피드백이 명시적으로 요구하지 않으면 우선순위 낮음

---

## 2. MSIX packaged 모드에서 시작 시 즉시 크래시 (Win11 25H2 특이성 추정)

### 증상
MSIX 로 packaged install 후 시작 메뉴에서 실행 시 트레이 아이콘이 등장하지 않고 즉시 종료. Event Log 에 다음 정보:

```
Faulting application: AluxLabs Link.exe v1.0.0.x
Faulting module: Microsoft.UI.Xaml.dll v3.1.8.0 (번들된 DLL)
Exception: 0xc000027b (STATUS_FATAL_USER_CALLBACK_EXCEPTION)
WER P5: combase.dll
WER P8: 0x80040111 (REGDB_E_CLASSNOTREG)
```

### 영향 범위
- 개발 PC: Windows 11 25H2 build 26200 — 크래시 재현
- 정상 PC 에서의 packaged install 검증 미완료 (cert 신뢰 단계에서 막혀 실제 install 까지 못 감)
- **Unpackaged 형태 (F5 / Release portable EXE) 는 정상 동작 — 정상 PC 와 dev PC 모두에서 검증됨**

### 추정 원인
WinUI 3 self-contained 패키징의 알려진 한계 — XAML metadata / COM 클래스 등록의 일부가 시스템 레지스트리에 의존. 번들된 DLL 의 클래스를 활성화하려 할 때 시스템 레지스트리에서 매칭되는 항목을 못 찾아 `REGDB_E_CLASSNOTREG` 발생.

Windows 11 25H2 (build 26200) 의 특이성일 가능성도 배제 못 함. 동일 build 의 정상 PC 검증 미완.

### 시도한 해결책
- NuGet WindowsAppSDK 1.8.260508005 → 1.8.250907003 다운그레이드: 동일 크래시 (timestamp 만 바뀜)
- self-contained 활성화 (`<SelfContained>true</SelfContained>`, `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>` in pubxml): 같은 크래시
- Ghost 패키지 (`ScratchFoundation.1711508CFD202`) 제거: 원본 Scratch Link 설치 문제는 해결됐으나 우리 빌드의 크래시는 별개
- .NET 6 + WAS 1.3 으로 stack 회귀 시도: **빌드 환경 비호환** — 옛 WAS NuGet 의 build target 이 .NET 10 SDK + VS 2026 (v18.0) 환경의 path 와 매칭 안 됨. WAS 1.5, 1.6 도 동일 결과.

### 출하 영향
- **Portable EXE 배포 (MSIX 없이) → 영향 없음** (검증 완료)
- **MSIX 사이드로드 배포 → 위험** (이 이슈 미해결 시 사용자 PC 에서도 같은 크래시 가능성)
- **MS Store 배포 → 미검증** (Partner Center 심사 환경에서 실행 시도 → 거기서 결과 판명)

### 향후 검토
1. 정상 PC 에서 우리 .msixbundle 의 진짜 install + 실행 검증 (cert 수동 import 통과 후)
2. 동일 build 의 다른 OS (24H2, Win10 22H2) 에서 검증
3. 위 결과에 따라:
   - 정상 PC 에서 동작 → dev PC 25H2 특이성 확정, 일반 사용자엔 OK
   - 정상 PC 에서도 크래시 → 우리 빌드의 packaged 모드 호환 문제, 더 깊은 디버그 필요

---

## 3. Ghost 패키지로 인한 원본 Scratch Link 설치 차단 (해결됨)

### 증상
개발 PC 에서 MS Store 에서 원본 Scratch Link 설치 시도 시 실패. 정상 PC 에서는 정상.

### 원인
리브랜드 이전에 빌드/배포된 옛 fork 버전이 원본의 `ScratchFoundation.1711508CFD202` Identity 로 dev PC 에 등록돼 있었음. 자체 서명 cert 로 서명된 그 ghost 가 MS-signed 정식 release 의 install 을 Identity name 충돌로 차단.

### 해결
```powershell
Get-AppxPackage -AllUsers *ScratchFoundation* | Remove-AppxPackage -AllUsers
```

리브랜드 commit `53bfd57` (MSIX Package Identity → `ALUXInc.AluxLabsLink`) 이후로는 새 빌드가 이 Identity 를 더 이상 사용하지 않으므로 같은 문제 재발하지 않음.

### 출하 영향
없음. 과거 dev install 의 환경 정리 이슈로 해결됨.

---

## 4. 검증된 사실 — 출하 결정 시 신뢰 가능

| 항목 | 상태 |
|---|---|
| 리브랜드 (식별자/이름/manifest/네임스페이스/폴더/문서) | ✅ 완료 (commit `7684c1c` 까지) |
| MSIX Identity 충돌 (Publisher CN, ScratchFoundation 잔존) | ✅ 해결 (commit `7e9f28b`) |
| Self-contained pubxml 설정 | ✅ 적용 (commit `5209ce6`) |
| Release self-contained portable EXE 빌드 | ✅ 성공 (`dotnet publish -c Release_Win -r win-x64 --self-contained true`) |
| Portable EXE 동작 (dev PC, Win11 25H2) | ✅ 트레이 정상 등장 |
| Portable EXE 동작 (정상 PC, Win11 25H2) | ✅ 트레이 정상 등장 |
| Tooltip 동작 (unpackaged) | ❌ Issue #1 deferred |
| MSIX install + 실행 (dev PC) | ❌ Issue #2 — 정상 PC 미검증 |

---

## 5. 출하 형식 결정용 비교

| 출하 형식 | 1809 호환 | 코드 서명 | 사용자 UX | 현재 검증 상태 |
|---|---|---|---|---|
| Portable EXE + ZIP | ✓ | ⚠️ EXE 직접 서명 가능 | 압축 풀고 실행 (시작 메뉴 등록 X) | ✅ 동작 확인 |
| Inno Setup / NSIS installer | ✓ | ✓ 표준 .exe 서명 | "다음 → 완료" 마법사 | 미시도 |
| MSIX 사이드로드 | ✓ (이론) | ✓ 자체 cert 또는 EV/OV | 자체 서명 cert 신뢰 등록 필요 | Issue #2 미해결 |
| MS Store | ✓ | MS 가 자동 서명 | Store 에서 "Install" 한 번 | 미등록 |

### 권장 출하 path
1. **단기 (학원 직배포)**: 코드 서명된 Inno Setup installer — Issue #2 우회, 가장 안정. **MSIX 의 알려진 한계 회피**.
2. **중기 (대규모 보급)**: MS Store 등록 검토 — MS 가 서명/runtime/업데이트 자동 처리. Issue #2 의 실제 영향은 Store 심사 단계에서 결과 확정.
3. **소규모 dev/베타**: Portable ZIP — 임시 검증용

---

## 6. 회귀 시도 (실패) — 향후 같은 시도 반복 방지

`.NET 8` + `WindowsAppSDK 1.8` stack 을 원본 `.NET 6` + `WindowsAppSDK 1.3` 으로 회귀 시도했으나 다음 이유로 빌드 자체 실패:

- 옛 `Microsoft.WindowsAppSDK` NuGet (1.3, 1.5, 1.6 모두) 의 `MrtCore.PriGen.targets` 가 `Microsoft\VisualStudio\v17.0\AppxPackage\Microsoft.Build.Packaging.Pri.Tasks.dll` 경로를 가정하는데, 우리 환경 (.NET 10 SDK 10.0.300 + VS 2026 build 18.0) 의 실제 path 구조와 불일치.
- 진짜 stack 회귀하려면 **빌드 환경까지 회귀** (VS 2022 설치 + .NET 6 SDK 강제) 가 필요. 작업량 크고 다른 호환성 이슈 사슬 가능성.

따라서 stack 회귀는 **포기**. 현재 `.NET 8` + `WindowsAppSDK 1.8.260508005` stack 으로 출하 형식을 결정하는 방향.

---

## 7. 미커밋 변경 (회귀 시도) — 모두 revert 완료

본 문서 작성 시점 (commit `5209ce6` 직후) 에 회귀 시도로 한 다음 변경들은 **모두 `git restore .` 로 revert** 했다. 작업 트리 clean 상태:

- `SharedProps/WindowsSDK.props`: WAS 1.8 → 1.3/1.5/1.6 (revert)
- `aluxlabs-link-win/aluxlabs-link-win.csproj`: net8.0 → net6.0, RuntimeIdentifiers win10- prefix (revert)
- 3 pubxml: self-contained=false (revert)
- `aluxlabs-link-win-msix/aluxlabs-link-win-msix.wapproj`: AssetTargetFallback net6.0 (revert)

git log 에 이 회귀 시도는 흔적 남지 않음.
