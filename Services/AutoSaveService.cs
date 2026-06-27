using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using NolirRpgmTranslator.Models;

namespace NolirRpgmTranslator.Services
{
    public class AutoSaveService
    {
        private readonly ProjectState _projectState;
        private readonly ExportService _exportService;
        private readonly DispatcherTimer _timer;

        public event EventHandler<DateTime>? AutoSaveCompleted;

        public AutoSaveService(ProjectState projectState, ExportService exportService)
        {
            _projectState = projectState;
            _exportService = exportService;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(30);
            _timer.Tick += Timer_Tick;
        }

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            TriggerAutoSave();
        }

        public void TriggerAutoSave()
        {
            if (string.IsNullOrEmpty(_projectState.DataFolderPath) || _projectState.AllRows.Count == 0)
            {
                return;
            }

            // Check if there are any modified rows
            bool hasDirtyRows = _projectState.AllRows.Any(r => r.IsDirty);
            if (!hasDirtyRows)
            {
                return;
            }

            try
            {
                string autosavePath = Path.Combine(_projectState.DataFolderPath, ".nolir_autosave.json");
                _exportService.ExportFlatJson(_projectState, autosavePath);

                // Reset the IsDirty flags on UI thread safely
                foreach (var row in _projectState.AllRows)
                {
                    if (row.IsDirty)
                    {
                        row.IsDirty = false;
                    }
                }

                AutoSaveCompleted?.Invoke(this, DateTime.Now);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Autosave failed: {ex.Message}");
            }
        }
    }
}
