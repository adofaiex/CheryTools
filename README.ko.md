# CheryTools Sonnet

[简体中文](README.md) | [English](README.en.md) | **한국어**

얼불춤(A Dance of Fire and Ice)용 종합 보조 모드로, UnityModManager에서 동작합니다.

May the world... be as we wish.

> **사용 전 안내**
>
> - 서드파티 모드와 모드 로더는 게임 충돌이나 불안정을 일으킬 수 있습니다. 게임이 업데이트되면 먼저 모드와 모드 로더를 제거하고, 새 버전과의 호환이 확인된 뒤 다시 설치해 주세요.
> - CheryTools의 코드는 AI의 도움을 받아 작성되었습니다. 기능 설계, 결정 및 배포는 제작자가 책임집니다.
> - 공식 CheryTools에는 판정, 입력 타이밍, 오토플레이를 바꾸는 기능이 없습니다. 이런 기능이 있는 빌드나 모듈은 비공식이며 CheryTools와 무관합니다.

## 기능

CheryTools는 하나의 프레임워크와 여러 개의 독립 모듈로 이루어져 있습니다. 게임에서 `Insert`를 누르면 패널이 열리며(단축키는 설정에서 변경 가능), 인터페이스는 중국어 간체와 영어를 지원합니다.

### KeyViewer

- 시각적 캔버스 편집기(FreeMake): 키를 드래그해 배치하고, 범위 선택, 정렬 스냅, 일괄 편집을 지원합니다.
- 키 레인: 단색 또는 그라데이션, 페더링, 그림자, 둥근 모서리, 여러 줄의 레인을 레이어별로 표시합니다.
- 일반 키 외에도 KPS, 총 입력 수, 이미지 키, 영상 키를 배치할 수 있으며, 이미지와 영상은 여러 채우기 방식을 지원합니다.
- 여러 프로필을 언제든 전환할 수 있고, 가져오기와 내보내기를 지원합니다.

### Overlayer

- 텍스트, 이미지, 영상, 진행 바의 네 가지 컴포넌트.
- `{xacc}`, `{progress}`, `{cbpm}`, `{musictime:p}` 등 다양한 실시간 태그. 숫자 태그는 `:N` 접미사로 소수 자릿수를 지정할 수 있습니다. 예: `{xacc:2}`.
- 노드 애니메이션: 키 입력, 콤보, 판정, 레벨 시작과 종료 시점에 크기, 위치, 색상 변경이나 텍스트 교체 효과를 줍니다.
- 사용자 글꼴을 지원하며, 설정을 가져오고 내보낼 수 있습니다.

### AET Recorder

- 채보를 오프라인으로 영상 렌더링하므로, 실제 플레이 중의 프레임 저하에 영향을 받지 않습니다.
- x264, x265, NVIDIA 하드웨어 인코딩을 지원하며 해상도, 프레임 레이트, 화질(CRF)을 조절할 수 있습니다.
- 렌더링 중에는 입력을 차단하고, 영상 배경을 오디오와 동기화합니다.

### Tools

- 키 디바운스: 기계식 키보드의 채터링을 걸러냅니다.
- 키 제한: 목록에 있는 키만 게임에 전달됩니다.
- 인터페이스 정리: 판정 텍스트, 미스 표시, 레벨 제목, 오차 막대, 마지막 타일 플래시 등을 각각 숨길 수 있고, 플레이용과 녹화용 설정을 따로 둡니다.
- 에디터 선택 정보: 선택한 타일의 각도, 박자 수, 길이를 표시합니다.
- 불, 얼음, 바람 행성의 본체, 고리, 꼬리 색상과 투명도.

## 설치

1. 최신 UnityModManager를 설치하세요. 오래된 버전(예: 0.25.5)에서는 CheryTools가 제대로 로드되지 않는 것으로 확인되었습니다.
2. 릴리스 패키지를 내려받아 게임 폴더의 `Mods\CheryTools`에 패키지 전체를 압축 해제하세요. `CheryTools.Sonnet.Contracts.dll` 등의 파일이 모두 있는지 확인하고, 일부 파일만 덮어쓰지 마세요.
3. 게임을 실행하고 UMM에서 CheryTools를 활성화한 뒤 `Insert`를 눌러 패널을 여세요.

업데이트하기 전에 `Mods\CheryTools` 아래의 `Framework.xml`과 `Modules` 폴더의 설정 파일을 백업해 주세요.

### 지원하는 게임 버전

- 3.3.1 (r148)
- 3.4.0 (r150)

하나의 릴리스 패키지로 두 버전을 모두 지원합니다.

### 문제 신고

게임의 `Player.log`(`%USERPROFILE%\AppData\LocalLow\7th Beat Games\A Dance of Fire and Ice\` 안에 있음), CheryTools 버전, 게임 버전을 함께 알려 주세요.

## 소스에서 빌드하기

### 준비물

- Windows
- .NET SDK (프로젝트 대상은 .NET Framework 4.8이며, 필요한 참조 어셈블리는 NuGet으로 자동 다운로드됩니다)
- UnityModManager가 설치된 얼불춤. 빌드 시 게임의 `Managed` 폴더를 참조하며, 이 저장소에는 게임 파일이 포함되어 있지 않습니다.

### 빌드

```powershell
dotnet build .\CheryTools.Sonnet.sln -c Release -p:ManagedAssembliesDir="<게임 폴더>\A Dance of Fire and Ice_Data\Managed"
```

게임이 Steam 기본 위치(`C:\Program Files (x86)\Steam\...`)에 설치되어 있다면 `ManagedAssembliesDir`를 생략할 수 있습니다. `-p:GameExePath=<게임 exe의 전체 경로>`로 게임 위치를 지정할 수도 있습니다.

빌드 결과는 `out\`에 출력됩니다.

### 패키징

```powershell
.\package.ps1 -ManagedAssembliesDir "<게임 폴더>\A Dance of Fire and Ice_Data\Managed"
```

릴리스 패키지는 `dist\`에 생성됩니다. `-Deploy`를 붙이면 `-TargetGameDir`로 지정한 폴더에도 배포되며, 기존 사용자 설정은 덮어쓰지 않습니다.

FFmpeg는 저장소에 포함되어 있지 않습니다. 릴리스 패키지에 FFmpeg를 함께 넣으려면 패키징 전에 `ffmpeg.exe`를 `ThirdParty\FFmpeg\`에 넣으세요. 이 프로젝트는 gyan.dev의 essentials 빌드를 사용합니다. 넣지 않아도 패키징은 가능하며, AET Recorder가 게임 폴더, 모듈 폴더, 시스템 PATH 순서로 `ffmpeg.exe`를 찾습니다. 설정에서 경로를 직접 지정할 수도 있습니다.

### 폴더 구조

```text
Contracts/   프레임워크와 모듈 사이의 인터페이스 (MIT)
Framework/   UMM 진입점, 패널, 모듈 로딩, 입력 격리
Shared/      여러 어셈블리가 함께 쓰는 소스 파일
Modules/     KeyViewer, Overlayer, AetRecorder, Tools, 예제 모듈과 실험 모듈
Package/     Info.json과 기본 설정
Resources/   글꼴과 기본 배경
lib/         릴리스 패키지와 함께 배포되는 런타임 라이브러리
```

## 모듈 작성

모듈은 `Mods\CheryTools\Modules\`에 두는 독립 DLL이며, `CheryTools.Sonnet.Contracts`만 참조하면 됩니다. `Modules/Example`이 최소 예제입니다.

KeyViewer, Overlayer, AET Recorder, Tools 네 가지 공식 모듈 외의 모든 모듈은 서드파티 모듈입니다. 서드파티 모듈은 플레이어가 프레임워크 설정에서 허용하지 않으면 로드되지 않으며, 로드되어 있는 동안 CheryTools는 `CheryTools-Unofficial`로 동작하고 화면 왼쪽 아래에 안내가 계속 표시됩니다.

모듈을 만들기 전에 [모듈 제작 가이드](MODULE_GUIDE.ko.md)를 먼저 읽어 주세요. 반드시 지켜야 할 규칙, 프로젝트 설정, 인터페이스, 실행 규칙이 정리되어 있습니다.

## 라이선스

- 이 프로젝트는 [GPL-3.0](LICENSE)으로 배포됩니다.
- `Contracts/` 폴더는 [MIT](Contracts/LICENSE)로 배포됩니다.
- 글꼴, 배경 이미지, FFmpeg 등 서드파티 콘텐츠는 각자의 약관을 따릅니다. 자세한 내용은 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)를 참고하세요.

이 프로젝트를 기반으로 한 수정판은 다른 이름을 사용하고, CheryTools Sonnet에서 파생되었음을 밝혀 주세요.

## 특별히 감사드립니다

- [ADOFAI Tweaks](https://github.com/PizzaLovers007/AdofaiTweaks)
- [AETC](https://github.com/adofaiex/ADOFAI.EditorTweaks.ChartRendering) (AAA幽蓝色栗子梦男): AET Recorder의 렌더링 기능은 이 프로젝트에서 파생되었습니다

## 제작자

CherySui · ADOFAIEX
