using System.ComponentModel;
using System.Runtime.CompilerServices;
using MyStickies.Localization;

namespace MyStickies.Models;

/// <summary>스티커 노트 한 장의 데이터</summary>
public sealed class Note : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _body = string.Empty;
    private string _colorHex = NotePalette.Blue;
    private DateTime? _archivedAt;

    public Guid Id { get; init; } = Guid.NewGuid();

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value) return;
            _title = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Label));
        }
    }

    public string Body
    {
        get => _body;
        set
        {
            if (_body == value) return;
            _body = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Preview));
        }
    }

    public string ColorHex
    {
        get => _colorHex;
        set
        {
            if (_colorHex == value) return;
            _colorHex = value;
            OnPropertyChanged();
        }
    }

    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>덱 표시 순서. 작을수록 위. 재정렬 시 0부터 다시 매김</summary>
    public long SortOrder { get; set; }

    /// <summary>완료 처리 시각. null이면 활성 노트</summary>
    public DateTime? ArchivedAt
    {
        get => _archivedAt;
        set
        {
            if (_archivedAt == value) return;
            _archivedAt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsArchived));
        }
    }

    /// <summary>완료(보관) 여부. 보관된 노트는 덱에 표시하지 않음</summary>
    public bool IsArchived => ArchivedAt is not null;

    /// <summary>목록용 한 줄 미리보기. 줄바꿈을 공백으로 접음</summary>
    public string Preview => string.Join(' ', Body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Trim()));

    /// <summary>탭 세로 라벨용 대문자 제목</summary>
    public string Label => Title.ToUpperInvariant();

    /// <summary>제목 미입력 시 사용하는 기본 제목. 형식: 새 메모 (yyyy-MM-dd HH:mm)</summary>
    public static string DefaultTitle(DateTime now) => Strings.Get("DefaultTitle", now);

    /// <summary>제목이 비어 있으면 기본 제목으로 채움</summary>
    public void EnsureTitle(DateTime now)
    {
        if (string.IsNullOrWhiteSpace(Title))
            Title = DefaultTitle(now);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>원본 앱의 파스텔 팔레트</summary>
public static class NotePalette
{
    public const string Blue = "#BDD9FF";
    public const string Green = "#BFEBD6";
    public const string Purple = "#E3D5FF";
    public const string Yellow = "#FBE27A";
    public const string Orange = "#FFC9B5";

    public static readonly string[] All = [Blue, Green, Purple, Yellow, Orange];
}
