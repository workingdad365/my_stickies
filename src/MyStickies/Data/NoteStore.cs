using System.Collections.ObjectModel;
using System.IO;
using MyStickies.Models;

namespace MyStickies.Data;

/// <summary>
/// 메모리 내 노트 목록과 SQLite 저장소를 함께 관리.
/// 덱 창과 메모 관리 창이 같은 인스턴스를 공유해 변경 사항이 즉시 반영됨.
/// DB 파일이 외부(다른 PC의 동기화 등)에서 바뀌면 감지해 알림.
/// </summary>
public sealed class NoteStore : IDisposable
{
    /// <summary>자체 쓰기 직후 이 시간 동안의 파일 변경 이벤트는 무시</summary>
    private static readonly TimeSpan OwnWriteGrace = TimeSpan.FromSeconds(3);

    /// <summary>외부 변경 이벤트가 잠잠해진 뒤 알림까지 대기 시간 (동기화 도구의 연속 쓰기 대비)</summary>
    private static readonly TimeSpan ExternalChangeDebounce = TimeSpan.FromMilliseconds(1500);

    private NoteRepository _repo;
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private DateTime _lastOwnWriteUtc = DateTime.MinValue;

    /// <summary>전체 노트 (숨긴 노트 포함). 생성 시각 오름차순</summary>
    public ObservableCollection<Note> Notes { get; }

    /// <summary>현재 사용 중인 DB 파일 경로</summary>
    public string DbPath => _repo.DbPath;

    /// <summary>컬렉션 변경이 아닌 개별 노트의 내용/상태 변경 알림</summary>
    public event Action<Note>? NoteChanged;

    /// <summary>DB 파일이 외부에서 변경됨. 스레드 풀에서 발생하므로 UI 스레드로 넘겨서 처리할 것</summary>
    public event Action? ExternalChangeDetected;

    public NoteStore(NoteRepository repo)
    {
        _repo = repo;
        Notes = new ObservableCollection<Note>(Load(repo, seedSamples: true));
        StartWatcher();
    }

    /// <summary>DB에서 노트 로드. seedSamples가 true이고 DB 파일이 없던 최초 실행이면 샘플 노트를 넣어 둠</summary>
    private static List<Note> Load(NoteRepository repo, bool seedSamples)
    {
        // 이전 이름(notes.db)의 파일이 있으면 개명해 이어 쓰므로 최초 실행이 아님
        var firstRun = !File.Exists(repo.DbPath) && !NoteRepository.RenameLegacyFile(repo.DbPath);
        repo.Initialize();

        var notes = repo.LoadAll();
        if (seedSamples && firstRun && notes.Count == 0)
        {
            long order = 0;
            foreach (var sample in SampleNotes.Create())
            {
                sample.SortOrder = order++;
                repo.Insert(sample);
                notes.Add(sample);
            }
        }
        return notes;
    }

    /// <summary>다른 DB 파일로 전환. 파일이 없으면 안내 메모가 든 새 파일을 만들고, 있으면 그대로 읽음</summary>
    public void SwitchTo(NoteRepository repo)
    {
        StopWatcher();
        _repo = repo;
        ReplaceAll(Load(repo, seedSamples: true));
        StartWatcher();
    }

    /// <summary>현재 DB 파일을 다시 읽어 메모리 목록을 교체</summary>
    public void Reload() => ReplaceAll(_repo.LoadAll());

    private void ReplaceAll(List<Note> notes)
    {
        Notes.Clear();
        foreach (var note in notes)
            Notes.Add(note);
    }

    /// <summary>기본 제목의 빈 노트를 만들어 DB와 목록에 추가</summary>
    public Note Add(DateTime now) => Add(now, Note.DefaultTitle(now), string.Empty);

    /// <summary>제목과 본문을 지정해 노트 추가 (가져오기 등). 제목이 비면 기본 제목</summary>
    public Note Add(DateTime now, string title, string body)
    {
        var activeCount = Notes.Count(n => !n.IsArchived);
        var note = new Note
        {
            Title = string.IsNullOrWhiteSpace(title) ? Note.DefaultTitle(now) : title.Trim(),
            Body = body,
            ColorHex = NotePalette.All[activeCount % NotePalette.All.Length],
            CreatedAt = now,
            UpdatedAt = now,
            SortOrder = Notes.Count == 0 ? 0 : Notes.Max(n => n.SortOrder) + 1,
        };
        MarkOwnWrite();
        _repo.Insert(note);
        Notes.Add(note);
        return note;
    }

    /// <summary>
    /// 일부 노트의 상대 순서를 바꿈. ordered에 담긴 노트들이 전체 목록에서 차지하던 자리들에
    /// 새 순서대로 다시 배치되고, 나머지 노트의 자리는 그대로. 이후 전체 sort_order를 0부터 다시 매겨 저장
    /// </summary>
    public void Reorder(IReadOnlyList<Note> ordered)
    {
        var slots = ordered.Select(n => Notes.IndexOf(n)).Where(i => i >= 0).OrderBy(i => i).ToList();
        if (slots.Count != ordered.Count) return;

        for (var i = 0; i < ordered.Count; i++)
        {
            var current = Notes.IndexOf(ordered[i]);
            if (current != slots[i])
                Notes.Move(current, slots[i]);
        }

        var changed = new List<Note>();
        for (var i = 0; i < Notes.Count; i++)
        {
            if (Notes[i].SortOrder == i) continue;
            Notes[i].SortOrder = i;
            changed.Add(Notes[i]);
        }
        if (changed.Count == 0) return;

        MarkOwnWrite();
        _repo.UpdateSortOrders(changed);
    }

    /// <summary>노트 내용 저장</summary>
    public void Save(Note note)
    {
        MarkOwnWrite();
        _repo.Update(note);
        NoteChanged?.Invoke(note);
    }

    /// <summary>숨김 처리: 숨김 시각 기록</summary>
    public void Archive(Note note, DateTime now)
    {
        note.ArchivedAt = now;
        note.UpdatedAt = now;
        Save(note);
    }

    /// <summary>숨김 해제: 다시 활성 노트로</summary>
    public void Restore(Note note, DateTime now)
    {
        note.ArchivedAt = null;
        note.UpdatedAt = now;
        Save(note);
    }

    /// <summary>영구 삭제</summary>
    public void Delete(Note note)
    {
        MarkOwnWrite();
        _repo.Delete(note.Id);
        Notes.Remove(note);
    }

    private void MarkOwnWrite() => _lastOwnWriteUtc = DateTime.UtcNow;

    private void StartWatcher()
    {
        var dir = Path.GetDirectoryName(_repo.DbPath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

        _watcher = new FileSystemWatcher(dir, Path.GetFileName(_repo.DbPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
        };
        _watcher.Changed += OnDbFileChanged;
        _watcher.Created += OnDbFileChanged;
        _watcher.Renamed += OnDbFileChanged;
        _watcher.EnableRaisingEvents = true;
    }

    private void StopWatcher()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
        _debounce?.Dispose();
        _debounce = null;
    }

    private void OnDbFileChanged(object sender, FileSystemEventArgs e)
    {
        if (DateTime.UtcNow - _lastOwnWriteUtc < OwnWriteGrace) return;

        // 연속 이벤트는 마지막 이벤트 기준으로 한 번만 알림
        _debounce?.Dispose();
        _debounce = new Timer(_ => ExternalChangeDetected?.Invoke(), null, ExternalChangeDebounce, Timeout.InfiniteTimeSpan);
    }

    public void Dispose() => StopWatcher();
}
