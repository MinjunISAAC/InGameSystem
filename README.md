# InGameSystem

디펜스 장르 게임의 인게임 루프를 **StateMachine 패턴**으로 관리하는 시스템.  
상태 전이·웨이브 진행·배속 제어·코인 관리를 하나의 매니저에서 일관되게 처리합니다.

## 상태 흐름

```
Ready → Play ⇄ Pause
             ⇄ Upgrade
           → Win | Fail
```

| 상태 | 진입 조건 | 동작 |
|------|-----------|------|
| Ready | Init() 호출 | 스테이지 초기화 대기 |
| Play | 게임 시작 / Pause·Upgrade 복귀 | PlayTime 누적, 웨이브 자동 진행 |
| Pause | 일시정지 버튼 | PlayTimeScale 0 저장 후 복원 대기 |
| Upgrade | 업그레이드 화면 | PlayTimeScale 0 저장 후 복원 대기 |
| Win | 마지막 웨이브 클리어 | PlayTimeScale 0, 결과 처리 |
| Fail | 성 체력 0 | PlayTimeScale 0, 결과 처리 |

## 핵심 설계 원칙

| 항목 | 내용 |
|------|------|
| **StateMachine** | `ChangeState()` 호출 시 이전 Coroutine 중단 → 해당 상태의 `Co_*` Coroutine 실행 |
| **웨이브 자동 진행** | `PlayTime`과 `WaveData.StartTime`을 매 프레임 비교, 조건 충족 시 자동 전환 |
| **배속 제어** | `PlayTimeScale`로 적 스폰 딜레이·PlayTime 누적 모두 제어, Pause/Upgrade 진입 시 배속값 저장 후 복귀 시 복원 |
| **이벤트 기반 UI** | `OnChangeWave`, `OnChangeCoin`, `OnChangeTimeScale` 등 `Action` 이벤트로 UI와 **완전 분리** |

## 사용법

```csharp
// 1. 웨이브 테이블 설정
var waveTable = new List<WaveData>
{
    new WaveData { Wave = 1, StartTime = 0f,  Duration = 30f, SpawnDelay = 0.5f, EnemyIds = new(){101, 101, 102} },
    new WaveData { Wave = 2, StartTime = 35f, Duration = 30f, SpawnDelay = 0.4f, EnemyIds = new(){102, 103} },
};
inGameSystem.SetWaveTable(waveTable);

// 2. UI 이벤트 연결
inGameSystem.OnChangeWave  += wave  => waveLabel.text  = $"Wave {wave}";
inGameSystem.OnChangeCoin  += coin  => coinLabel.text  = coin.ToString();
inGameSystem.OnEnemySpawned+= id   => SpawnEnemy(id);

// 3. 게임 시작
inGameSystem.Init();
inGameSystem.ChangeState(EInGameState.Play);

// 4. 상태 전환 예시
inGameSystem.ChangeState(EInGameState.Pause);    // 일시정지
inGameSystem.ChangeState(EInGameState.Play);     // 재개 (배속 자동 복원)
inGameSystem.ChangeState(EInGameState.Upgrade);  // 업그레이드 화면
```

## 파일 구조

```
Scripts/
├── InGameSystem.cs    # 메인 매니저 (StateMachine + 웨이브 + 코인)
├── EInGameState.cs    # 상태 Enum
└── WaveData.cs        # 웨이브 데이터 구조체
```

## 확장 방법

- 적 스폰: `OnEnemySpawned` 이벤트 구독 → `enemyId` 기반으로 프리팹 Instantiate
- 성 체력: `HitCastle(damage)` 메서드를 추가하고 체력 0 시 `ChangeState(EInGameState.Fail)` 호출
- 보스: `WaveData`에 `BossId` 필드 추가 후 `Co_SpawnEnemies` 내에서 별도 이벤트 발행
