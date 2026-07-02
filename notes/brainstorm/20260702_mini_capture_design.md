# Mini Capture 브레인스토밍 설계 초안

## 1. 한 줄 목표

PicPick/ShareX의 큰 기능 세트를 대체하는 것이 아니라, Windows에서 항상 떠 있는 투명 캡처 버튼 하나로 드래그 캡처, 창 지정 캡처, 전체 화면 캡처, 타이머 캡처를 빠르게 실행하고 PNG를 지정 폴더에 자동 저장한 뒤, 심플 이미지 뷰어와 Windows 탐색기 스타일의 트리형 미니 파일 탐색기로 방금 저장한 캡처와 저장 폴더를 빠르게 확인하는 경량 앱을 만든다.

## 2. 문제

현재 PicPick은 기능은 좋지만 오류가 잦고, ShareX는 강력하지만 사용자가 원하는 범위보다 무겁다. 문제는 "캡처 기능 부족"이 아니라, 자주 쓰는 캡처 동선이 앱 무게와 오류에 끌려다니는 것이다.

증거/가정:

- 사용자는 투명한 항상 위 버튼, 4개 캡처 모드, PNG 자동 저장, 저장 폴더 열기, 단순 이미지 뷰어를 핵심으로 제시했고, 이후 이미지 뷰어와 Windows 탐색기식 파일 탐색기까지 계획서에 포함하기를 요청했다.
- PicPick/ShareX 수준의 편집, 업로드, OCR, 스크롤 캡처, 영상 녹화는 이번 목표가 아니다.
- Windows 데스크톱 앱 기준으로 본다.

## 3. 사용자

대표 사용자는 작업 중 화면을 자주 저장하고, 캡처 후 파일 위치와 방금 저장된 이미지를 빠르게 확인해야 하는 Windows 사용자다. 앱은 항상 떠 있지만 방해되지 않아야 하고, 클릭 한 번으로 캡처 모드가 펼쳐져야 한다. 저장은 PNG 중심이며, 기본 경로는 설정 가능해야 한다. 캡처 후에는 외부 파일 탐색기를 매번 열지 않고도 트리형 폴더 탐색, 최근 캡처 목록, 썸네일 아이콘 보기, 파일명, 폴더 위치를 앱 안에서 빠르게 훑어볼 수 있어야 한다.

주요 제약:

- 빠른 시작과 낮은 상주 메모리 사용이 중요하다.
- Electron 계열은 경량 목표와 맞지 않는다.
- 창 지정 캡처는 커서 아래 창 감지, 테두리 강조, 클릭 확정 흐름이 필요하다.
- 플로팅 버튼이나 오버레이가 결과 이미지에 찍히지 않도록 처리해야 한다.
- 파일 탐색기는 Windows Explorer 전체 대체가 아니라 캡처 저장 폴더 중심의 가벼운 브라우저여야 한다. 단, UI 모델은 Windows 탐색기의 주소줄, 검색 박스, 왼쪽 트리, 오른쪽 파일 영역을 따른다.

## 4. 사용자 여정

핵심 여정:

1. 앱 실행 후 작은 투명 캡처 버튼이 화면 한쪽에 항상 위로 떠 있다.
2. 사용자가 버튼을 클릭하면 드래그, 창, 전체화면, 타이머 모드가 아이콘 메뉴로 펼쳐진다.
3. 사용자가 모드를 선택한다.
4. 캡처가 완료되면 PNG가 지정 폴더에 자동 저장된다.
5. 토스트/미니 패널에서 저장 성공, 파일 열기, 폴더 열기, 뷰어 열기를 제공한다.
6. 뷰어에서는 확대/축소, fit/100%, 좌우 화살표로 같은 폴더 이미지를 넘긴다.
7. 파일 탐색기 패널에서는 왼쪽 트리형 폴더 네비게이션으로 저장 폴더를 찾고, 오른쪽 파일 영역에서 자세히 보기와 작은/중간/큰 썸네일 아이콘 보기를 전환한다.
8. 선택한 파일은 중앙 뷰어에서 바로 열고, 필요하면 삭제, 이름 변경, 외부 폴더 열기까지 처리한다.

이번 설계가 해결해야 하는 한 가지 막힘은 "가벼운 캡처 동선"이다. 주석 편집이나 공유 파이프라인은 막힘이 아니다.

## 5. 핵심 흐름

드래그 캡처:

- 투명 오버레이 표시
- 마우스 드래그로 사각형 선택
- `Esc` 취소, 마우스 업 확정
- 선택 영역 PNG 저장

창 지정 캡처:

- 커서 이동 중 `WindowFromPoint`로 HWND 후보 찾기
- `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`로 보이는 창 경계 확보
- 얇은 테두리 오버레이로 후보 강조
- 클릭 시 해당 창 캡처

전체 화면 캡처:

- 모든 모니터를 포함한 virtual desktop 또는 현재 모니터 중 정책 선택
- MVP 기본값은 모든 모니터 1장 PNG

타이머 캡처:

- 0 / 3 / 5 / 10초
- 타이머 종료 후 마지막 선택 모드 또는 전체화면 캡처
- 드롭다운/툴팁 캡처를 위해 카운트다운 중 UI를 최소화한다.

이미지 뷰어:

- 캡처 직후 마지막 저장 파일을 즉시 연다.
- 마우스 휠 또는 `+`/`-`로 확대/축소한다.
- `Fit`과 `100%`를 빠르게 전환한다.
- 좌우 화살표로 같은 폴더의 이전/다음 이미지를 넘긴다.
- 큰 이미지는 비동기 로딩하고, 현재 이미지 주변 파일만 미리 읽어 초기 반응성을 유지한다.

미니 파일 탐색기:

- 기본 시작 위치는 캡처 저장 폴더다.
- Windows 탐색기처럼 주소줄, 검색 박스, 왼쪽 트리형 폴더 네비게이션, 오른쪽 파일 영역으로 구성한다.
- 왼쪽 탐색기는 `빠른 접근 > 내 PC > 사진 > MiniCapture > 연도 > 월 > 일`처럼 접힘/펼침 가능한 트리형 구조를 따른다.
- 오른쪽 파일 영역은 자세히 보기와 이미지 썸네일 아이콘 보기를 전환할 수 있다.
- 썸네일 아이콘 보기는 작게, 중간, 크게 3가지 크기를 제공한다.
- 최근순, 이름순, 크기순 정렬을 제공한다.
- 파일 열기, 이름 변경, 삭제, 외부 폴더 열기를 제공한다.
- MVP에서는 복사/이동/태그/검색 인덱싱 같은 범용 파일 관리자 기능은 제외한다.

실패/재진입:

- 캡처 실패 시 사용자에게 원인과 재시도 버튼을 보여준다.
- 저장 실패 시 폴더 권한 또는 경로 오류를 보여주고 다른 폴더 선택으로 빠져나간다.
- 취소 시 플로팅 버튼 상태로 즉시 복귀한다.
- 뷰어/탐색기에서 파일이 사라진 경우 목록을 갱신하고 다음 사용 가능한 이미지로 이동한다.

## 6. 화면 / 출력 의도

Floating Button:

- 핵심 액션: 클릭해서 모드 메뉴 열기
- 핵심 출력: 방해되지 않는 항상 위 캡처 진입점

Capture Menu:

- 핵심 액션: 드래그/창/전체/타이머 선택
- 핵심 출력: 모드 선택 후 즉시 캡처 상태 전환

Region Overlay:

- 핵심 액션: 영역 드래그
- 핵심 출력: 선택 영역 크기와 캡처 확정

Window Picker Overlay:

- 핵심 액션: 창 위에 커서 올리고 클릭
- 핵심 출력: 선택 후보 테두리 강조와 HWND 확정

Save Result Mini Panel:

- 핵심 액션: 파일 열기, 폴더 열기, 뷰어 열기
- 핵심 출력: 저장된 PNG 경로 확인

Mini Viewer:

- 핵심 액션: 확대/축소, fit/100%, 좌우 이동
- 핵심 출력: 현재 폴더의 PNG/JPG를 빠르게 확인

Mini File Explorer:

- 핵심 액션: 왼쪽 트리에서 저장 폴더를 고르고, 오른쪽 파일 영역에서 자세히/작게/중간/크게 보기를 전환하며 캡처 파일을 선택
- 핵심 출력: 선택 파일을 뷰어로 열거나 외부 폴더에서 위치 확인

Viewer + Explorer Combined Layout:

- 핵심 액션: 왼쪽 슬라이드 탐색기에서 트리형 폴더와 파일 목록을 다루고, 중앙 뷰어에서 이미지를 확인
- 핵심 출력: 캡처 후 "저장됨 → 확인 → 정리" 동선이 앱 안에서 끝남

## 7. 우선순위

Must:

- 투명/항상 위 캡처 버튼
- 4개 캡처 모드: 드래그, 창 지정, 전체 화면, 타이머
- PNG 자동 저장
- 저장 폴더 열기
- 심플 이미지 뷰어: 빠른 열기, 확대/축소, fit/100%, 좌우 이동
- 미니 파일 탐색기: Windows 탐색기식 왼쪽 트리, 주소줄/검색, 자세히 보기, 작게/중간/크게 썸네일 아이콘 보기, 정렬, 파일 열기, 외부 폴더 열기

Should:

- 전역 단축키
- 최근 캡처 열기
- 플로팅 버튼 위치 기억
- 멀티 모니터 DPI 대응
- 파일 삭제와 이름 변경
- 뷰어와 탐색기 사이의 선택 상태 동기화
- 트리 노드 접기/펼치기 상태와 보기 모드 기억

Not Doing:

- 안 하는 것(2개): OCR; 업로드/클라우드 공유; 스크롤 캡처; 영상 녹화/GIF; 강한 이미지 편집기; 범용 파일 관리자

- OCR: MVP 경량성과 충돌하고 후처리 파이프라인이 커진다.
- 업로드/클라우드 공유: ShareX식 기능 확장으로 번지기 쉽다.
- 스크롤 캡처: 안정성 난도가 높고 초기 목표의 "빠른 PNG 저장"과 다르다.
- 영상 녹화/GIF: 캡처 앱 범위를 벗어나 메모리와 UI 복잡도를 키운다.
- 강한 이미지 편집기: FastStone/PicPick의 전체 편집 기능을 복제하지 않는다.
- 범용 파일 관리자: 드라이브 트리, 압축, 네트워크 위치, 복잡한 복사/이동 큐까지 다루면 경량 캡처 앱의 중심이 흐려진다.

## 8. 성공 지표

- 앱 실행 후 플로팅 버튼 표시까지 체감 1초 이내를 목표로 한다.
- 드래그/전체화면 캡처 후 PNG 파일 생성까지 체감 1초 이내를 목표로 한다.
- 4개 모드 각각이 최소 20회 반복 사용에서 앱 재시작 없이 동작한다.
- 저장 폴더 열기와 뷰어 열기가 캡처 직후 1클릭으로 가능하다.
- 캡처 저장 폴더의 최근 500개 이미지 목록이 체감 지연 없이 표시된다.
- 뷰어에서 좌우 이동 시 다음 이미지가 0.3초 이내에 표시되는 것을 목표로 한다.
- 작게/중간/크게 썸네일 아이콘 보기 전환이 즉시 반영되고 스크롤 위치가 불필요하게 튀지 않는다.
- 기본 상주 메모리는 경량 데스크톱 앱 수준을 목표로 하며, Electron 방식은 제외한다.

## 9. 검증 계획

측정자:

- 개발자가 로컬 Windows 환경에서 반복 캡처 테스트를 수행한다.

언제:

- MVP 구현 직후
- 창 지정 캡처 구현 직후
- 뷰어 구현 직후

방법:

- 드래그, 창 지정, 전체화면, 타이머 각각 수동 반복 테스트
- 멀티 모니터와 DPI 배율이 다른 화면에서 좌표 검증
- 저장 경로 없는 경우, 권한 없는 경우, 파일명 충돌 경우 확인
- 뷰어에서 큰 PNG, 작은 PNG, 같은 폴더 여러 이미지 좌우 이동 확인
- 파일 탐색기에서 10개, 500개, 2,000개 이미지가 있는 폴더를 열어 목록 로딩과 스크롤 반응 확인
- 뷰어/탐색기 동기화: 목록 선택 → 이미지 표시, 좌우 이동 → 목록 선택 변경 확인
- 트리형 탐색기: 빠른 접근, 내 PC, 사진, MiniCapture, 연/월/일 폴더의 선택과 접기/펼치기 확인
- 보기 전환: 자세히, 작게, 중간, 크게 모드에서 파일명 표시, 선택 상태, 스크롤 동작 확인
- 삭제/이름 변경을 제공하는 경우 실제 파일 시스템 반영과 실패 복구 확인

Plan B:

- Windows.Graphics.Capture 기반 창 캡처가 특정 앱에서 실패하면 BitBlt/PrintWindow fallback을 제한적으로 둔다.
- 썸네일 생성이 느리면 파일 목록을 먼저 표시하고, 썸네일은 지연 로딩한다.

Plan C:

- 창 지정 캡처가 불안정하면 MVP에서는 드래그/전체/타이머를 먼저 닫고 창 지정은 별도 이정표로 분리한다.
- 파일 탐색기가 범위를 키우면 첫 버전에서는 최근 캡처 목록과 외부 폴더 열기만 제공하고, 전체 탐색기는 두 번째 이정표로 분리한다.

## Recommended Architecture

권장 1안은 C# WPF 또는 WinForms + Win32 P/Invoke다. Greenshot/ShareX/Pointframe 같은 C# 계열 참고가 많고, 빠르게 MVP를 만들 수 있다. 플로팅 버튼과 오버레이는 WPF가 편하고, 캡처 핵심은 Win32/Windows.Graphics.Capture API로 붙인다. 단, self-contained 배포 용량은 커질 수 있으므로 런타임 의존 배포 또는 설치형 배포를 검토한다.

권장 모듈:

- FloatingButton: topmost/layered window, 위치 기억, 클릭 메뉴
- CaptureMenu: 4개 모드와 타이머 선택
- RegionCaptureOverlay: 화면 dim + 드래그 사각형
- WindowPickerOverlay: HWND 감지 + 테두리 강조
- CaptureEngine: Windows.Graphics.Capture 우선, BitBlt/PrintWindow fallback
- SaveService: PNG 저장, 파일명 규칙, 경로 검증
- ShellService: 폴더 열기, 파일 선택 열기
- MiniViewer: 이미지 로딩, 확대/축소, fit/100%, 좌우 이동
- CaptureFileIndex: 저장 폴더 스캔, 이미지 확장자 필터, 정렬, 파일 변경 감지
- MiniExplorer: Windows 탐색기식 주소줄/검색/트리/파일 영역, 자세히/작게/중간/크게 보기, 선택 상태, 파일 열기, 이름 변경/삭제 후보
- FolderTreeModel: 빠른 접근, 내 PC, 저장 폴더, 연/월/일 폴더 노드 구성과 접힘/펼침 상태
- ThumbnailService: 지연 썸네일 생성, 캐시, 취소 가능한 로딩

## Option Space

### Option A: C# WPF/WinForms + Win32 API

장점:

- 개발 속도가 빠르다.
- Windows UI와 오버레이 구현이 쉽다.
- Greenshot, ShareX, Pointframe 참고가 바로 맞는다.

단점/리스크:

- .NET 런타임 또는 배포 용량 이슈가 있다.
- 고성능 캡처/뷰어 쪽은 네이티브보다 조심스럽게 최적화해야 한다.

추천도: 높음. 첫 MVP에 가장 현실적이다.

### Option B: C++ Win32 + WIC/Direct2D + Windows.Graphics.Capture

장점:

- 가장 가볍고 실행이 빠르다.
- 작은 exe와 낮은 상주 메모리를 노리기 좋다.
- 이미지 뷰어를 WIC/Direct2D로 직접 최적화할 수 있다.

단점/리스크:

- 개발 난도가 높다.
- 오버레이 UI, 설정, 오류 처리까지 직접 챙겨야 한다.

추천도: 중간. 최종 경량성은 최고지만 첫 구현 비용이 크다.

### Option C: Go + Wails + Native Capture Helper

장점:

- Electron보다 작게 갈 수 있다.
- UI 제작은 빠르다.
- WinShot 같은 참고 레포가 있다.

단점/리스크:

- React/WebView 계층이 들어오면 "진짜 미니 앱" 감각이 약해질 수 있다.
- 창 캡처/오버레이는 결국 native helper 품질에 달린다.

추천도: 보조. UI를 빨리 만들고 싶을 때만 고려한다.

## Reference Apps And Repos

캡처 UX:

- Greenshot: Windows 경량 캡처 앱 기준. 선택 영역, 창, 전체화면, 파일 저장/export 흐름 참고.
- ShareX: 기능은 과하지만 region capture UX와 after-capture task 구조 참고.
- ksnip: 사각 영역, 현재 마우스 모니터, 전체화면, 포커스 창, 커서 아래 창, 딜레이가 요구사항과 많이 겹친다.
- Flameshot: GUI 캡처, 지정 경로 저장, 딜레이 옵션, 오버레이 툴바 흐름 참고.
- PinShot: 타이머 0/3/5/10초, 드래그 선택, 항상 위 핀 이미지 UX 참고.
- Snipaste: 창/요소 감지, 플로팅 이미지, 투명도/클릭 통과 UX 참고. 소스보다 UX 참고용.

이미지 뷰어:

- FastStone Image Viewer: 빠른 전체화면 워크플로우, 확대/축소, fly-out 패널, 포맷 지원의 기준점.
- qView: 도구바 없는 미니멀 뷰어와 빠른 전환, 낮은 CPU/메모리 목표 참고.
- Minimal Image Viewer: Win32/C++ 기반 뷰어. 확대/축소, pan, fit/actual, 이전/다음 이미지 흐름 참고.
- Windows File Explorer: 범용 기능을 따라 하기보다, 저장 폴더 열기와 파일 선택 위치 확인의 기준 동작만 참고.
- FastStone의 폴더/썸네일 브라우저: 이미지 뷰어와 파일 탐색이 한 화면에서 이어지는 UX 참고. 단, 편집/변환/관리 기능 전체는 제외.

구현/API:

- Windows.Graphics.Capture: Windows 10/11의 현대적 화면/창 캡처 API.
- robmikh/Win32CaptureSample: HWND/HMONITOR 기반 GraphicsCaptureItem 생성 참고.
- Microsoft WPF ScreenCapture sample: WPF에서 Windows.Graphics.Capture 사용 참고.
- WindowFromPoint: 커서 아래 창 후보 찾기.
- DwmGetWindowAttribute + DWMWA_EXTENDED_FRAME_BOUNDS: 실제 보이는 창 테두리 좌표 얻기.
- SetLayeredWindowAttributes + SetWindowPos(HWND_TOPMOST): 투명/항상 위 버튼 구현.
- SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE): 플로팅 UI가 캡처 결과에 찍히지 않게 하는 후보.
- RegisterHotKey: 전역 단축키.
- ShellExecute / SHOpenFolderAndSelectItems: 저장 폴더 열기 또는 저장 파일 선택.
- BitBlt / PrintWindow: fallback 후보. 현대 앱/가속 렌더링에서는 한계가 있으므로 보조 수단으로만 둔다.

## Source Links

- Greenshot: https://github.com/greenshot/greenshot
- ShareX region capture: https://getsharex.com/docs/region-capture
- ksnip: https://github.com/ksnip/ksnip
- Flameshot: https://github.com/flameshot-org/flameshot
- WinShot: https://github.com/mrgoonie/winshot
- Pointframe: https://github.com/dimitar-radenkov/Pointframe
- PinShot: https://github.com/jim72-commits/PinShot
- Snipaste: https://www.snipaste.com/
- FastStone Image Viewer: https://www.faststone.org/FSViewerDetail.htm
- qView: https://interversehq.com/qview/
- Minimal Image Viewer: https://github.com/deminimis/minimalimageviewer
- Win32CaptureSample: https://github.com/robmikh/Win32CaptureSample
- Windows.Graphics.Capture: https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture
- WPF ScreenCapture sample: https://github.com/microsoft/Windows.UI.Composition-Win32-Samples/blob/master/dotnet/WPF/ScreenCapture/README.md
- WindowFromPoint: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-windowfrompoint
- DWMWINDOWATTRIBUTE: https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute
- SetLayeredWindowAttributes: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setlayeredwindowattributes
- SetWindowPos: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos
- SetWindowDisplayAffinity: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity
- RegisterHotKey: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey
- ShellExecute: https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shellexecutea
- SHOpenFolderAndSelectItems: https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shopenfolderandselectitems
- Capturing an Image / BitBlt: https://learn.microsoft.com/en-us/windows/win32/gdi/capturing-an-image
- PrintWindow: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-printwindow

## Self Review

- Placeholder scan: 통과. 금지 placeholder 없음.
- Contradiction check: 통과. 경량 앱 목표와 제외 범위가 일치함.
- Scope check: 조건부 통과. 전체 앱은 한 세션 구현 범위를 넘을 수 있으므로 MVP는 캡처 버튼 + 저장 + 기본 뷰어로 쪼개야 함.
- Ambiguity check: 통과. 기술 스택과 전체화면 정책은 사용자 선택 질문으로 남김.
- UX plan shape: 통과. 문제, 성공 지표, Not Doing, 검증 Plan B/C 포함.
