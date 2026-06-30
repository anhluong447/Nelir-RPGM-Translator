using System.Collections.Generic;
using System.Linq;
using Nelir.Models;

namespace Nelir.Services
{
    public class ProjectDiffService
    {
        public ProjectDiffResult Compare(ProjectState oldProject, ProjectState newRawProject)
        {
            var result = new ProjectDiffResult();

            var oldByKey = oldProject.AllRows
                .Where(r => r.RowType != RowType.SectionHeader)
                .ToDictionary(r => r.UniqueKey);

            var newByKey = newRawProject.AllRows
                .Where(r => r.RowType != RowType.SectionHeader)
                .ToDictionary(r => r.UniqueKey);

            foreach (var kvp in newByKey)
            {
                var key = kvp.Key;
                var newRow = kvp.Value;

                if (!oldByKey.TryGetValue(key, out var oldRow))
                {
                    result.NewRows.Add(newRow);
                }
                else if (oldRow.RawText != newRow.RawText)
                {
                    result.ChangedRows.Add(new ChangedRowItem(oldRow, newRow));
                }
                else
                {
                    // Giữ nguyên — carry forward bản dịch cũ sang project mới
                    newRow.TranslationText = oldRow.TranslationText;
                    result.UnchangedRows.Add(newRow);
                }
            }

            foreach (var kvp in oldByKey)
            {
                var key = kvp.Key;
                var oldRow = kvp.Value;

                if (!newByKey.ContainsKey(key))
                {
                    result.RemovedRows.Add(oldRow);
                }
            }

            return result;
        }
    }
}
