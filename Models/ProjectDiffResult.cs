using System.Collections.Generic;

namespace Nelir.Models
{
    public class ProjectDiffResult
    {
        public List<TranslationRow> NewRows { get; set; } = new();        // Có trong RAW mới, không có trong project cũ
        public List<TranslationRow> RemovedRows { get; set; } = new();    // Có trong project cũ, không còn trong RAW mới
        public List<ChangedRowItem> ChangedRows { get; set; } = new(); // RawText khác nhau nhưng cùng UniqueKey
        public List<TranslationRow> UnchangedRows { get; set; } = new();  // Giữ nguyên, giữ bản dịch cũ

        public int TotalNew => NewRows.Count;
        public int TotalRemoved => RemovedRows.Count;
        public int TotalChanged => ChangedRows.Count;
    }

    public class ChangedRowItem
    {
        public TranslationRow OldRow { get; set; }
        public TranslationRow NewRow { get; set; }

        public ChangedRowItem(TranslationRow oldRow, TranslationRow newRow)
        {
            OldRow = oldRow;
            NewRow = newRow;
        }
    }
}
