# README 데모 영상 생성

Windows, .NET 9 SDK, FFmpeg(`libx264`, `libwebp_anim`)가 필요함. `ffmpeg`가 PATH에 등록되어 있어야 함.

저장소 루트에서 실행함. 기본값은 한국어·영어·중국어 간체·일본어 전체 생성임.

```powershell
pwsh -NoProfile -File tools/demo/render.ps1

# 영어 버전만 생성함.
pwsh -NoProfile -File tools/demo/render.ps1 -Language en
```

## 결과물

- MP4: 1920×1080, 30fps, 15초, H.264, 무음 영상
- WebP: 960×540, 15fps 입력, 무한 반복 미리보기
- `docs/media/check-<언어>-*.png`: 장면 검수용 스틸, Git 제외 대상
- `docs/media/verification-<언어>.log`: 앱 언어·편집·SQLite 저장·고정·고정 해제 검증 결과, Git 제외 대상

각 언어의 MP4와 WebP를 `docs/media`에 생성함.

| 언어 | 파일명 접두사 | README |
|---|---|---|
| 한국어 | `my-stickies-demo` | `README.md` |
| 영어 | `my-stickies-demo-en` | `README.en.md` |
| 중국어 간체 | `my-stickies-demo-zh-CN` | `README.zh-CN.md` |
| 일본어 | `my-stickies-demo-ja` | `README.ja.md` |

WebP 인코더는 동일한 연속 프레임을 합치므로 최종 프레임 수는 225개보다 적을 수 있음.
README는 반복 WebP를 표시하고 같은 내용의 MP4로 연결함. GitHub README의 HTML 동영상 태그 지원에 의존하지 않는 구성임.

## 제작 방식

`render.ps1`이 앱 소스를 임시 폴더에 복사하고 `MovieDirector.cs`를 추가해 데모 전용 앱을 빌드함.
이 복사본에서만 데이터 경로, 단일 인스턴스 이름, 가상 작업 영역, 시작 동작을 교체함. 언어별로 별도 데이터 폴더를 사용함.
실제 `MainWindow`, `NoteTab`, `FloatingNoteWindow`와 SQLite 저장 로직을 실행하고 WPF 시각 트리를 프레임으로 렌더링함.
실행 중인 일반 앱의 설정·메모 DB와 분리되며, 업데이트 확인·트레이·전역 단축키를 사용하지 않음.

책갈피 → 펼치기 → 본문 편집 및 저장 → 핀 이벤트 → 독립 창 이동 → 고정 해제 순서로 진행함.
동작은 코드로 제어하며, 커서·클릭 강조·자막·가상 배경은 합성 요소임. 실제 마우스 입력을 녹화한 영상은 아님.
마지막 구간은 첫 프레임으로 전환하여 자연스럽게 반복됨.

앱 UI 언어와 예시 메모, 입력 문구, 자막을 언어별로 적용함. 영상 문구는 `locales.json`에서 관리함.
배경은 코드로 그린 도형이며 외부 이미지·음원에 의존하지 않음.
한국어는 Malgun Gothic, 영어는 Segoe UI, 중국어 간체는 Microsoft YaHei UI, 일본어는 Yu Gothic UI를 사용함.
해당 Windows 글꼴이 없으면 렌더링을 중단함. 자막은 번역 길이에 맞춰 지정 영역 안에서 크기를 조절함.
주요 장면은 0초, 3.5초, 6.1초, 10.2초, 마지막 프레임으로 저장됨.
앱 구현이 변경되어 격리용 치환 대상을 찾지 못하면 빌드를 중단함. 렌더링에 실패하면 `render-error-<언어>.log`를 확인함.

## 검증

```powershell
ffprobe -v error -show_entries stream=codec_name,width,height,r_frame_rate,nb_frames:format=duration docs/media/my-stickies-demo.mp4
dotnet test tests/MyStickies.Tests/MyStickies.Tests.csproj -c Release
```

영상의 기대값은 H.264 / 1920×1080 / 30fps / 450프레임 / 15초임.
