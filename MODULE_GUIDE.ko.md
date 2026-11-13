# CheryTools 모듈 제작 가이드

[简体中文](MODULE_GUIDE.md) | [English](MODULE_GUIDE.en.md) | **한국어**

이 문서는 CheryTools용 서드파티 모듈을 만들려는 개발자를 위한 안내입니다. 모듈은 `Mods\CheryTools\Modules\`에 두는 독립 DLL이며, CheryTools 프레임워크가 로드해 패널 왼쪽 내비게이션에 표시합니다.

---

## 1. 서드파티 모듈의 로드 방식

- 공식 모듈은 KeyViewer, Overlayer, AET Recorder, Tools 네 가지뿐입니다. 정식 릴리스 패키지는 이들의 SHA-256 해시를 기록하며, 프레임워크가 로드할 때 하나씩 확인합니다.
- 그 밖의 DLL, 그리고 파일 이름은 공식 모듈과 같지만 해시가 다른 DLL은 모두 서드파티 모듈로 취급합니다.
- 서드파티 모듈은 **기본적으로 로드되지 않습니다**. 플레이어가 프레임워크 설정에서 "Allow third-party modules"(서드파티 모듈 로드 허용)를 체크하고 게임을 재시작해야 합니다.
- 서드파티 모듈이 로드되어 있는 동안:
  - 이번 실행에서 CheryTools의 UMM 모드 ID가 `CheryTools-Unofficial`로 바뀌어, Together처럼 화이트리스트로 모드를 식별하는 도구는 더 이상 CheryTools로 인식하지 않습니다.
  - 게임 화면 왼쪽 아래에 "CT third-party modules active" 안내가 계속 표시됩니다.
  - 사이드바의 모듈 이름 뒤에 서드파티 표시가 붙습니다.
- 서드파티 모듈은 공식 지원 대상이 아닙니다. 문제를 신고할 때는 먼저 서드파티 모듈을 제거한 뒤 재현해야 합니다.

---

## 2. 반드시 지켜야 할 규칙

1. **플레이 결과에 영향을 주지 마세요.** 판정, 입력 타이밍, 키 입력, 오토플레이, 정확도, 점수 등 플레이 결과를 바꾸는 어떤 것도 수정해서는 안 됩니다. 부정행위에 쓰이는 모듈은 CheryTools와 무관하며, 그 책임은 모듈 제작자에게 있습니다.
2. **공식 모듈을 사칭하지 마세요.**
   - 파일 이름을 `CheryTools.`로 시작하지 마세요.
   - 공식 모듈의 ID(`keyviewer.preview`, `overlayer.preview`, `aet.recorder`, `tools.preview`)나 공식 모듈 이름을 표시 이름으로 쓰지 마세요.
   - 이름이나 설명에서 공식 모듈이라고 주장하지 마세요.
3. **검증을 우회하지 마세요.** 프레임워크와 공식 모듈 파일을 교체하거나 수정하지 말고, CheryTools의 UMM 모드 ID를 바꾸지 말며, 왼쪽 아래의 서드파티 안내를 숨기거나 가리지 마세요.
4. **플레이어 데이터를 수집하거나 업로드하지 마세요.** 네트워크 연결이 필요하다면 어느 주소에 연결해 무엇을 보내는지 설명에 적어 주세요.
5. **기본 정보를 밝혀 주세요.** 릴리스 설명에 지원하는 CheryTools 버전, 지원하는 게임 버전, 라이선스, 연락처를 적어 주세요.

---

## 3. 프로젝트 만들기

- 대상 프레임워크: .NET Framework 4.8 (`net48`).
- `CheryTools.Sonnet.Contracts.dll`만 참조하면 됩니다. 이 파일은 CheryTools 패키지에 포함되어 있으니 모듈과 함께 **배포하지 마세요**.
- UI를 그리려면 ImGui.NET 1.91.6.1이 필요합니다. 이것도 CheryTools가 제공하므로 함께 배포하지 마세요.
- Unity나 게임 API가 필요하면 게임의 `A Dance of Fire and Ice_Data\Managed` 폴더에 있는 어셈블리를 참조하고, 역시 함께 배포하지 마세요.

프로젝트 파일 예시:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <AssemblyName>YourName.YourModule</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="CheryTools.Sonnet.Contracts">
      <HintPath>path\to\CheryTools.Sonnet.Contracts.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(ManagedAssembliesDir)\UnityEngine.CoreModule.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <PackageReference Include="ImGui.NET" Version="1.91.6.1" ExcludeAssets="runtime" />
  </ItemGroup>
</Project>
```

빌드한 DLL을 `Mods\CheryTools\Modules\`에 넣으면 됩니다. 프레임워크는 이 폴더만 검사하고 하위 폴더는 검사하지 않습니다. 모듈이 의존하는 다른 DLL도 이 폴더에 두며, 이 파일들도 서드파티 파일로 취급됩니다.

---

## 4. 모듈 인터페이스

모듈은 `ICheryModule`을 구현한 public 클래스입니다.

| 멤버 | 설명 |
|---|---|
| `Metadata` | 모듈 정보 (아래 참고) |
| `Tabs` | 모듈 내부의 탭 목록 |
| `Initialize(ModuleContext)` | 로드할 때 한 번 호출 |
| `Update()` | 매 프레임 호출 |
| `Draw(ModuleUiContext)` | 플레이어가 모듈 페이지를 열어 두는 동안 매 프레임 호출되며, UI를 그림 |
| `Shutdown()` | 언로드할 때 호출 |

`ModuleMetadata(id, displayName, version, category, accentColor, apiVersion)`:

- `id`: 고유 식별자입니다. `제작자.모듈명` 형식을 권장합니다. 예: `yourname.mymodule`. 이미 사용 중인 ID의 모듈은 건너뜁니다.
- `displayName`: 사이드바에 표시되는 이름.
- `apiVersion`: 기본값을 그대로 두세요. `ModuleApi.CurrentVersion`(현재 1)과 같아야 하며, 다르면 프레임워크가 로드를 거부합니다.
- `NavigationTitle` / `NavigationTitleEn`: 선택 사항. 모듈 내부 내비게이션의 중국어·영어 제목.

`ModuleTab(id, title, titleEn)`: 탭. `titleEn`은 선택 사항이며, 인터페이스가 영어일 때 표시됩니다.

`ModuleContext`가 제공하는 것:

- `ModuleDirectory`: 모듈이 있는 폴더
- `Logger`: 게임 로그에 기록 (`Info` / `Warning` / `Error`)
- `Ugui`: 프레임워크가 할당한 UGUI 루트 오브젝트
- `Visuals`: 패널 배경 읽기, ImGui 텍스처 등록

에디터처럼 메인 패널과 별도로 표시해야 하는 창이 있다면 `ICheryModuleOverlay`도 구현하세요.

최소 예제:

```csharp
using System.Collections.Generic;
using System.Numerics;
using CheryTools.Sonnet.Contracts;
using ImGuiNET;

public sealed class MyModule : ICheryModule
{
    private IModuleLogger _logger;

    public ModuleMetadata Metadata { get; } =
        new ModuleMetadata("yourname.mymodule", "My Module", "1.0.0", "Tools", new Vector4(0.4f, 0.8f, 1f, 1f));

    public IReadOnlyList<ModuleTab> Tabs { get; } = new[] { new ModuleTab("main", "主页", "Home") };

    public void Initialize(ModuleContext context)
    {
        _logger = context.Logger;
        _logger.Info("My module loaded");
    }

    public void Update() { }

    public void Draw(ModuleUiContext context)
    {
        ImGui.TextUnformatted("Hello CheryTools");
    }

    public void Shutdown() { }
}
```

저장소의 `Modules/Example`이 완전한 예제 프로젝트입니다.

---

## 5. 실행 규칙

**ImGui**

- 자체 ImGui 컨텍스트를 만들거나 `ImGui.NewFrame()`, `ImGui.Render()`를 호출하지 마세요. 그리기는 `Draw`와 `DrawOverlay` 안에서만 하세요.
- 플레이어 입력, 파일 경로, 태그 결과 같은 동적 텍스트는 `ImGui.TextUnformatted`로 표시하세요. `ImGui.Text`는 텍스트를 서식 문자열로 해석하므로, `%`가 들어 있으면 게임이 충돌할 수 있습니다.
- `BeginChild`를 호출했다면 반환값이 false여도 반드시 `EndChild`를 호출하세요.

**UGUI**

- 게임 화면에 표시하는 것은 `context.Ugui`를 통해, 프레임워크가 할당한 루트 오브젝트 아래에 만드세요. CheryTools가 꺼져 있는 동안 프레임워크가 이 루트를 숨깁니다.
- 직접 만든 오브젝트는 `Shutdown`에서 정리하세요.

**예외**

- 프레임워크는 `Initialize`, `Update`, `Draw`에서 발생한 예외를 잡아, 오류가 난 모듈만 비활성화하고 다른 모듈에는 영향을 주지 않습니다.
- Harmony 패치, 코루틴, 이벤트 콜백에서 발생한 예외는 프레임워크가 잡을 수 없으니 직접 처리하세요.

**Harmony**

- `yourname.mymodule`처럼 고유한 Harmony ID를 쓰고, `Shutdown`에서 패치를 해제하세요.
- 제네릭 메서드에는 패치하지 마세요. 일부 게임 버전에서 문제가 생깁니다.

**설정 파일**

- `ModuleDirectory` 안에 자신만의 파일 이름으로 저장하세요. `Framework.xml`이나 이름이 `CheryTools.`로 시작하는 설정 파일을 읽거나 쓰지 마세요.

**게임 버전**

- CheryTools는 3.3.1(r148)과 3.4.0(r150)을 모두 지원하며, 두 버전은 게임 코드가 일부 다릅니다. 버전마다 다른 멤버는 리플렉션으로 접근하고, 두 버전 모두에서 테스트하세요.

**성능**

- `Update`와 `Draw`는 매 프레임 실행됩니다. 그 안에서 큰 메모리 할당이나 오래 걸리는 작업은 피하세요.

---

## 6. 라이선스

- `CheryTools.Sonnet.Contracts`는 MIT 라이선스로 배포됩니다. 이것만 참조하는 모듈은 비공개를 포함해 어떤 라이선스든 사용할 수 있습니다.
- 모듈이 CheryTools 프레임워크나 공식 모듈의 코드를 복사하거나 수정해서 사용했다면, 그 모듈은 CheryTools의 파생 저작물이므로 GPL-3.0으로 공개해야 합니다.

---

## 7. 배포 안내

- 배포할 때 서드파티 모듈이라는 점, CheryTools 설정에서 서드파티 모듈 로드를 허용해야 한다는 점, 그리고 2절 5번 규칙에서 요구하는 정보를 함께 적어 주세요.
- 모듈에 대한 플레이어의 문의는 직접 처리하고, CheryTools 공식 채널로 안내하지 마세요.
