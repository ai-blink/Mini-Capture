# Dev Feedback

## User Preferences

- Use Korean for progress, plans, and final responses.
- Prefer lightweight Windows-native app direction over broad feature suites.
- Keep the capture app focused and avoid PicPick/ShareX-scale expansion.
- File browser should feel like Windows Explorer with a tree-style left panel.

## Working Style

- User is iterating visually; HTML planning mockups are useful for UI shape decisions.
- Commit approved planning artifacts before implementation.
- `창 제외`에서 실행 중인 대상을 고르면, 확인 가능한 실행 파일 경로와 프로세스명을 함께 보존한다. 적용 기준은 라디오 버튼으로만 사용자가 전환하며, 전환 과정에서 저장된 반대 기준 값을 버리거나 자동으로 바꾸지 않는다.
- 편집 기능은 Photoshop 같은 일반 편집기 기준으로 설계한다. 적용한 효과(모자이크 등)도 선택 해제 후 다시 선택해 이동·크기·설정을 고칠 수 있어야 하고, 선택 영역·도형은 드래그로 옮길 수 있어야 한다. 즉시 굽는 방식은 사용자가 거부했다(2026-09-27).
- 편집 UX 규칙이 갈리면 추측으로 정하지 말고 관례 비교 리서치(PicPick·Greenshot·ShareX·Flameshot·ksnip 등, 코드·문서 근거)로 다수결 근거를 대고 선택지를 제시한다. 사용자는 실제 앱을 눌러 보며 빠르게 반복하므로, 매 슬라이스마다 "어떤 버튼을 눌러 무엇을 하는지" 표로 설명하고 툴바에 라벨·툴팁·단축키를 드러낸다(아이콘만으로는 찾지 못했다: 객체 선택 버튼).
- "기능이 사라졌다"는 보고는 커밋 이력만으로 닫지 말고 실행 중인 앱의 실제 표시 위치(UIA 좌표·모니터 경계)까지 확인한다. 코드가 멀쩡해도 창이 화면 밖으로 복원되어 UI가 안 보일 수 있다.
- 설정 화면에서 동일한 행의 버튼은 정상인데 입력 영역의 테두리가 잘리는 문제는 기능 완료로 보지 않는다. 같은 레이아웃 제약을 사용하는 다른 프로젝트 설정 화면에도 원인을 재사용해 확인한다.
