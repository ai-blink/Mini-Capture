# Mini Capture Build Plan

## 1. 목표

무엇을 끝내야 하는가:

- 승인된 Mini Capture 설계를 C# WPF 기반 구현으로 착수할 수 있는 build-ready 계약으로 고정한다.

완료 기준:

- P0-P4 구현 순서와 검증 기준이 정해져 있다.
- 앱 구현 전 범위와 제외 범위가 명확하다.
- `CLAUDE.md`, `doc/app-dev/`, `rules/dev-*`가 현재 설계와 일치한다.

## 2. 범위

이번에 할 것:

- 문서/계획 초기화.
- WPF + Win32 capture API 기준의 구현 순서 확정.
- 캡처, 저장, 뷰어, 탐색기 모듈 경계 확정.

이번에 하지 않을 것:

- 앱 소스 스캐폴딩.
- 런타임 구현.
- OCR, 업로드, 스크롤 캡처, 영상, 무거운 편집기, 범용 파일 관리자.

## 3. 접근

큰 단계:

1. Project skeleton and floating button.
2. Basic drag/full/timer capture and PNG save.
3. Window picker and capture overlay hardening.
4. Viewer and Windows Explorer-style mini browser.
5. DPI, multi-monitor, repeated manual tests, packaging.

중요한 판단 기준:

- Windows-native behavior and WPF patterns first.
- Keep capture, save, viewer, file index, folder tree, and thumbnails separate.
- Load file lists before thumbnails.
- Use fallback capture APIs only where the modern capture path fails.

## 4. 검증

통과해야 할 확인:

- `dotnet build` after implementation begins.
- Manual repeated capture tests for drag/full/timer/window modes.
- Save result actions: open file, open folder, open viewer.
- Viewer checks: zoom, fit, 100%, previous/next.
- Explorer checks: tree navigation, details/small/medium/large views, 500 image responsiveness.

## 5. 남는 리스크

주의할 점:

- Windows.Graphics.Capture behavior varies by OS/app/window type.
- DPI and multi-monitor coordinates can break overlay alignment.
- Thumbnail generation can block UI if not lazy/cancellable.

후속으로 넘길 것:

- Packaging choice.
- Global hotkey policy.
- Any advanced editing request as V2 design.
