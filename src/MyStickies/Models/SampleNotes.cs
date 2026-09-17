namespace MyStickies.Models;

/// <summary>최초 실행 시 넣어 두는 안내 메모. 사용법을 간단히 설명하며 지워도 무방</summary>
public static class SampleNotes
{
    public static List<Note> Create()
    {
        var t = DateTime.Now;
        return
        [
            new()
            {
                Title = "환영합니다",
                Body = "My Stickies는 화면 오른쪽 가장자리에 붙어 있는 스티커 메모입니다.\n\n" +
                       "- 오른쪽 가장자리의 책갈피에 마우스를 올리면 메모들이 펼쳐집니다\n" +
                       "- 스티커 위에 마우스를 올리면 내용이 보입니다\n" +
                       "- 스티커를 클릭하면 바로 편집할 수 있습니다\n" +
                       "- 마우스를 치우면 다시 접힙니다\n\n" +
                       "이 안내 메모들은 다 읽으신 뒤 지우셔도 됩니다.",
                ColorHex = NotePalette.Blue, CreatedAt = t, UpdatedAt = t,
            },
            new()
            {
                Title = "편집과 단축키",
                Body = "- 제목을 클릭하면 제목칸, 본문을 클릭하면 본문칸이 열립니다\n" +
                       "- 제목칸에서 Enter: 본문칸으로 이동\n" +
                       "- Ctrl+Enter 또는 Ctrl+S: 저장하고 닫기\n" +
                       "- Esc: 변경 취소\n" +
                       "- 바깥을 클릭해도 저장됩니다\n" +
                       "- 제목을 비우면 \"새 메모 (시각)\"이 제목이 됩니다\n\n" +
                       "전역 단축키\n" +
                       "- Ctrl+Alt+S: 덱 열기/닫기\n" +
                       "- Ctrl+Alt+N: 새 메모 바로 쓰기",
                ColorHex = NotePalette.Green, CreatedAt = t.AddSeconds(1), UpdatedAt = t.AddSeconds(1),
            },
            new()
            {
                Title = "버튼과 정리",
                Body = "- 덱 아래 +: 새 메모\n" +
                       "- 스티커 오른쪽 위 −: 숨김 (덱에서 빼되 보관)\n" +
                       "- 스티커 오른쪽 위 ×: 삭제. 한 번 더 눌러 확인\n" +
                       "- 편집 중 아래 색상 점: 스티커 색 변경\n" +
                       "- 스티커를 위아래로 끌면 순서가 바뀝니다\n\n" +
                       "덱에는 최근 메모 몇 장만 보입니다. 나머지와 숨긴 메모는 " +
                       "트레이 아이콘 또는 책갈피 우클릭 메뉴의 \"메모 관리\"에서 검색, 복원, 내보내기, 가져오기를 할 수 있습니다.",
                ColorHex = NotePalette.Purple, CreatedAt = t.AddSeconds(2), UpdatedAt = t.AddSeconds(2),
            },
            new()
            {
                Title = "설정과 동기화",
                Body = "트레이 아이콘 우클릭 > 설정에서 바꿀 수 있습니다.\n\n" +
                       "- 메모 저장 폴더: Synology Drive 같은 동기화 폴더를 지정하면 여러 PC에서 같은 메모를 씁니다\n" +
                       "- 덱을 붙일 모니터, 표시 개수, 시작 위치, 접힘 지연\n" +
                       "- 글꼴과 본문 크기\n" +
                       "- 전체화면 앱 실행 중 자동 숨김, 전역 단축키\n" +
                       "- Windows 로그인 시 자동 실행",
                ColorHex = NotePalette.Yellow, CreatedAt = t.AddSeconds(3), UpdatedAt = t.AddSeconds(3),
            },
        ];
    }
}
