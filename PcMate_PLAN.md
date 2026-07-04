# PcMate MVP 프로젝트 플랜

## 1. 프로젝트 개요

**PcMate**는 Windows 데스크탑 화면 위에 항상 표시되는 캐릭터형 시스템 리소스 모니터링 앱이다.

PC의 리소스 상태를 단순 숫자가 아니라 캐릭터 행동으로 시각화하는 것을 목표로 한다. 1차 MVP에서는 전체 시스템 리소스 중 **메모리 사용률**만 우선 수집하고, 사용률 구간에 따라 캐릭터 행동을 4단계로 전환한다.

이 프로젝트의 핵심은 단순한 캐릭터 앱이 아니라, **Windows 시스템 상태를 직관적인 캐릭터 행동으로 표현하는 데스크탑 오버레이 도구**를 구현하는 것이다.

---

## 2. 프로젝트 이름

```text
PcMate
```

기존 이름인 `MemoryPet`은 1차 MVP의 기능인 메모리 사용률 모니터링에는 적합하지만, 장기적으로는 범위가 너무 좁다.

향후 다음 리소스까지 확장할 가능성이 있으므로 프로젝트명은 특정 리소스에 한정되지 않아야 한다.

- Memory
- CPU
- GPU
- Disk
- Network
- Process
- Temperature
- File System Event

`PcMate`는 PC 상태를 곁에서 보여주는 동반자형 데스크탑 앱이라는 의미를 담는다.

---

## 3. 프로젝트 목표

### 3.1 핵심 목표

- Windows 화면 최상단에 고정되는 캐릭터 창 구현
- 창 크기 조정 가능
- 메모리 사용률 실시간 수집
- 메모리 사용률에 따라 캐릭터 행동 변경
- 추후 CPU/GPU/Disk/Network/Process 감시 기능으로 확장 가능한 구조 설계
- 캐릭터 앱처럼 보이지만 내부 구조는 시스템 리소스 모니터링 도구로 설계

### 3.2 1차 MVP 목표

1차 MVP는 기능을 과도하게 확장하지 않고 다음 기능만 구현한다.

```text
메모리 사용률 수집
→ 상태 4단계 분류
→ 캐릭터 행동 변경
→ 화면 최상단 오버레이 표시
→ 창 크기 조정
```

### 3.3 제외 범위

다음 기능은 1차 MVP에서 제외한다.

- 경험치 기능
- 레벨/업그레이드 기능
- 대화 기능
- AI 챗봇 기능
- 캐릭터 호감도
- 아이템/상점
- Live2D
- Steam Workshop 수준의 모드 시스템
- CPU/GPU/Disk/Network 모니터링
- 프로세스별 사용량 분석
- 보안 이벤트 감시
- 파일 변경 이벤트 감시
- 랜섬웨어 행위 탐지

위 기능들은 추후 `would 기능` 또는 확장 기능으로 분리한다.

---

## 4. 타깃 플랫폼

| 항목 | 내용 |
|---|---|
| OS | Windows 10/11 |
| 앱 유형 | Desktop Overlay Application |
| 주 언어 | C# |
| UI 프레임워크 | WPF |
| 런타임 | .NET 8 LTS `net8.0-windows` |
| 배포 형태 | 초기에는 Debug/Release exe, 추후 self-contained publish 고려 |

---

## 5. 기술 선택

### 5.1 C# + WPF 선택 이유

PcMate는 단순 CLI나 웹앱이 아니라 Windows 데스크탑 위에 떠 있는 상주형 프로그램이다. 따라서 다음 기능을 자연스럽게 다룰 수 있는 C# WPF가 적합하다.

- `Topmost`를 통한 화면 최상단 고정
- 투명 배경 창
- 프레임 없는 창
- 크기 조정 가능한 창
- 이미지/스프라이트 애니메이션
- Windows API 연동
- 실행 파일 배포
- 추후 트레이 아이콘, 시작 프로그램 등록, 설정 저장 확장

Python도 `psutil`, `PySide6`, `PyQt6` 등을 사용하면 구현 가능하지만, Windows 데스크탑 앱으로 장기 유지보수하기에는 C#이 더 적합하다.

### 5.2 Python을 사용하지 않는 이유

Python은 시스템 리소스 수집 자체는 쉽다.

```python
import psutil

memory = psutil.virtual_memory()
print(memory.percent)
```

하지만 이 프로젝트의 핵심은 리소스 수집보다 다음 요소에 있다.

- 화면 최상단 오버레이
- 투명 창
- 크기 조정
- 캐릭터 애니메이션
- Windows 앱 배포
- 장기적으로 트레이 앱화
- Windows API와의 자연스러운 연동

따라서 1차 구현부터 C# WPF를 사용한다.

---

## 6. 핵심 기능 명세

### 6.1 메모리 사용률 수집

앱은 일정 주기마다 현재 물리 메모리 사용률을 가져온다.

초기 기준:

- 갱신 주기: 1초
- 측정 대상: 전체 물리 메모리 사용률
- 반환값: 0~100 사이 정수

예상 구현 방식:

- `GlobalMemoryStatusEx` Windows API를 P/Invoke로 호출
- 또는 초기 실험 단계에서는 .NET에서 사용 가능한 성능 카운터/관리 API 검토

---

### 6.2 캐릭터 상태 분류

메모리 사용률에 따라 캐릭터 상태를 4단계로 분류한다.

| 메모리 사용률 | 캐릭터 상태 | 행동 |
|---:|---|---|
| 0% ~ 39% | `Lying` | 누워있기 |
| 40% ~ 59% | `Sitting` | 앉아있기 |
| 60% ~ 79% | `Walking` | 걷기 |
| 80% ~ 100% | `Running` | 냅다 달리기 |

초기 구현은 단순 구간 기준으로 한다.  
이후 상태가 너무 자주 바뀌면 hysteresis를 적용한다.

```text
앉기 → 걷기: 65% 이상일 때 전환
걷기 → 앉기: 55% 이하일 때 전환
```

---

### 6.3 화면 최상단 고정

창은 기본적으로 다른 창보다 위에 표시된다.

필수 조건:

- `Topmost = true`
- 사용자가 위치를 이동할 수 있어야 함
- 사용자가 크기를 조절할 수 있어야 함
- 작업 중 너무 방해되지 않도록 추후 투명도 설정 고려

초기 MVP에서는 구현 안정성을 위해 일반 창 테두리를 유지할 수 있다. 캐릭터형 앱 느낌을 강화하는 것은 2차 단계에서 처리한다.

---

### 6.4 창 크기 조정

필수 조건:

- 최소 크기 설정
- 최대 크기 제한은 선택
- 캐릭터 이미지는 창 크기에 맞게 비율 유지
- `Stretch="Uniform"` 사용

초기 기준:

```text
초기 크기: 300 x 300
최소 크기: 150 x 150
```

---

### 6.5 캐릭터 애니메이션

초기 MVP에서는 Live2D, Spine, 복잡한 GIF 처리 대신 **PNG 프레임 기반 애니메이션**을 사용한다.

예시 파일 구조:

```text
Assets/
  Character/
    Lying/
      lying_000.png
      lying_001.png
      lying_002.png
    Sitting/
      sitting_000.png
      sitting_001.png
      sitting_002.png
    Walking/
      walking_000.png
      walking_001.png
      walking_002.png
      walking_003.png
    Running/
      running_000.png
      running_001.png
      running_002.png
      running_003.png
```

상태별 프레임 전환 속도 예시:

| 상태 | 프레임 간격 |
|---|---:|
| Lying | 600ms |
| Sitting | 500ms |
| Walking | 180ms |
| Running | 80ms |

---

## 7. 시스템 구조

### 7.1 전체 구조

```text
ResourceMonitor
    ↓
ResourceSnapshot
    ↓
StateClassifier
    ↓
CharacterState
    ↓
AnimationController
    ↓
OverlayWindow
```

### 7.2 1차 MVP 구조

1차 MVP에서는 `ResourceMonitor`의 구체 구현체로 `MemoryMonitor`만 사용한다.

```text
MemoryMonitor
    ↓
ResourceSnapshot
    ↓
StateClassifier
    ↓
CharacterState
    ↓
AnimationController
    ↓
MainWindow
```

### 7.3 컴포넌트 역할

| 컴포넌트 | 역할 |
|---|---|
| `IResourceMonitor` | 시스템 리소스 모니터의 공통 인터페이스 |
| `MemoryMonitor` | 현재 메모리 사용률 수집 |
| `ResourceSnapshot` | 특정 시점의 시스템 리소스 상태 데이터 |
| `StateClassifier` | 리소스 사용률을 캐릭터 상태로 변환 |
| `CharacterState` | 캐릭터 상태 enum |
| `AnimationController` | 상태에 맞는 프레임 애니메이션 제어 |
| `MainWindow` | 최상단 오버레이 UI 표시 |
| `MainViewModel` | UI 상태 바인딩 및 갱신 |

---

## 8. 프로젝트 디렉터리 구조

```text
PcMate/
  PcMate.sln
  PcMate.csproj
  App.xaml
  App.xaml.cs
  MainWindow.xaml
  MainWindow.xaml.cs

  Models/
    CharacterState.cs
    ResourceSnapshot.cs

  Monitors/
    IResourceMonitor.cs
    MemoryMonitor.cs

  Services/
    StateClassifier.cs
    AnimationController.cs
    WindowPlacementStore.cs

  ViewModels/
    MainViewModel.cs

  assets/
    characters/

  Utils/
    NativeMemoryApi.cs
```

---

## 9. 주요 클래스 설계

### 9.1 CharacterState

```csharp
public enum CharacterState
{
    Lying,
    Sitting,
    Walking,
    Running
}
```

### 9.2 ResourceType

```csharp
public enum ResourceType
{
    Memory,
    Cpu,
    Gpu,
    Disk,
    Network
}
```

1차 MVP에서는 `Memory`만 사용한다.

### 9.3 ResourceSnapshot

```csharp
public sealed class ResourceSnapshot
{
    public int MemoryUsagePercent { get; init; }
    public DateTime CollectedAt { get; init; }
}
```

추후 확장 시 다음 필드를 추가할 수 있다.

```csharp
public int? CpuUsagePercent { get; init; }
public int? GpuUsagePercent { get; init; }
public int? DiskUsagePercent { get; init; }
public int? NetworkUsagePercent { get; init; }
```

단, 초기에 모든 필드를 추가하지 않고 실제 기능 확장 시점에 반영한다.

### 9.4 StateClassifier

```csharp
public sealed class StateClassifier
{
    public CharacterState Classify(ResourceSnapshot snapshot)
    {
        int memoryPercent = snapshot.MemoryUsagePercent;

        return memoryPercent switch
        {
            < 40 => CharacterState.Lying,
            < 60 => CharacterState.Sitting,
            < 80 => CharacterState.Walking,
            _ => CharacterState.Running
        };
    }
}
```

### 9.5 IResourceMonitor

```csharp
public interface IResourceMonitor
{
    ResourceSnapshot GetSnapshot();
}
```

### 9.6 MemoryMonitor

```csharp
public sealed class MemoryMonitor : IResourceMonitor
{
    public ResourceSnapshot GetSnapshot()
    {
        int memoryUsagePercent = NativeMemoryApi.GetMemoryUsagePercent();

        return new ResourceSnapshot
        {
            MemoryUsagePercent = memoryUsagePercent,
            CollectedAt = DateTime.Now
        };
    }
}
```

---

## 10. UI 설계

### 10.1 초기 MainWindow 요구사항

- 화면 최상단 표시
- 크기 조정 가능
- 메모리 사용률 텍스트 표시
- 캐릭터 이미지 표시
- 드래그 이동 가능

초기 XAML 예시:

```xml
<Window
    x:Class="PcMate.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="PcMate"
    Topmost="True"
    Width="300"
    Height="300"
    MinWidth="150"
    MinHeight="150"
    ResizeMode="CanResize"
    WindowStyle="SingleBorderWindow"
    Background="Transparent">

    <Grid>
        <Image
            x:Name="CharacterImage"
            Stretch="Uniform" />

        <Border
            HorizontalAlignment="Right"
            VerticalAlignment="Top"
            Margin="8"
            Padding="8"
            CornerRadius="8"
            Background="#AA000000">
            <TextBlock
                x:Name="ResourceText"
                Foreground="White"
                FontSize="14" />
        </Border>
    </Grid>
</Window>
```

2차 단계에서는 다음 설정을 검토한다.

```xml
WindowStyle="None"
AllowsTransparency="True"
Background="Transparent"
```

---

## 11. 상태 갱신 흐름

앱 실행 후 다음 루프를 반복한다.

```text
1. 1초마다 타이머 Tick 발생
2. MemoryMonitor가 메모리 사용률 수집
3. ResourceSnapshot 생성
4. StateClassifier가 캐릭터 상태 결정
5. 이전 상태와 다르면 AnimationController가 애니메이션 세트 변경
6. UI에 현재 메모리 사용률과 캐릭터 프레임 반영
```

---

## 12. 개발 단계

### 12.1 0단계: 프로젝트 초기화

작업 내용:

- WPF 프로젝트 생성
- Git 저장소 초기화
- 기본 창 실행 확인
- README 작성

완료 기준:

- 앱 실행 시 빈 WPF 창이 뜬다.
- 솔루션명과 프로젝트명이 `PcMate`로 설정되어 있다.

---

### 12.2 1단계: 최상단 창 구현

작업 내용:

- `Topmost=true` 적용
- 창 크기 조정 가능 설정
- 최소 크기 설정
- 위치 이동 가능 확인

완료 기준:

- 다른 프로그램 위에 항상 표시된다.
- 창 크기를 조절할 수 있다.

---

### 12.3 2단계: 메모리 사용률 수집

작업 내용:

- `GlobalMemoryStatusEx` 연동
- 메모리 사용률을 1초마다 조회
- 화면에 `%`로 표시

완료 기준:

- 앱에서 현재 메모리 사용률이 실시간으로 갱신된다.

---

### 12.4 3단계: 상태 분류 구현

작업 내용:

- `CharacterState` enum 생성
- `ResourceSnapshot` 생성
- `StateClassifier` 구현
- 메모리 사용률 구간별 상태 결정
- 현재 상태 텍스트 표시

완료 기준:

- 메모리 사용률에 따라 `Lying`, `Sitting`, `Walking`, `Running` 상태가 바뀐다.

---

### 12.5 4단계: 캐릭터 이미지 전환

작업 내용:

- 상태별 임시 이미지 준비
- 상태 변경 시 이미지 교체
- 창 크기에 맞춰 이미지 비율 유지

완료 기준:

- 메모리 사용률 구간에 따라 캐릭터 이미지가 변경된다.

---

### 12.6 5단계: 프레임 애니메이션

작업 내용:

- 상태별 PNG 프레임 로드
- 상태별 프레임 전환 속도 지정
- `DispatcherTimer` 또는 별도 애니메이션 루프 구성

완료 기준:

- `Walking`, `Running` 상태에서 캐릭터가 움직인다.
- `Running`은 `Walking`보다 빠르게 움직인다.

---

### 12.7 6단계: 사용성 개선

작업 내용:

- 창 위치 저장
- 창 크기 저장
- 종료 버튼 또는 컨텍스트 메뉴 추가
- 투명 배경 적용 검토
- 프레임 없는 창 적용 검토

완료 기준:

- 앱을 껐다 켜도 이전 위치와 크기가 복원된다.
- 사용자가 앱을 쉽게 종료할 수 있다.

---

## 13. MVP 완료 기준

다음 조건을 만족하면 1차 MVP 완료로 간주한다.

- [x] Windows에서 실행되는 WPF 앱이다.
- [x] 솔루션명과 앱 이름이 `PcMate`이다.
- [x] 화면 최상단에 고정된다.
- [x] 창 크기를 조절할 수 있다.
- [x] 현재 메모리 사용률을 실시간으로 표시한다.
- [x] 메모리 사용률에 따라 캐릭터 상태가 4단계로 바뀐다.
- [ ] 최소 4종류의 캐릭터 상태 이미지 또는 애니메이션을 가진다.
- [x] 코드가 모니터링 계층, 상태 분류 계층, UI 계층으로 분리되어 있다.
- [x] 추후 CPU/GPU/Disk/Network 모니터를 추가할 수 있는 구조를 가진다.

---

## 14. 리스크 및 대응

### 14.1 상태가 너무 자주 바뀌는 문제

문제:

- 메모리 사용률이 경계값 근처에서 흔들리면 캐릭터 상태가 계속 바뀔 수 있다.

대응:

- hysteresis 적용
- 최소 상태 유지 시간 설정

```text
최소 3초 동안 같은 상태 유지
또는
전환 기준과 복귀 기준을 다르게 설정
```

---

### 14.2 캐릭터 이미지 품질 문제

문제:

- 개발보다 이미지 제작에 시간이 과도하게 들어갈 수 있다.

대응:

- 초기에는 임시 도형/무료 리소스 사용
- 캐릭터 퀄리티는 MVP 이후 개선
- Live2D는 1차 MVP에서 제외

---

### 14.3 최상단 창이 방해되는 문제

문제:

- 사용자가 작업 중 불편함을 느낄 수 있다.

대응:

- 위치 이동 가능
- 크기 조정 가능
- 투명도 조절 기능 추가
- 항상 위 기능 on/off 추가
- 추후 트레이 메뉴 추가

---

### 14.4 WPF GIF 처리 문제

문제:

- WPF 기본 Image 컨트롤은 GIF 애니메이션 처리에 제약이 있다.

대응:

- PNG 프레임 애니메이션 직접 구현
- 또는 WPF 애니메이션 라이브러리 검토
- MVP에서는 직접 프레임 전환 방식 우선

---

### 14.5 이름과 범위 불일치 문제

문제:

- 1차 MVP가 메모리 사용률 기반이라 프로젝트가 메모리 전용 앱처럼 보일 수 있다.

대응:

- 프로젝트명은 `PcMate`로 유지
- README와 포트폴리오 설명에서 “1차 MVP는 메모리 기반”이라고 명시
- 코드 구조는 `MemoryMonitor`를 `IResourceMonitor`의 한 구현체로 둔다

---

## 15. 향후 확장 계획

향후 확장 계획은 현재 MVP 구현 목표와 분리하기 위해 별도 문서로 관리한다.

- [PcMate_EXPANSION_PLAN.md](./PcMate_EXPANSION_PLAN.md)

현재 문서는 1차 MVP 구현과 당장 필요한 구조 설계만 다룬다.

---

## 16. 포트폴리오 표현 방식

이 프로젝트는 단순히 “귀여운 캐릭터 앱”으로 설명하면 약하다.  
취업/포트폴리오에서는 다음과 같이 표현하는 것이 좋다.

```text
PcMate는 Windows 데스크탑 환경에서 동작하는 시스템 리소스 모니터링 오버레이 앱입니다.
1차 MVP에서는 메모리 사용률을 주기적으로 수집하고, 사용률 구간에 따라 캐릭터 상태를
lying / sitting / walking / running으로 전환합니다.

단순 수치 표시가 아니라 상태 기반 시각화를 적용했으며,
추후 CPU/GPU/프로세스/파일 이벤트/네트워크 이벤트 감시 기능으로 확장 가능하도록
모니터링 계층과 UI 계층을 분리했습니다.
```

강조할 기술 요소:

- C# / WPF
- Windows desktop overlay
- Topmost window
- System resource monitoring
- P/Invoke
- State machine
- Timer/event-driven programming
- UI animation
- MVVM 구조
- 확장 가능한 모니터링 아키텍처

---

## 17. 1차 개발 우선순위

```text
P0: WPF 창 생성
P0: 화면 최상단 고정
P0: 창 크기 조정
P0: 메모리 사용률 조회
P0: 메모리 사용률 텍스트 표시
P0: 상태 4단계 분류
P1: 상태별 이미지 변경
P1: PNG 프레임 애니메이션
P1: 창 위치/크기 저장
P2: 투명 배경
P2: 프레임 없는 창
P2: 트레이 아이콘
P2: 항상 위 on/off
P3: MVP 이후 확장 후보는 PcMate_EXPANSION_PLAN.md 참고
```

---

## 18. 예상 개발 순서

```text
Day 1
- WPF 프로젝트 생성
- 솔루션명 PcMate 설정
- MainWindow 기본 UI 구성
- Topmost, ResizeMode 적용

Day 2
- IResourceMonitor 인터페이스 생성
- MemoryMonitor 구현
- 메모리 사용률 표시
- DispatcherTimer 적용

Day 3
- CharacterState enum 구현
- ResourceSnapshot 구현
- StateClassifier 구현
- 상태 텍스트 표시

Day 4
- 상태별 임시 이미지 적용
- 이미지 전환 로직 구현

Day 5
- PNG 프레임 애니메이션 구현
- 상태별 프레임 속도 조정

Day 6
- 창 위치/크기 저장
- 종료 버튼 또는 컨텍스트 메뉴 추가
- README 정리

Day 7
- 코드 정리
- 영상/GIF 데모 제작
- 포트폴리오 설명 작성
```

---

## 19. 최종 한 줄 정의

**PcMate는 Windows PC의 리소스 상태를 캐릭터 행동으로 시각화하는 데스크탑 오버레이 기반 시스템 모니터링 앱이다.**
