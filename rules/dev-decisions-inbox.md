# Dev Decisions Inbox

| ID | Date | One-liner | Section | Status |
|---|---|---|---|---|
| D-018 | 2026-09-27 | 마크업은 캡처 편집기 다수 관례를 따른다: 그리기 도구 드래그=새로 그림, 클릭=선택, 이동=객체 선택(V), 핸들 8개, 텍스트 더블클릭 편집, Esc 단계 해제, 단일 키 단축키, 아이콘 라벨. | UX | integrated |
| D-017 | 2026-09-27 | 모자이크는 모자이크 도구 드래그로 만드는 비파괴 편집 객체(재선택·이동·크기·종류 변경, 저장 시 합성)이며, 블록 크기는 이미지 픽셀 기준이다(영역 선택→적용 흐름은 같은 날 폐기). | UX | integrated |
| D-016 | 2026-09-27 | 다국어 UI는 `.resx`(프로젝트별 독립, `Strings.Designer.cs` 수기 작성) + 재시작 적용 방식으로 하고, `SettingsWindow`부터 슬라이스로 진행한다. | Architecture | integrated |
| D-015 | 2026-08-28 | 창 제외 프로세스 선택은 실행 파일 경로와 프로세스명을 함께 저장하며, 적용 기준은 값을 삭제·자동 전환하지 않는 라디오 토글로만 바꾼다. | UX | integrated |
| D-014 | 2026-08-06 | ViewerWindow.xaml.cs split into 13 partial files by responsibility; fields stay in anchor; internal static test-exposed helpers keep accessibility. | Architecture | integrated |
| D-013 | 2026-08-06 | Mosaic tool: 2 types (block/Gaussian blur) + adjustable block-size slider, not a fixed constant. | UX | integrated |
| D-009 | 2026-07-02 | Manual Windows validation gates overlay/capture behavior. | Validation | integrated |
| D-008 | 2026-07-02 | V1 excludes OCR/upload/scrolling/video/heavy editing/general file management. | Scope | integrated |
| D-007 | 2026-07-02 | Browser/viewer/capture services remain separated by responsibility. | Architecture | integrated |
| D-006 | 2026-07-02 | First stack target is C# WPF plus Win32 capture APIs. | Architecture | integrated |
| D-005 | 2026-07-02 | Icon views support small, medium, and large thumbnails. | UX | integrated |
| D-004 | 2026-07-02 | File browser follows Windows Explorer conventions. | UX | integrated |

Oldest rows archived to `memory/archive-decisions-2026-Q3.md` (Step 7 threshold, >10 items): D-001, D-002, D-003.
