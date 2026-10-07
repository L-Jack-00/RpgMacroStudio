using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.Core;

/// <summary>
/// 프로필 편집 이력(추가, 삭제, 수정, 상태 변경 등)의 실행 취소(Undo) 및 다시 실행(Redo)을 관리하는 스냅샷 매니저입니다.
/// 단축키(Ctrl+Z / Ctrl+Y) 및 툴바 버튼을 통해 완벽한 작업 복원을 제공합니다.
/// </summary>
public class UndoRedoManager
{
    public record SnapshotEntry(string Snapshot, string ActionName);

    private readonly Stack<SnapshotEntry> _undoStack = new();
    private readonly Stack<SnapshotEntry> _redoStack = new();
    private const int MaxHistory = 50;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// 프로필 변경 전 현재 상태 및 수행 중인 작업 명칭을 Undo 스택에 저장합니다.
    /// </summary>
    public void RecordSnapshot(MacroEventProfile profile, string actionName = "작업")
    {
        try
        {
            string snapshot = JsonSerializer.Serialize(profile, _jsonOptions);
            _undoStack.Push(new SnapshotEntry(snapshot, actionName));
            _redoStack.Clear();

            if (_undoStack.Count > MaxHistory)
            {
                var list = _undoStack.ToList();
                list.RemoveAt(list.Count - 1);
                _undoStack.Clear();
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    _undoStack.Push(list[i]);
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// 마지막 상태로 실행 취소(Undo)하고 이전 프로필과 취소된 작업명을 반환합니다.
    /// </summary>
    public (MacroEventProfile? Profile, string ActionName) Undo(MacroEventProfile currentProfile)
    {
        if (!CanUndo) return (null, string.Empty);

        try
        {
            var previousEntry = _undoStack.Pop();
            string currentSnapshot = JsonSerializer.Serialize(currentProfile, _jsonOptions);
            _redoStack.Push(new SnapshotEntry(currentSnapshot, previousEntry.ActionName));

            var restored = JsonSerializer.Deserialize<MacroEventProfile>(previousEntry.Snapshot, _jsonOptions);
            return (restored, previousEntry.ActionName);
        }
        catch
        {
            return (null, string.Empty);
        }
    }

    /// <summary>
    /// 취소된 작업을 다시 실행(Redo)하고 복원된 프로필과 재실행된 작업명을 반환합니다.
    /// </summary>
    public (MacroEventProfile? Profile, string ActionName) Redo(MacroEventProfile currentProfile)
    {
        if (!CanRedo) return (null, string.Empty);

        try
        {
            var nextEntry = _redoStack.Pop();
            string currentSnapshot = JsonSerializer.Serialize(currentProfile, _jsonOptions);
            _undoStack.Push(new SnapshotEntry(currentSnapshot, nextEntry.ActionName));

            var restored = JsonSerializer.Deserialize<MacroEventProfile>(nextEntry.Snapshot, _jsonOptions);
            return (restored, nextEntry.ActionName);
        }
        catch
        {
            return (null, string.Empty);
        }
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
