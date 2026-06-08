# SignPath Foundation OSS 무료 코드사이닝 신청서

**제출일**: 2026-06-08
**제출 URL**: https://signpath.org/apply
**프로젝트**: AluxLabs Link (`aluxrobot/scratch-link`)
**예상 검토 기간**: 2~4주

---

## 배경

- 한국 법인은 Azure Trusted Signing(미국·캐나다·EU·UK 한정) 사용 불가
- 상용 EV 인증서는 연 80~100만원 비용
- AluxLabs Link는 AGPL-3.0-only OSS이므로 **SignPath Foundation OSS 플랜(무료)** 자격 충족
  - public GitHub repo ✅
  - OSI 승인 라이선스(AGPL-3.0) ✅
  - GitHub Actions CI 빌드 ✅
  - 활성 프로젝트 ✅

---

## 신청서 필드 입력값

| 필드 | 입력값 |
|---|---|
| Project Name ★ | `AluxLabs Link` |
| Repository URL ★ | `https://github.com/aluxrobot/scratch-link` |
| Homepage URL ★ | `https://github.com/aluxrobot/scratch-link` |
| Download URL | `https://scratch-link.aluxcoding.com/` |
| Privacy Policy URL | (비움) |
| Wikipedia URL | (비움) |
| Tagline ★ | `Windows desktop bridge between Scratch 3.0 and educational hardware (BLE, Bluetooth, USB Serial).` |
| Description ★ | (아래 본문) |
| Reputation ★ | (아래 본문) |
| Maintainer Type | `For-profit company or corporate-backed project` |
| Build System | `GitHub Actions` |
| First Name ★ | 본인 영문 이름 |
| Last Name ★ | 본인 영문 성 |
| Email ★ | 회사 이메일 또는 개인 이메일 |
| Company Name | `Alux Co., Ltd.` |
| Primary Discovery Channel ★ | `Google search` |
| Please specify the exact source | `Searched for free code signing alternatives after Azure Trusted Signing was found unavailable for Korean entities.` |
| Code of Conduct 동의 ★ | ✅ |
| 개인정보 처리 동의 ★ | ✅ |

★ = 필수

---

## Description (제출 본문)

```
AluxLabs Link is a small Windows desktop application that bridges
Scratch 3.0 (a visual programming environment widely used in education)
with hardware peripherals — BLE devices, Bluetooth Classic devices,
and USB serial ports.

This repository is a Windows-focused, rebranded fork of Scratch
Foundation's scratch-link project. The fork has been renamed to
"AluxLabs Link" to clearly distinguish it from upstream, while
remaining a derivative work under the same AGPL-3.0-only license.
It extends hardware support for educational robotics platforms
distributed in the Korean market and rebuilds the Windows app on
.NET 8 + WinUI 3 + Windows App SDK 1.8 (upstream was .NET 6).

End users are students, teachers, and parents who install AluxLabs
Link to connect physical robots and sensors to Scratch 3.0 lessons
through Alux's educational curriculum.
```

---

## Reputation (제출 본문)

```
This project is a fork of "scratch-link" maintained by the Scratch
Foundation (https://github.com/scratchfoundation/scratch-link),
which is a core component of Scratch 3.0 — one of the most widely
used educational programming platforms in the world, reaching tens
of millions of learners. The upstream repository is the official
hardware bridge shipped on scratch.mit.edu.

Our fork ("AluxLabs Link") targets the Korean educational market
through Alux, a Korean education company. The signed Windows builds
will be distributed to schools, after-school robotics academies, and
individual learners who use Alux's educational robot kits with
Scratch 3.0. Distribution channel is https://scratch-link.aluxcoding.com/
served by our own CloudFront CDN. Expected reach: schools and
individual learners across South Korea using Alux's robotics
curriculum.

The fork extends upstream's hardware support (adds USB Serial
transport, modernizes the Windows runtime to .NET 8 + WinUI 3 +
Windows App SDK 1.8), and is maintained as an active downstream
that tracks upstream where applicable.

License: GNU AGPL-3.0 (inherited from upstream).
```

---

## 제출 시점의 사전 작업 (참고)

신청 직전에 develop 브랜치에 반영해둔 사항 — 심사관이 repo 첫 화면에서 즉시 확인 가능하도록:

1. **README 최상단 영문 요약 박스** (PR #11)
   - 프로젝트 성격, 라이선스, upstream 관계, SignPath 사용 사실을 영문 1단락에 집약

2. **README `## 코드사이닝` 섹션** (PR #9)
   - SignPath Foundation 코드사이닝 명시 (한·영 병기)
   - Download URL 요건 충족: *"This page must mention that the project uses the SignPath Foundation for code signing."*

3. **CI/CD 파이프라인 구축** (PR #5)
   - `release.yml`로 tag 푸시 → MSIX 빌드 → S3 업로드 → CloudFront invalidation
   - 빌드 산출물: `AluxLabs-Link-{version}.msixbundle`

4. **upstream 잔재 워크플로 비활성화** (PR #10)
   - 옛 `ci.yml`, `signature-assistant.yml`의 자동 트리거 주석 처리

---

## 승인 후 후속 작업 (TODO)

- [ ] SignPath.io 조직 가입 안내 메일 수신
- [ ] Signing Policy 설정:
  - GitHub repo·branch 제한 (`aluxrobot/scratch-link`, `develop` 또는 tag만)
  - 산출물 type·해시 검증
- [ ] API Token + Organization ID + Project Slug 발급
- [ ] GitHub Secrets 등록:
  - `SIGNPATH_API_TOKEN`
  - `SIGNPATH_ORG_ID`
- [ ] `release.yml`에 SignPath signing step 추가 (`signpath/github-action-submit-signing-request@v1`)
- [ ] dev 채널 빌드로 서명 동작 검증
- [ ] README의 "승인 절차 완료 전까지는 임시 자체서명 인증서가 사용되며..." 안내문 제거

---

## 자격 박탈 / 정책 변경 시 대응 시나리오

만약 SignPath Foundation이 자격 재심사에서 박탈하거나 정책을 바꿀 경우:

- **대체 1순위**: SSL.com eSigner EV (연 ~$590)
- **대체 2순위**: Certum Cloud EV (연 ~€269)
- **대체 3순위**: DigiCert KeyLocker EV (연 ~$700)

모두 한국 법인 발급 가능, GitHub Actions 통합 지원.
`release.yml`의 signing step만 교체하면 전환 가능한 구조로 설계.
