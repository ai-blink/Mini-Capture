# 03 Technical Plan

## 1. 목표

무엇을 끝내야 하는가:

- 승인된 Mini Capture UX를 C# WPF 기반 Windows 앱으로 구현할 수 있는 P0-P3 개발 계약을 확정한다.

완료 기준:

- 파일 소유권, 구현 순서, 검증 방식이 명확하다.
- 구현 전 해야 할 것과 하지 않을 것이 분리되어 있다.
- P0부터 시작해도 설계 방향이 흔들리지 않는다.

## 2. 범위

이번에 할 것:

- WPF + Win32 API 기반 앱 구조를 기준으로 계획한다.
- 캡처 버튼, 캡처 모드, PNG 저장, 뷰어, 탐색기 모듈 경계를 정한다.
- P0-P3 구현 순서와 검증 기준을 정한다.

이번에 하지 않을 것:

- 앱 소스 스캐폴딩 또는 런타임 구현.
- OCR, 업로드, 스크롤 캡처, 동영상, 무거운 편집기.
- 범용 파일 관리자 기능.

## 3. 접근

큰 단계:

1. Project skeleton: WPF 앱, 기본 창 수명주기, 설정 저장 위치, 로깅 최소면을 만든다.
2. Capture entry: 투명 topmost floating button과 4개 모드 메뉴를 만든다.
3. Capture/save path: drag/full/timer부터 PNG 저장과 결과 패널을 닫는다.
4. Window capture: HWND picking, DWM bounds highlight, capture fallback 판단을 붙인다.
5. Viewer/browser: 이미지 뷰어와 Windows Explorer-style 트리/파일 영역을 구현한다.
6. Hardening/package: DPI, multi-monitor, overlay exclusion, repeated manual tests, packaging을 검증한다.

중요한 판단 기준:

- 기존 Windows 네이티브 API와 WPF 패턴을 우선한다.
- CaptureEngine, SaveService, Viewer, Explorer 책임을 섞지 않는다.
- 썸네일은 지연 로딩하고 파일 목록 표시를 먼저 완료한다.
- Windows.Graphics.Capture가 막히는 경우에만 BitBlt/PrintWindow fallback을 쓴다.

## 4. 검증

통과해야 할 확인:

- `dotnet build` 또는 선택된 .NET 빌드 명령.
- 드래그/전체/타이머 캡처 20회 반복 수동 테스트.
- 창 지정 캡처 후보 highlight와 click capture 수동 테스트.
- 저장 실패, 폴더 없음, 파일 삭제, 파일명 충돌 처리 확인.
- 뷰어 좌우 이동, zoom/fit/100%, 탐색기 보기 전환 확인.

## 5. 남는 리스크

주의할 점:

- Windows.Graphics.Capture 권한/OS 차이와 특정 앱 캡처 실패.
- DPI와 multi-monitor 좌표 변환.
- overlay/floating button이 캡처 결과에 찍히는 문제.
- 많은 파일에서 썸네일 생성이 UI를 막는 문제.

후속으로 넘길 것:

- 설치 패키징 방식.
- 전역 단축키 정책.
- 고급 편집 기능 요청이 생길 경우 별도 V2 설계.

## 6. 구현 상태

- P0 complete: WPF skeleton, floating capture button, four-mode menu, clean exit.
- P1 complete: drag/full/timer capture, dated PNG auto-save, result panel actions.
- P2 complete: window picker overlay, DWM bounds highlight, selected-window PNG capture, Esc cancel.
- P3 complete: mini viewer and Windows Explorer-style browser first usable slice.

현재 캡처 경로는 GDI `CopyFromScreen` 기반이다. Windows.Graphics.Capture는 protected/accelerated window 실패 사례가 확인될 때 hardening/fallback으로 붙인다.

## Finish Line Contract

- Finish Line: P0 구현을 시작할 수 있는 build-ready plan과 project docs가 준비된다.
- Acceptance Checks: UX plan validator, git diff check, secret scan 또는 대체 스캔이 통과한다.
- Scope Limit: docs/planning only. No app source scaffold in this init.
- Review Budget: 최대 2회 수정 루프.
- Stop Rule: docs 검증 통과 후 구현으로 넘어가기 전 멈춘다.
