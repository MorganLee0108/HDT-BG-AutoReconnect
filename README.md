# HDT-BG-AutoReconnect

**Skip every Battlegrounds combat automatically.** A [Hearthstone Deck Tracker](https://github.com/HearthSim/Hearthstone-Deck-Tracker) (HDT) plugin that skips combat animations by reconnecting, with an **auto toggle** that does it for you the moment each combat starts. No more clicking a button every turn.

**전장 전투를 자동으로 스킵합니다.** 재접속으로 전투 애니메이션을 건너뛰는 HDT 플러그인입니다. **자동 스위치**를 켜두면 전투가 시작될 때마다 알아서 재접속하므로 매 턴 버튼을 누를 필요가 없습니다.

[English](#english) · [한국어](#한국어) · [Download / 다운로드](https://github.com/MorganLee0108/HDT-BG-AutoReconnect/releases/latest)

```
[ 재접속 ]  자동 ○━━ OFF      ← manual: click the button to skip / 버튼을 누를 때만 스킵
[ 재접속 ]  자동 ━━● ON       ← auto: skips every combat as it starts / 전투마다 자동 스킵
```

### Why this one? / 다른 재접속 플러그인과 다른 점

| | Typical reconnect plugins / 일반 재접속 플러그인 | **HDT-BG-AutoReconnect** |
|---|---|---|
| Manual reconnect button / 수동 버튼 | ✅ | ✅ |
| **Auto-skip every combat / 전투마다 자동 스킵** | ❌ (click every turn / 매 턴 클릭) | ✅ **one toggle / 스위치 하나** |
| Loop protection / 반복 끊김 방지 | – | ✅ once per turn / 턴당 1회 |
| Starts OFF on every launch / 실행 시 항상 OFF | – | ✅ |
| Network access / 외부 서버 통신 | varies / 제각각 | ❌ none / 없음 |

---

## English

### Features

- **Reconnect button**: shown at the bottom right during Battlegrounds matches. Clicking it drops Hearthstone's game-server connection; the client immediately reconnects and skips the combat.
- **Auto toggle**
  - **ON**: reconnects automatically the instant combat starts (no delay). Combat is skipped every turn without you touching anything.
  - **OFF**: does nothing automatically. The manual button keeps working.
  - Always **starts OFF** when HDT launches, so it never disconnects you unexpectedly.
- **Loop protection**: auto-reconnect fires at most once per turn of the same game, and only re-arms after the recruit phase has been observed for 5+ seconds. The game state replayed after a reconnect can't trigger a disconnect/reconnect loop.
- The button shows **실패** (failed) on failure and **(관리자 권한 필요)** (administrator required) when HDT isn't elevated.

> **The UI is in Korean.** 재접속 = Reconnect, 자동 = Auto, 실패 = Failed, 연결 끊는 중… = Disconnecting…

### How it works

When the client reconnects while the server is in the combat phase, it jumps straight past the combat animation. The combat result is decided on the server beforehand, so only what you see changes. The timing is forgiving: the reconnect just has to land during combat.

- **Combat-start detection**: uses HDT's combat-phase state (`GameV2.IsBattlegroundsCombatPhase`), which HDT derives from the game log. The moment it flips from false to true is the start of combat.
- **Disconnect**: uses the Windows `SetTcpEntry` API on a single TCP connection owned by the Hearthstone process. That's the game-server address found in `Hearthstone.log`, or Hearthstone's most recent connection if it isn't found. Battle.net and other programs are never touched.
- **No network access**: the plugin talks to no external server. It only reads Hearthstone's log file and HDT's game state.

### Install

1. Download `HDT-BG-AutoReconnect-v*.zip` from [Releases](https://github.com/MorganLee0108/HDT-BG-AutoReconnect/releases/latest) and unzip it.
2. Put `HDT_BG_AutoReconnect.dll` into the HDT plugins folder, `%AppData%\HearthstoneDeckTracker\Plugins\`. You can also open it from HDT via **Options → Tracker → Plugins → Plugins Folder**.
   - If Windows marks the file as blocked, right-click the DLL → **Properties → Unblock**.
3. **Run HDT as administrator.** The Windows API that drops the connection requires elevation.
4. Enable **전장 자동 재접속 (BG AutoReconnect)** in **Options → Tracker → Plugins**.

> If you also use the original [HDT_Reconnector](https://github.com/qxplays/HDT_Reconnector) (BGMatchHelper), enable only one of them, or you'll get two reconnect buttons.

### Caveats

- Deliberately dropping the connection is a **gray area under Blizzard's terms of service**. Use at your own risk.
- On a bad network the reconnect can take longer and cut into your recruit time.
- This is not an official HearthSim/HDT plugin.

### Build

Requirements: the [.NET SDK](https://dotnet.microsoft.com/download) (8 or newer) and an installed HDT. The build references HDT's `HearthstoneDeckTracker.exe`.

```powershell
.\build.ps1
# if HDT isn't in the default location (%LOCALAPPDATA%\HearthstoneDeckTracker\current):
.\build.ps1 -HdtDir "D:\HDT\current"
```

The DLL is written to `src\HDT_BG_AutoReconnect\bin\Release\net472\` and the release zip to `dist\`.

---

## 한국어

### 기능

- **재접속 버튼**: 전장 경기 중 화면 오른쪽 아래에 표시됩니다. 누르면 하스스톤의 게임 서버 연결 하나만 끊고, 클라이언트가 바로 다시 접속하면서 전투를 건너뜁니다.
- **자동 스위치**
  - **ON**: 전투가 시작되는 순간 지연 없이 자동으로 재접속합니다. 아무것도 누르지 않아도 매 턴 전투가 스킵됩니다.
  - **OFF**: 자동으로는 아무것도 하지 않습니다. 수동 버튼은 그대로 쓸 수 있습니다.
  - HDT를 켤 때마다 **항상 OFF로 시작**합니다. 모르는 사이에 자동으로 끊기는 일을 막기 위해서입니다.
- **반복 방지**: 같은 게임의 같은 턴에서는 한 번만 자동으로 끊습니다. 재접속한 뒤에는 영입 단계가 5초 이상 이어져야 다시 자동 재접속이 가능해집니다. 재접속 직후 게임 상태가 다시 들어오면서 끊고 붙기를 반복하는 일을 막기 위해서입니다.
- 실패하면 버튼에 "실패"가 표시됩니다. 관리자 권한이 없으면 "(관리자 권한 필요)"가 표시됩니다.

### 원리

다시 접속했을 때 서버가 전투 중이면 클라이언트가 전투 애니메이션을 보여주지 않고 결과로 바로 넘어갑니다. 전투 결과는 서버에서 이미 정해져 있어서, 이 방법으로 바뀌는 것은 보이는 화면뿐입니다. 재접속이 전투 중에만 이뤄지면 되므로 끊는 시점은 크게 중요하지 않습니다.

- **전투 시작 감지**: HDT가 게임 로그로 판단하는 전투 단계 값(`GameV2.IsBattlegroundsCombatPhase`)을 씁니다. 이 값이 꺼짐에서 켜짐으로 바뀌는 순간이 전투 시작입니다.
- **연결 끊기**: Windows `SetTcpEntry` API로 하스스톤 프로세스의 TCP 연결 하나만 끊습니다. 하스스톤 로그(`Hearthstone.log`)에서 찾은 게임 서버 주소의 연결을 끊고, 찾지 못하면 하스스톤의 가장 최근 연결을 끊습니다. 배틀넷과 다른 프로그램의 연결은 건드리지 않습니다.
- **외부 통신 없음**: 플러그인은 외부 서버와 통신하지 않습니다. 읽는 것은 하스스톤 로그 파일과 HDT의 게임 상태뿐입니다.

### 설치

1. [Releases](https://github.com/MorganLee0108/HDT-BG-AutoReconnect/releases/latest)에서 `HDT-BG-AutoReconnect-v*.zip`을 받아 압축을 풉니다.
2. `HDT_BG_AutoReconnect.dll`을 HDT 플러그인 폴더에 넣습니다. 폴더 위치는 `%AppData%\HearthstoneDeckTracker\Plugins\`이고, HDT의 **Options → Tracker → Plugins → Plugins Folder**로도 열 수 있습니다.
   - 파일이 "차단됨"으로 표시되면 DLL을 우클릭하고 **속성 → 차단 해제**를 누릅니다.
3. **HDT를 관리자 권한으로 실행합니다.** 연결을 끊는 Windows API가 관리자 권한을 요구합니다.
4. **Options → Tracker → Plugins**에서 **전장 자동 재접속 (BG AutoReconnect)**을 체크해서 켭니다.

> 원본 [HDT_Reconnector](https://github.com/qxplays/HDT_Reconnector)(BGMatchHelper)를 쓰고 있었다면 둘 중 하나만 켜두세요. 둘 다 켜면 재접속 버튼이 두 개 생깁니다.

### 주의

- 연결을 일부러 끊는 방식이라 **블리자드 약관상 회색지대**입니다. 사용은 본인 책임입니다.
- 네트워크 상태가 나쁘면 재접속이 늦어져 영입 시간을 손해 볼 수 있습니다.
- HearthSim/HDT 공식 플러그인이 아닙니다.

### 빌드

요구사항은 [.NET SDK](https://dotnet.microsoft.com/download)(8 이상)와 설치된 HDT입니다. HDT의 `HearthstoneDeckTracker.exe`를 참조해서 빌드합니다.

```powershell
.\build.ps1
# HDT가 기본 위치(%LOCALAPPDATA%\HearthstoneDeckTracker\current)가 아니면:
.\build.ps1 -HdtDir "D:\HDT\current"
```

결과물은 `src\HDT_BG_AutoReconnect\bin\Release\net472\HDT_BG_AutoReconnect.dll`에 생기고, 배포용 zip은 `dist\`에 생깁니다.

---

## Credits / License

- Based on the BGMatchHelper code from [qxplays/HDT_Reconnector](https://github.com/qxplays/HDT_Reconnector) (MIT): connection dropping, log watching, overlay registration. / [qxplays/HDT_Reconnector](https://github.com/qxplays/HDT_Reconnector)(MIT)의 BGMatchHelper 코드를 기반으로 합니다. 연결 끊기, 로그 감시, 오버레이 등록 코드가 여기서 왔습니다.
- Added: the auto-reconnect toggle, combat-start detection with loop protection, and the Korean UI. / 추가한 것은 자동 재접속 스위치, 전투 시작 감지와 반복 방지, 한국어 UI입니다.
- [MIT License](LICENSE)
