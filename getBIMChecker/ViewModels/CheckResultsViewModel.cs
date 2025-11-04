using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using getBIMChecker.Models;
using getBIMChecker.Services;
using Microsoft.Win32;

namespace getBIMChecker.ViewModels
{
    /// <summary>
    /// ViewModel для окна результатов проверки осей
    /// </summary>
    public class CheckResultsViewModel : INotifyPropertyChanged
    {
        private readonly UIDocument _uiDocument;
        private CheckReport _report;
        private ObservableCollection<AxisValidationResult> _errors;
        private AxisValidationResult _selectedError;

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
            }
        }

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
                HighlightAxisInModel();
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

        public ICommand ExportToCsvCommand { get; }
        public ICommand CloseCommand { get; }

        #endregion

        #region Constructor

        public CheckResultsViewModel(CheckReport report, UIDocument uiDocument)
        {
            _report = report;
            _uiDocument = uiDocument;
            _errors = new ObservableCollection<AxisValidationResult>(report.Errors);

            // Инициализация команд
            ExportToCsvCommand = new RelayCommand(ExportToCsv);
            CloseCommand = new RelayCommand(CloseWindow);
        }

        #endregion

        #region Command Methods

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
                    var reportService = new ReportService(null); // DatabaseService не нужен для экспорта
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

        #endregion

        #region Helper Methods

        /// <summary>
        /// Выделение оси в модели Revit при выборе строки
        /// </summary>
        private void HighlightAxisInModel()
        {
            if (SelectedError?.ElementId == null)
                return;

            try
            {
                var elementId = new ElementId((int)SelectedError.ElementId.Value);
                var ids = new System.Collections.Generic.List<ElementId> { elementId };
                
                _uiDocument.Selection.SetElementIds(ids);
                _uiDocument.ShowElements(elementId);
            }
            catch (Exception ex)
            {
                // Логирование ошибки (элемент может быть удален)
                System.Diagnostics.Debug.WriteLine($"Ошибка выделения оси: {ex.Message}");
            }
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
