using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using getBIMChecker.Events;
using getBIMChecker.Models;
using getBIMChecker.Services;
using Microsoft.Win32;

namespace getBIMChecker.ViewModels
{
    /// <summary>
    /// ViewModel для окна результатов проверки осей (немодальное окно)
    /// </summary>
    public class CheckResultsViewModel : INotifyPropertyChanged
    {
        #region Private Fields

        private readonly UIDocument _uiDocument;
        private readonly ExternalEvent _externalEvent;
        private readonly AxisFixEventHandler _eventHandler;

        private CheckReport _report;
        private ObservableCollection<AxisValidationResult> _errors;
        private AxisValidationResult _selectedError;
        private string _fixStatusMessage;
        private bool _isFixing;

        #endregion

        #region Properties

        /// <summary>
        /// Отчет о проверке
        /// </summary>
        public CheckReport Report
        {
            get => _report;
            set
            {
                _report = value;
                OnPropertyChanged(nameof(Report));
            }
        }

        /// <summary>
        /// Список ошибок
        /// </summary>
        public ObservableCollection<AxisValidationResult> Errors
        {
            get => _errors;
            set
            {
                _errors = value;
                OnPropertyChanged(nameof(Errors));
                OnPropertyChanged(nameof(HasErrors));
            }
        }

        /// <summary>
        /// Есть ли ошибки для исправления
        /// </summary>
        public bool HasErrors => Errors != null && Errors.Count > 0;

        /// <summary>
        /// Выбранная ошибка
        /// </summary>
        public AxisValidationResult SelectedError
        {
            get => _selectedError;
            set
            {
                _selectedError = value;
                OnPropertyChanged(nameof(SelectedError));
                OnPropertyChanged(nameof(CanFixSelected));
                
                // Выделяем ось в модели при выборе
                if (_selectedError?.ElementId != null)
                {
                    SelectAxisInModel(_selectedError.ElementId.Value);
                }
            }
        }

        /// <summary>
        /// Можно ли исправить выбранную ось
        /// </summary>
        public bool CanFixSelected => SelectedError != null && !IsFixing && CanBeFixed(SelectedError);

        /// <summary>
        /// Статусное сообщение об исправлении
        /// </summary>
        public string FixStatusMessage
        {
            get => _fixStatusMessage;
            set
            {
                _fixStatusMessage = value;
                OnPropertyChanged(nameof(FixStatusMessage));
            }
        }

        /// <summary>
        /// Идет процесс исправления
        /// </summary>
        public bool IsFixing
        {
            get => _isFixing;
            set
            {
                _isFixing = value;
                OnPropertyChanged(nameof(IsFixing));
                OnPropertyChanged(nameof(CanFixSelected));
            }
        }

        /// <summary>
        /// Статистика ошибок для отображения
        /// </summary>
        public string ErrorStatistics
        {
            get
            {
                if (Report == null)
                    return string.Empty;

                var stats = Report.GetErrorStatistics();
                var lines = stats.OrderByDescending(s => s.Value)
                    .Select(s => $"  • {GetErrorTypeName(s.Key)}: {s.Value}");

                return string.Join("\n", lines);
            }
        }

        #endregion

        #region Commands

        public ICommand FixSelectedCommand { get; }
        public ICommand FixAllCommand { get; }
        public ICommand SelectAxisCommand { get; }
        public ICommand ExportToCsvCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand RefreshCommand { get; }

        #endregion

        #region Constructor

        public CheckResultsViewModel(
            CheckReport report,
            UIDocument uiDocument,
            AxisFixEventHandler eventHandler,
            ExternalEvent externalEvent)
        {
            _report = report;
            _uiDocument = uiDocument;
            _errors = new ObservableCollection<AxisValidationResult>(report.Errors);



            _report = report;
            _uiDocument = uiDocument;
            _errors = new ObservableCollection<AxisValidationResult>(report.Errors);

            // Используем переданные (а не создаём новые!)
            _eventHandler = eventHandler;
            _externalEvent = externalEvent;

            _eventHandler.OnFixCompleted = OnFixCompleted;
            _eventHandler.OnSelectCompleted = OnSelectCompleted;


            // Создаем внешнее событие
            _externalEvent = ExternalEvent.Create(_eventHandler);

            // Инициализация команд
            FixSelectedCommand = new RelayCommand(FixSelected, () => CanFixSelected);
            FixAllCommand = new RelayCommand(FixAll, () => HasErrors && !IsFixing);
            SelectAxisCommand = new RelayCommand<AxisValidationResult>(SelectAxis);
            ExportToCsvCommand = new RelayCommand(ExportToCsv);
            CloseCommand = new RelayCommand(CloseWindow);
            RefreshCommand = new RelayCommand(RefreshErrors);

            FixStatusMessage = "Готово к исправлению";
        }

        #endregion

        #region Command Methods

        /// <summary>
        /// Исправить выбранную ось
        /// </summary>
        private void FixSelected()
        {
            if (SelectedError == null)
                return;

            IsFixing = true;
            FixStatusMessage = $"Исправление оси {SelectedError.AxisName}...";

            _eventHandler.CurrentAction = AxisFixAction.FixSingle;
            _eventHandler.ErrorToFix = SelectedError;

            _externalEvent.Raise();
        }

        /// <summary>
        /// Исправить все оси
        /// </summary>
        private void FixAll()
        {
            if (Errors == null || Errors.Count == 0)
                return;

            // Фильтруем только исправимые ошибки
            var fixableErrors = Errors.Where(e => CanBeFixed(e)).ToList();

            if (fixableErrors.Count == 0)
            {
                MessageBox.Show(
                    "Нет ошибок, которые можно автоматически исправить.",
                    "Информация",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Будет исправлено {fixableErrors.Count} осей.\n\n" +
                "Ошибки типа 'Отсутствует в эталоне' не будут исправлены автоматически.\n\n" +
                "Продолжить?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            IsFixing = true;
            FixStatusMessage = $"Исправление {fixableErrors.Count} осей...";

            _eventHandler.CurrentAction = AxisFixAction.FixAll;
            _eventHandler.ErrorsToFix = fixableErrors;

            _externalEvent.Raise();
        }

        /// <summary>
        /// Выделить ось в модели
        /// </summary>
        private void SelectAxis(AxisValidationResult error)
        {
            if (error?.ElementId == null)
                return;

            SelectAxisInModel(error.ElementId.Value);
        }

        /// <summary>
        /// Выделить ось в модели через ExternalEvent
        /// </summary>
        private void SelectAxisInModel(long elementId)
        {
            _eventHandler.CurrentAction = AxisFixAction.SelectAxis;
            _eventHandler.ElementIdToSelect = elementId;

            _externalEvent.Raise();
        }

        /// <summary>
        /// Экспорт в CSV
        /// </summary>
        private void ExportToCsv()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV файлы (*.csv)|*.csv",
                    FileName = $"AxisCheck_{Report.ModelName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    DefaultExt = "csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var reportService = new ReportService(null);
                    reportService.ExportToCsv(Report, saveDialog.FileName);

                    MessageBox.Show(
                        "Отчет успешно экспортирован",
                        "Экспорт завершен",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка экспорта:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Закрыть окно
        /// </summary>
        private void CloseWindow()
        {
            var window = Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.DataContext == this);
            window?.Close();
        }

        /// <summary>
        /// Обновить список ошибок (удалить исправленные)
        /// </summary>
        private void RefreshErrors()
        {
            // Удаляем ошибки, у которых больше нет типов ошибок
            var toRemove = Errors.Where(e => !e.HasErrors).ToList();
            
            foreach (var error in toRemove)
            {
                Errors.Remove(error);
            }

            Report.ErrorCount = Errors.Count;
            Report.Errors = Errors.ToList();

            OnPropertyChanged(nameof(ErrorStatistics));
            OnPropertyChanged(nameof(HasErrors));

            FixStatusMessage = $"Осталось ошибок: {Errors.Count}";
        }

        #endregion

        #region Callbacks

        /// <summary>
        /// Callback после завершения исправления
        /// </summary>
        private void OnFixCompleted(System.Collections.Generic.List<FixResult> results)
        {
            IsFixing = false;

            int successCount = results.Count(r => r.Success);
            int failCount = results.Count - successCount;

            // Обновляем список ошибок
            foreach (var result in results.Where(r => r.Success))
            {
                // Находим исправленную ошибку и удаляем исправленные типы ошибок
                var error = Errors.FirstOrDefault(e => e.AxisName == result.AxisName);
                if (error != null)
                {
                    foreach (var fixedError in result.FixedErrors)
                    {
                        error.ErrorTypes.Remove(fixedError);
                    }
                }
            }

            // Обновляем UI
            RefreshErrors();

            // Показываем результат
            if (failCount == 0)
            {
                FixStatusMessage = $"Успешно исправлено: {successCount}";
            }
            else
            {
                FixStatusMessage = $"Исправлено: {successCount}, ошибок: {failCount}";

                // Показываем детали ошибок
                var failedMessages = results
                    .Where(r => !r.Success)
                    .SelectMany(r => r.FailedErrors)
                    .Take(5);

                MessageBox.Show(
                    $"Некоторые ошибки не удалось исправить:\n\n{string.Join("\n", failedMessages)}",
                    "Результат исправления",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Callback после выделения оси
        /// </summary>
        private void OnSelectCompleted(bool success)
        {
            if (!success)
            {
                System.Diagnostics.Debug.WriteLine("[CheckResultsVM] Не удалось выделить ось");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Проверить, можно ли автоматически исправить ошибку
        /// </summary>
        private bool CanBeFixed(AxisValidationResult error)
        {
            if (error == null || !error.HasErrors)
                return false;

            // Ошибка "Отсутствует в эталоне" не может быть автоматически исправлена
            // (нужно либо удалить ось, либо добавить в эталон вручную)
            bool hasOnlyNotInReference = error.ErrorTypes.All(e => e == ErrorType.NotInReference);
            
            return !hasOnlyNotInReference;
        }

        /// <summary>
        /// Получить название типа ошибки
        /// </summary>
        private string GetErrorTypeName(ErrorType type)
        {
            return type switch
            {
                ErrorType.Deviation => "Отклонение от эталона",
                ErrorType.NonParallel => "Непараллельность",
                ErrorType.NotPinned => "Не закреплена",
                ErrorType.WrongWorkset => "Неправильный рабочий набор",
                ErrorType.NotInReference => "Отсутствует в эталоне",
                ErrorType.NotInModel => "Отсутствует в модели",
                _ => "Неизвестная ошибка"
            };
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
