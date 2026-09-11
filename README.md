<div align="center">

<img src="./assets/app.ico" width="112" height="112" alt="PcMate 아이콘" />

# PcMate

**내 PC의 상태를 캐릭터의 움직임으로 보여주는 Windows 데스크톱 친구**

CPU, 메모리, GPU, 네트워크 사용량을 숫자만이 아니라<br />
화면 위 캐릭터의 행동으로 자연스럽게 확인해 보세요.

[Microsoft Store에서 설치](https://apps.microsoft.com/detail/9n263p97lstg?hl=ko-KR&gl=KR&ocid=pdpshare) · [itch.io에서 다운로드](https://nabura.itch.io/pcmate)

</div>

---

## PcMate 소개

PcMate는 컴퓨터 화면 위에 작은 캐릭터를 띄워 두고 PC 자원 사용량을 실시간으로 보여주는 데스크톱 앱입니다.

작업 관리자를 계속 열어 두지 않아도 캐릭터가 **퍼질러 자고, 멍하니 서 있고, 걷고, 뛰는 모습**을 통해 현재 시스템 부하를 직관적으로 알 수 있습니다. 컴퓨터가 갑자기 느려졌다면 말풍선을 켜서 어떤 프로그램이 자원을 많이 사용하고 있는지도 확인할 수 있습니다.

필요할 때만 표시하거나 항상 위에 둘 수 있으며, 원하는 이미지로 나만의 캐릭터를 만들어 사용할 수도 있습니다.

## 주요 기능

- **실시간 자원 모니터링** — 메모리, CPU, GPU 사용률과 네트워크 수신 속도를 확인합니다.
- **상태 기반 캐릭터 애니메이션** — 자원 사용량에 따라 `퍼질러 자기 → 멍때리기 → 적당히 걷기 → 냅다 뛰기` 상태로 변화합니다.
- **상위 사용 프로세스 안내** — 말풍선을 통해 메모리, CPU 또는 GPU를 많이 사용하는 프로그램을 보여줍니다.
- **나만의 캐릭터 등록** — GIF, PNG, JPG 이미지를 상태별로 등록하고 수정하거나 삭제할 수 있습니다.
- **세밀한 동작 설정** — 자원별 상태 경계값과 애니메이션 속도(`0.5x`~`4x`)를 조절할 수 있습니다.
- **자유로운 화면 배치** — 창의 위치와 크기, 항상 위 표시 여부를 원하는 대로 설정할 수 있습니다.
- **표시 항목 선택** — 자원 표시줄, 말풍선, 이미지 테두리와 다크 모드를 각각 켜고 끌 수 있습니다.
- **한국어·영어 지원** — 시스템 언어를 따르거나 앱에서 직접 언어를 선택할 수 있습니다.
- **설정 자동 저장** — 마지막으로 사용한 캐릭터, 위치, 크기와 표시 설정을 다음 실행 때 복원합니다.

## 설치

PcMate는 Windows x64 환경을 지원합니다.

### Microsoft Store

가장 간편하게 설치하려면 Microsoft Store를 이용하세요.

**[Microsoft Store에서 PcMate 설치하기](https://apps.microsoft.com/detail/9n263p97lstg?hl=ko-KR&gl=KR&ocid=pdpshare)**

### itch.io

설치 없이 실행하는 압축 파일은 itch.io에서 받을 수 있습니다.

1. **[PcMate 다운로드 페이지](https://nabura.itch.io/pcmate)**에서 Windows용 ZIP 파일을 받습니다.
2. 압축을 원하는 폴더에 풉니다.
3. `PcMate.exe`를 실행합니다.

## 사용 방법

1. PcMate를 실행하면 캐릭터가 데스크톱 위에 나타납니다.
2. 캐릭터를 드래그해 원하는 위치로 옮기고 창 크기를 조절합니다.
3. 캐릭터를 마우스 오른쪽 버튼으로 눌러 모니터링할 자원과 표시 옵션을 선택합니다.
4. **캐릭터** 메뉴에서 기본 캐릭터를 바꾸거나 직접 준비한 이미지를 등록합니다.
5. **상태 경계값**에서 캐릭터가 걷거나 뛰기 시작하는 기준을 자원별로 설정합니다.

## 소스에서 실행하기

개발에는 Windows와 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)가 필요합니다.

```powershell
dotnet restore PcMate.csproj
dotnet run --project PcMate.csproj
```

릴리스 빌드는 다음 명령으로 만들 수 있습니다.

```powershell
dotnet publish PcMate.csproj -c Release
```

## 기술 구성

- C# / .NET 8
- WPF
- Windows 시스템 및 성능 카운터 기반 자원 모니터링
- RESX 기반 한국어·영어 현지화

## 배포 페이지

- [Microsoft Store](https://apps.microsoft.com/detail/9n263p97lstg?hl=ko-KR&gl=KR&ocid=pdpshare)
- [itch.io](https://nabura.itch.io/pcmate)
