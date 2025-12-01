using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.UI;
using getBIMChecker.Events;
using getBIMChecker.Models;
using getBIMChecker.Services;
using Microsoft.Win32;

namespace getBIMChecker.ViewModels
{
    public class LevelCheckResultsViewModel : INotifyPropertyChanged
    {
        private readonly UIDocument _uiDocument;
        private readonly ExternalEvent _externalEvent;
        private readonly LevelFixEventHandler _eventHandler;
        private LevelCheckReport _report;
        private ObservableCollection<LevelValidationResult> _errors;
        private LevelValidationResult _selectedError;
        private string _fixStatusMessage;
        private bool _isFixing;

        public LevelCheckReport Report { get => _report; set { _report = value; OnPropertyChanged(nameof(Report)); } }
        public ObservableCollection<LevelValidationResult> Errors { get => _errors; set { _errors = value; OnPropertyChanged(nameof(Errors)); OnPropertyChanged(nameof(HasErrors)); } }
        public bool HasErrors => Errors != null && Errors.Count > 0;

        public LevelValidationResult SelectedError
        {
            get => _selectedError;
            set
            {
                _selectedError = value;
                OnPropertyChanged(nameof(SelectedError));
                OnPropertyChanged(nameof(CanFixSelected));
                if (_selectedError?.ElementId != null) SelectLevelInModel(_selectedError.ElementId.Value);
            }
        }

        public bool CanFixSelected => SelectedError != null && !IsFixing && CanBeFixed(SelectedError);
        public string FixStatusMessage { get => _fixStatusMessage; set { _fixStatusMessage = value; OnPropertyChanged(nameof(FixStatusMessage)); } }
        public bool IsFixing { get => _isFixing; set { _isFixing = value; OnPropertyChanged(nameof(IsFixing)); OnPropertyChanged(nameof(CanFixSelected)); } }

        public string ErrorStatistics
        {
            get
            {
                if (Report == null) return string.Empty;
                var stats = Report.GetErrorStatistics();
                return string.Join("\n", stats.OrderByDescending(s => s.Value).Select(s => $"  • {GetErrorTypeName(s.Key)}: {s.Value}"));
            }
        }

        public ICommand FixSelectedCommand { get; }
        public ICommand FixAllCommand { get; }
        public ICommand ExportToCsvCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand RefreshCommand { get; }

        public LevelCheckResultsViewModel(LevelCheckReport report, UIDocument uiDocument, LevelFixEventHandler eventHandler, ExternalEvent externalEvent)
        {
            _report = report;
            _uiDocument = uiDocument;
            _errors = new ObservableCollection<LevelValidationResult>(report.Errors);
            _eventHandler = eventHandler;
            _externalEvent = externalEvent;

            _eventHandler.OnFixCompleted = OnFixCompleted;
            _eventHandler.OnSelectCompleted = OnSelectCompleted;

            FixSelectedCommand = new RelayCommand(FixSelected, () => CanFixSelected);
            FixAllCommand = new RelayCommand(FixAll, () => HasErrors && !IsFixing);
            ExportToCsvCommand = new RelayCommand(ExportToCsv);
            CloseCommand = new RelayCommand(CloseWindow);
            RefreshCommand = new RelayCommand(RefreshErrors);

            FixStatusMessage = "Готово к исправлению";
        }

        private void FixSelected()
        {
            if (SelectedError == null) return;
            IsFixing = true;
            FixStatusMessage = $"Исправление уровня {SelectedError.LevelName}...";
            _eventHandler.CurrentAction = LevelFixAction.FixSingle;
            _eventHandler.ErrorToFix = SelectedError;
            _externalEvent.Raise();
        }

        private void FixAll()
        {
            var fixableErrors = Errors.Where(e => CanBeFixed(e)).ToList();
            if (fixableErrors.Count == 0)
            {
                MessageBox.Show("Нет ошибок для автоматического исправления.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Будет исправлено {fixableErrors.Count} уровней.\n\nПродолжить?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            IsFixing = true;
            FixStatusMessage = $"Исправление {fixableErrors.Count} уровней...";
            _eventHandler.CurrentAction = LevelFixAction.FixAll;
            _eventHandler.ErrorsToFix = fixableErrors;
            _externalEvent.Raise();
        }

        private void SelectLevelInModel(long elementId)
        {
            _eventHandler.CurrentAction = LevelFixAction.SelectLevel;
            _eventHandler.ElementIdToSelect = elementId;
            _externalEvent.Raise();
        }

        private void ExportToCsv()
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV файлы (*.csv)|*.csv",
                FileName = $"LevelCheck_{Report.ModelName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var reportService = new LevelReportService(null);
                    reportService.ExportToCsv(Report, saveDialog.FileName);
                    MessageBox.Show("Отчет экспортирован", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка экспорта:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseWindow()
        {
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
            window?.Close();
        }

        private void RefreshErrors()
        {
            foreach (var error in Errors.Where(e => !e.HasErrors).ToList())
                Errors.Remove(error);

            Report.ErrorCount = Errors.Count;
            Report.Errors = Errors.ToList();
            OnPropertyChanged(nameof(ErrorStatistics));
            OnPropertyChanged(nameof(HasErrors));
            FixStatusMessage = $"Осталось ошибок: {Errors.Count}";
        }

        private void OnFixCompleted(System.Collections.Generic.List<LevelFixResult> results)
        {
            IsFixing = false;
            int successCount = results.Count(r => r.Success);
            int failCount = results.Count - successCount;

            foreach (var result in results.Where(r => r.Success))
            {
                var error = Errors.FirstOrDefault(e => e.LevelName == result.LevelName);
                if (error != null)
                    foreach (var fixedError in result.FixedErrors)
                        error.ErrorTypes.Remove(fixedError);
            }

            RefreshErrors();

            if (failCount == 0)
                FixStatusMessage = $"Успешно исправлено: {successCount}";
            else
            {
                FixStatusMessage = $"Исправлено: {successCount}, ошибок: {failCount}";
                var failedMessages = results.Where(r => !r.Success).SelectMany(r => r.FailedErrors).Take(5);
                MessageBox.Show($"Некоторые ошибки не удалось исправить:\n\n{string.Join("\n", failedMessages)}", "Результат", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnSelectCompleted(bool success) { }

        private bool CanBeFixed(LevelValidationResult error)
            => error != null && error.HasErrors && !error.ErrorTypes.All(e => e == LevelErrorType.NotInReference);

        private string GetErrorTypeName(LevelErrorType type) => type switch
        {
            LevelErrorType.ElevationDeviation => "Отклонение отметки",
            LevelErrorType.NotPinned => "Не закреплен",
            LevelErrorType.WrongWorkset => "Неправильный рабочий набор",
            LevelErrorType.NotInReference => "Отсутствует в эталоне",
            LevelErrorType.NotInModel => "Отсутствует в модели",
            _ => "Неизвестная ошибка"
        };

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}