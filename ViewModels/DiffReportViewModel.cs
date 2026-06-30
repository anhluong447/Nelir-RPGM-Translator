using System.Collections.Generic;
using Nelir.Models;

namespace Nelir.ViewModels
{
    public class DiffReportViewModel
    {
        public List<TranslationRow> NewRows { get; }
        public List<TranslationRow> RemovedRows { get; }
        public List<ChangedRowItem> ChangedRows { get; }

        public string NewCountText => $"+ {NewRows.Count} Dòng mới";
        public string RemovedCountText => $"- {RemovedRows.Count} Dòng đã xóa";
        public string ChangedCountText => $"~ {ChangedRows.Count} Dòng thay đổi";

        public DiffReportViewModel(ProjectDiffResult diffResult)
        {
            NewRows = diffResult.NewRows;
            RemovedRows = diffResult.RemovedRows;
            ChangedRows = diffResult.ChangedRows;
        }
    }
}
