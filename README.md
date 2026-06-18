# Gauge

Gauge는 Claude Code와 Codex의 실제 사용량 한도를 Windows 시스템 트레이에서 확인하는 작은 WinUI 3 앱입니다. 트레이 아이콘을 클릭하면 Windows 11 빠른 설정과 비슷한 Acrylic 팝오버가 열리고, 도구별 5시간 및 주간 사용량과 초기화 시각을 한 카드에 표시합니다.

현재 범위는 Claude Code와 Codex입니다. Gemini와 Antigravity는 지원하지 않습니다.

## 주요 기능

- Claude Code와 Codex의 실제 rate-limit 사용량 표시
- 도구별로 제공되는 사용량 창만 동적 렌더링
- 70%/90% 임계값에 따른 진행 막대 및 트레이 아이콘 상태 표시
- 60초 주기 갱신과 팝오버를 열 때의 즉시 갱신
- 공급자별 오류 격리 및 마지막 정상 데이터 유지
- 공식 CLI 로그인을 이용한 인증 복구
- Windows 시작 시 자동 실행 설정
- 라이트/다크 작업 표시줄용 트레이 아이콘
- 단일 인스턴스 실행

## 요구 사항

- Windows 10 2004(빌드 19041) 이상 또는 Windows 11
- x64 PC
- Claude Code CLI 및/또는 Codex CLI 설치
- 사용할 도구의 공식 CLI에 로그인된 상태

자체 포함 설치본에는 .NET 런타임과 Windows App SDK가 포함되므로 별도 런타임 설치가 필요하지 않습니다. 소스에서 빌드하려면 .NET 10 SDK가 필요합니다.

## 실행

1. `GaugeSetup-win-x64.exe`로 설치합니다(관리자 권한 불필요). 완료 화면에서 바로 실행할 수 있습니다.
2. 작업 표시줄 알림 영역의 Gauge 아이콘을 왼쪽 클릭합니다.

앱은 일반 메인 창을 띄우지 않습니다. 트레이 아이콘을 오른쪽 클릭하면 자동 시작을 전환하거나 앱을 종료할 수 있습니다.

서명되지 않은 로컬 빌드에서는 Windows SmartScreen의 알 수 없는 게시자 경고가 나타날 수 있습니다.

## 로그인과 데이터

Gauge는 자격증명을 직접 발급하거나 갱신하지 않습니다. 각 공식 CLI가 관리하는 파일을 읽기 전용으로 사용합니다.

| 도구 | 자격증명 위치 | 로그인 명령 |
| --- | --- | --- |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` | `claude /login` |
| Codex | `%CODEX_HOME%\auth.json` 또는 `%USERPROFILE%\.codex\auth.json` | `codex login` |

로그인이 필요하면 팝오버의 설정 화면에서 해당 CLI 로그인 프로세스를 열 수 있습니다. Gauge는 CLI 자격증명 파일을 쓰거나 삭제하지 않으며, 토큰이나 CLI 로그인 출력을 기록하지 않습니다.

사용량은 각 CLI가 사용하는 OAuth usage endpoint에서 HTTPS로 조회합니다.

- Claude Code: Anthropic OAuth usage endpoint
- Codex: ChatGPT `wham/usage` endpoint

Claude endpoint의 강한 rate limit을 피하기 위해 Claude 데이터는 최소 약 5분 간격으로만 네트워크에서 가져오며, 그 사이에는 마지막 정상 값을 사용합니다. 429 응답에는 지수 백오프를 적용합니다. Codex는 일반적인 60초 갱신 주기를 사용합니다.

## 소스에서 빌드

```powershell
dotnet restore Gauge.csproj
dotnet build Gauge.csproj -c Debug -p:Platform=x64
```

Debug 실행 파일:

```text
bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Gauge.exe
```

테스트:

```powershell
dotnet test Gauge.Tests\Gauge.Tests.csproj -c Debug
```

## 설치 프로그램 만들기

Gauge의 기본 설치 방식은 관리자 권한이 필요 없는 Inno Setup 설치 프로그램입니다. 설치 위치, 프로그램 그룹, 설치 준비 화면을 생략해 진행 화면과 완료 화면만 표시합니다.

Inno Setup 6을 한 번 설치합니다.

```powershell
winget install --id JRSoftware.InnoSetup --scope user
```

그다음 설치 프로그램을 빌드합니다.

```powershell
.\build-installer.ps1
```

결과:

```text
dist\GaugeSetup-win-x64.exe
```

설치 위치는 `%LOCALAPPDATA%\Programs\Gauge`이며 UAC 승격이 필요하지 않습니다. 시작 메뉴와 제거 프로그램 항목이 등록되고, 제거할 때 Gauge의 자동 시작 레지스트리 값도 함께 정리됩니다.

현재 프로젝트 파일에는 x64와 ARM64 runtime identifier가 정의되어 있지만, `build-installer.ps1` 패키징 경로는 x64 전용입니다.

## 릴리스 배포

배포 버전은 `Gauge.csproj`의 `<Version>`에서 옵니다. 이 값으로 `v<버전>` 태그(예: `0.1.0` → `v0.1.0`)를 만들어 GitHub Release에 설치 프로그램을 올립니다. 앱 내 업데이트 기능도 같은 버전을 읽어 비교합니다.

릴리스할 때:

1. `Gauge.csproj`의 `<Version>`을 올리고 커밋·푸시합니다.
2. 다음 중 하나로 GitHub Release를 만듭니다.

**로컬에서 (GitHub CLI 사용):**

```powershell
.\release.ps1
```

`release.ps1`은 설치 프로그램을 빌드한 뒤 `gh`로 `v<버전>` 릴리스를 생성하고 `GaugeSetup-win-x64.exe`를 자산으로 업로드합니다. (`gh auth login` 필요. 같은 태그가 이미 있으면 자산만 교체합니다. `-Draft`, `-Notes "..."` 지원.)

**자동으로 (GitHub Actions):**

`v*` 태그를 푸시하면 `.github/workflows/release.yml`이 설치 프로그램을 빌드해 릴리스를 발행합니다.

```powershell
git tag v0.1.0
git push origin v0.1.0
```

## 업데이트

Gauge는 시작 시 GitHub의 최신 Release를 조용히 확인하고, 새 버전이 있으면 설정 화면의 **업데이트** 카드에 표시합니다. 카드의 **업데이트 확인** 버튼으로 수동 확인도 가능합니다.

새 버전이 있을 때 **지금 업데이트**를 누르면 설치 프로그램을 내려받아 자동(무인) 모드로 실행합니다. 실행 중인 Gauge가 종료되고, 같은 위치에 새 버전이 설치된 뒤 자동으로 다시 시작됩니다. 관리자 권한은 필요하지 않습니다.

## 구조

Gauge의 가장 중요한 규칙은 데이터 수집과 UI의 분리입니다.

```text
IUsageProvider
  ├─ ClaudeProvider
  └─ CodexProvider
         ↓
   UsageSnapshot
         ↓
 UsageService / UsageCoordinator
         ↓
   UsageViewModel
         ↓
 PopoverWindow / TrayIconService
```

- 모든 공급자는 `IUsageProvider.GetSnapshotAsync`를 구현합니다.
- 공급자별 응답은 공통 `UsageSnapshot` 모델로 정규화됩니다.
- UI와 ViewModel은 공급자 구현이나 원본 JSON 구조를 알지 않습니다.
- 한 공급자의 실패는 다른 공급자 갱신을 막지 않습니다.
- 팝오버는 별도 borderless `AppWindow`이며 `DesktopAcrylicBackdrop`을 사용합니다.

세부 구현 규칙과 공급자 스키마, polling 제약은 [AGENTS.md](AGENTS.md)를 참고하세요.

## 현재 제한 사항

- CLI가 갱신하는 토큰에 의존하므로 오랫동안 CLI를 사용하지 않아 토큰이 만료되면 CLI에서 다시 로그인해야 할 수 있습니다.
- 앱 자체 OAuth/PKCE 및 토큰 갱신은 구현되어 있지 않습니다.
- 공식 코드 서명이 포함되어 있지 않습니다.
- 배포 자동화는 현재 x64만 대상으로 합니다.
