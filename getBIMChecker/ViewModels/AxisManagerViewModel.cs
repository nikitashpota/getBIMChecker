using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using getBIMChecker.Commands;
using getBIMChecker.Models;
using getBIMChecker.Services;

namespace getBIMChecker.ViewModels
{
    /// <summary>
    /// ViewModel главного окна управления эталонными осями
    /// </summary>
    public class AxisManagerViewModel : INotifyPropertyChanged
    {
        #region Private Fields

        private readonly ExternalCommandData _commandData;
        private readonly Document _document;
        private readonly UIDocument _uiDocument;

        private readonly DatabaseService _databaseService;
        private readonly DirectoryService _directoryService;
        private readonly ModelBindingService _modelBindingService;
        private readonly AxisCollectionService _axisCollectionService;

        private ObservableCollection<Directory> _directories;
        private Directory _selectedDirectory;
        private int? _boundDirectoryId;
        private string _modelName;
        private int _selectedAxisCount;
        private List<Grid> _selectedGrids;
        private string _statusMessage;

        #endregion

        #region Properties

        /// <summary>
        /// Список директорий
        /// </summary>
        public ObservableCollection<Directory> Directories
        {
            get => _directories;
            set
            {
                _directories = value;
                OnPropertyChanged(nameof(Directories));
            }
        }

        /// <summary>
        /// Выбранная директория
        /// </summary>
        public Directory SelectedDirectory
        {
            get => _selectedDirectory;
            set
            {
                _selectedDirectory = value;
                OnPropertyChanged(nameof(SelectedDirectory));
            }
        }

        /// <summary>
        /// ID директории, к которой привязана модель
        /// </summary>
        public int? BoundDirectoryId
        {
            get => _boundDirectoryId;
            set
            {
                _boundDirectoryId = value;
                OnPropertyChanged(nameof(BoundDirectoryId));
                OnPropertyChanged(nameof(IsBound));
            }
        }

        /// <summary>
        /// Привязана ли модель к директории
        /// </summary>
        public bool IsBound => _boundDirectoryId.HasValue;

        /// <summary>
        /// Имя текущей модели
        /// </summary>
        public string ModelName
        {
            get => _modelName;
            set
            {
                _modelName = value;
                OnPropertyChanged(nameof(ModelName));
            }
        }

        /// <summary>
        /// Количество выбранных осей
        /// </summary>
        public int SelectedAxisCount
        {
            get => _selectedAxisCount;
            set
            {
                _selectedAxisCount = value;
                OnPropertyChanged(nameof(SelectedAxisCount));
                OnPropertyChanged(nameof(AxisCountText));
            }
        }

        /// <summary>
        /// Текст с количеством выбранных осей
        /// </summary>
        public string AxisCountText => SelectedAxisCount > 0 ? $"Выбрано осей: {SelectedAxisCount}" : "Оси не выбраны";

        /// <summary>
        /// Статусное сообщение
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        #endregion

        #region Commands

        public ICommand CreateDirectoryCommand { get; }
        public ICommand EditDirectoryCommand { get; }
        public ICommand DeleteDirectoryCommand { get; }
        public ICommand BindDirectoryCommand { get; }
        public ICommand SelectAxisCommand { get; }
        public ICommand SendAxisCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand RunAxisCheckCommand { get; }

        #endregion

        #region Constructor

        public AxisManagerViewModel(ExternalCommandData commandData)
        {
            _commandData = commandData;
            _uiDocument = commandData.Application.ActiveUIDocument;
            _document = _uiDocument.Document;

            // Инициализация сервисов
            var dbSettings = DatabaseSettings.Default;

            // Инициализация БД
            if (!DatabaseInitializer.Initialize(dbSettings))
            {
                StatusMessage = "Ошибка подключения к БД";
                return;
            }

            _databaseService = new DatabaseService(dbSettings);
            _directoryService = new DirectoryService(_databaseService);
            _modelBindingService = new ModelBindingService();
            _axisCollectionService = new AxisCollectionService();

            // Инициализация команд
            CreateDirectoryCommand = new RelayCommand(CreateDirectory);
            EditDirectoryCommand = new RelayCommand(EditDirectory, () => SelectedDirectory != null);
            DeleteDirectoryCommand = new RelayCommand(DeleteDirectory, () => SelectedDirectory != null);
            BindDirectoryCommand = new RelayCommand(BindDirectory, () => SelectedDirectory != null);
            SelectAxisCommand = new RelayCommand(SelectAxis);
            SendAxisCommand = new RelayCommand(SendAxis, () => SelectedAxisCount > 0 && IsBound);
            RefreshCommand = new RelayCommand(LoadDirectories);
            RunAxisCheckCommand = new RelayCommand(RunAxisCheck);

            // Загрузка данных
            ModelName = _modelBindingService.GetModelName(_document);

            // Получаем код директории из параметра "#_Код площадки"
            string boundDirectoryCode = _modelBindingService.GetBoundDirectoryCode(_document);
            if (!string.IsNullOrWhiteSpace(boundDirectoryCode))
            {
                // Ищем директорию по коду
                var boundDirectory = _directoryService.GetDirectoryByCode(boundDirectoryCode);
                BoundDirectoryId = boundDirectory?.Id;
            }

            LoadDirectories();

            StatusMessage = IsBound ? "Модель привязана к директории" : "Модель не привязана";
        }

        #endregion

        #region Command Methods

        /// <summary>
        /// Создать новую директорию
        /// </summary>
        private void CreateDirectory()
        {
            var dialog = new InputDialog("Создание директории", "Введите код директории:");
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string code = dialog.InputText;
                    int newDirectoryId = _directoryService.CreateDirectory(code);

                    // Проверяем существование параметра перед автоматической привязкой
                    if (_modelBindingService.CheckParameterExists(_document))
                    {
                        // Автоматически привязываем модель к новой директории
                        _modelBindingService.BindModelToDirectory(_document, code);
                        BoundDirectoryId = newDirectoryId;
                        StatusMessage = $"Директория '{code}' создана и привязана";
                    }
                    else
                    {
                        StatusMessage = $"Директория '{code}' создана. Для привязки создайте параметр '#_Код площадки'";
                    }

                    LoadDirectories();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка создания:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Редактировать директорию
        /// </summary>
        private void EditDirectory()
        {
            if (SelectedDirectory == null) return;

            var dialog = new InputDialog("Редактирование директории", "Введите новый код директории:", SelectedDirectory.Code);
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string newCode = dialog.InputText;
                    string oldCode = SelectedDirectory.Code;
                    int directoryId = SelectedDirectory.Id;

                    _directoryService.UpdateDirectory(directoryId, newCode);

                    // Если редактируем привязанную директорию, обновляем параметр
                    if (BoundDirectoryId == directoryId)
                    {
                        _modelBindingService.BindModelToDirectory(_document, newCode);
                    }

                    LoadDirectories();
                    StatusMessage = $"Директория обновлена с '{oldCode}' на '{newCode}'";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка редактирования:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Удалить директорию
        /// </summary>
        private void DeleteDirectory()
        {
            if (SelectedDirectory == null) return;

            try
            {
                // Сохраняем данные ПЕРЕД удалением
                int directoryId = SelectedDirectory.Id;
                string directoryCode = SelectedDirectory.Code;

                bool deleted = _directoryService.DeleteDirectory(directoryId, directoryCode);
                if (deleted)
                {
                    // Если удалили привязанную директорию, отвязываем модель
                    if (BoundDirectoryId == directoryId)
                    {
                        _modelBindingService.UnbindModel(_document);
                        BoundDirectoryId = null;
                    }

                    LoadDirectories();
                    StatusMessage = $"Директория '{directoryCode}' удалена";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Привязать модель к выбранной директории
        /// </summary>
        private void BindDirectory()
        {
            if (SelectedDirectory == null) return;

            try
            {
                // Сохраняем данные ПЕРЕД привязкой
                int directoryId = SelectedDirectory.Id;
                string directoryCode = SelectedDirectory.Code;

                // Проверяем существование параметра
                if (!_modelBindingService.CheckParameterExists(_document))
                {
                    MessageBox.Show(
                        "Параметр '#_Код площадки' не найден в Сведениях о проекте.\n\n" +
                        "Создайте этот параметр в Сведениях о проекте для привязки модели.",
                        "Параметр не найден",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                _modelBindingService.BindModelToDirectory(_document, directoryCode);
                BoundDirectoryId = directoryId;
                LoadDirectories();
                StatusMessage = $"Модель привязана к директории '{directoryCode}'";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка привязки:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Выбрать оси в модели
        /// </summary>
        private void SelectAxis()
        {
            try
            {
                // Закрываем окно для выбора осей
                var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
                window?.Hide();

                // Запускаем команду выбора осей
                var selectCommand = new SelectAxisCommand();
                string message = string.Empty;
                ElementSet elements = new ElementSet();

                var result = selectCommand.Execute(_commandData, ref message, elements);

                // Показываем окно обратно
                window?.Show();

                if (result == Result.Succeeded)
                {
                    _selectedGrids = Commands.SelectAxisCommand.SelectedGrids;
                    SelectedAxisCount = _selectedGrids?.Count ?? 0;
                    StatusMessage = $"Выбрано осей: {SelectedAxisCount}";
                }
                else if (result == Result.Cancelled)
                {
                    StatusMessage = "Выбор осей отменен";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка выбора осей:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Отправить оси в БД как эталонные для директории
        /// </summary>
        private void SendAxis()
        {
            System.Diagnostics.Debug.WriteLine("\n=========================================");
            System.Diagnostics.Debug.WriteLine("=== НАЧАЛО SendAxis (отправка ЭТАЛОННЫХ осей) ===");
            System.Diagnostics.Debug.WriteLine("=========================================\n");

            System.Diagnostics.Debug.WriteLine($"[SendAxis] IsBound: {IsBound}");
            System.Diagnostics.Debug.WriteLine($"[SendAxis] BoundDirectoryId: {BoundDirectoryId}");
            System.Diagnostics.Debug.WriteLine($"[SendAxis] _selectedGrids: {_selectedGrids?.Count ?? 0}");

            if (!IsBound)
            {
                System.Diagnostics.Debug.WriteLine("[SendAxis] ✗ Модель не привязана");
                MessageBox.Show("Модель не привязана к директории", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_selectedGrids == null || _selectedGrids.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[SendAxis] ✗ Оси не выбраны");
                MessageBox.Show("Не выбраны оси", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"[SendAxis] >>> Сбор данных осей...");

                // Собираем данные осей
                var axisDataList = _axisCollectionService.CollectAxisData(_selectedGrids);
                System.Diagnostics.Debug.WriteLine($"[SendAxis] ✓ Собрано осей: {axisDataList.Count}");

                if (axisDataList.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[SendAxis] Первая ось: {axisDataList[0].AxisName} ({axisDataList[0].X1:F1}, {axisDataList[0].Y1:F1})");
                }

                // Валидация
                System.Diagnostics.Debug.WriteLine($"[SendAxis] >>> Валидация...");
                if (!_axisCollectionService.ValidateAxisData(axisDataList, out string errorMessage))
                {
                    System.Diagnostics.Debug.WriteLine($"[SendAxis] ✗ Ошибка валидации: {errorMessage}");
                    MessageBox.Show($"Ошибка валидации осей:\n{errorMessage}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                System.Diagnostics.Debug.WriteLine($"[SendAxis] ✓ Валидация пройдена");

                // Проверка дубликатов
                System.Diagnostics.Debug.WriteLine($"[SendAxis] >>> Проверка дубликатов...");
                var duplicates = _axisCollectionService.FindDuplicateAxisNames(axisDataList);
                if (duplicates.Count > 0)
                {
                    var duplicatesList = string.Join(", ", duplicates);
                    System.Diagnostics.Debug.WriteLine($"[SendAxis] ⚠ Найдены дубликаты: {duplicatesList}");

                    var result = MessageBox.Show(
                        $"Найдены оси с одинаковыми именами: {duplicatesList}\n\nПродолжить?",
                        "Предупреждение",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SendAxis] Отменено пользователем");
                        return;
                    }
                }
                System.Diagnostics.Debug.WriteLine($"[SendAxis] ✓ Проверка дубликатов завершена");

                // КЛЮЧЕВОЕ ИЗМЕНЕНИЕ: Получаем ID эталонной модели для директории
                System.Diagnostics.Debug.WriteLine($"\n[SendAxis] >>> Получение ID эталонной модели...");
                System.Diagnostics.Debug.WriteLine($"[SendAxis] BoundDirectoryId: {BoundDirectoryId.Value}");

                int referenceModelId = _databaseService.GetOrCreateReferenceModelId(BoundDirectoryId.Value);
                System.Diagnostics.Debug.WriteLine($"[SendAxis] ✓ ReferenceModelId получен: {referenceModelId}");
                System.Diagnostics.Debug.WriteLine($"[SendAxis] ℹ️ Оси будут сохранены как ЭТАЛОННЫЕ для директории");

                // Удаляем старые эталонные оси директории
                System.Diagnostics.Debug.WriteLine($"\n[SendAxis] >>> Удаление старых эталонных осей...");
                _databaseService.DeleteAxesByModelId(referenceModelId);
                System.Diagnostics.Debug.WriteLine($"[SendAxis] ✓ Старые эталонные оси удалены");

                // Добавляем новые эталонные оси
                System.Diagnostics.Debug.WriteLine($"\n[SendAxis] >>> Добавление новых эталонных осей...");
                System.Diagnostics.Debug.WriteLine($"[SendAxis] Вызов InsertAxes с {axisDataList.Count} осями");

                _databaseService.InsertAxes(referenceModelId, axisDataList);

                System.Diagnostics.Debug.WriteLine($"[SendAxis] ✓✓✓ InsertAxes выполнен успешно!");

                // Проверяем что вставилось
                System.Diagnostics.Debug.WriteLine($"\n[SendAxis] >>> Проверка вставки...");
                var insertedAxes = _databaseService.GetAxesByModelId(referenceModelId);
                System.Diagnostics.Debug.WriteLine($"[SendAxis] Проверка: в БД теперь {insertedAxes.Count} эталонных осей");

                if (insertedAxes.Count != axisDataList.Count)
                {
                    System.Diagnostics.Debug.WriteLine($"[SendAxis] ⚠⚠⚠ ВНИМАНИЕ! Несоответствие количества!");
                    System.Diagnostics.Debug.WriteLine($"[SendAxis] Отправлено: {axisDataList.Count}, В БД: {insertedAxes.Count}");
                }

                // Обновляем список директорий (для обновления количества осей)
                System.Diagnostics.Debug.WriteLine($"\n[SendAxis] >>> Обновление списка директорий...");
                LoadDirectories();
                System.Diagnostics.Debug.WriteLine($"[SendAxis] ✓ Список обновлен");

                System.Diagnostics.Debug.WriteLine($"\n=========================================");
                System.Diagnostics.Debug.WriteLine($"=== УСПЕХ! Отправлено ЭТАЛОННЫХ осей: {axisDataList.Count} ===");
                System.Diagnostics.Debug.WriteLine($"=========================================\n");

                MessageBox.Show(
                    $"Успешно отправлено эталонных осей: {axisDataList.Count}\n\n" +
                    $"Эти оси будут использоваться для проверки всех моделей в директории '{_directoryService.GetDirectoryById(BoundDirectoryId.Value)?.Code}'",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                StatusMessage = $"Отправлено эталонных осей: {axisDataList.Count}";

                // Сбрасываем выбор
                _selectedGrids = null;
                SelectedAxisCount = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"\n=========================================");
                System.Diagnostics.Debug.WriteLine($"=== ОШИБКА В SendAxis ===");
                System.Diagnostics.Debug.WriteLine($"=========================================");
                System.Diagnostics.Debug.WriteLine($"[SendAxis] Тип: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[SendAxis] Сообщение: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[SendAxis] StackTrace:\n{ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[SendAxis] InnerException: {ex.InnerException.Message}");
                }

                System.Diagnostics.Debug.WriteLine($"=========================================\n");

                MessageBox.Show($"Ошибка отправки осей:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Запустить проверку осей текущей модели с эталонными
        /// </summary>
        private void RunAxisCheck()
        {
            if (!IsBound)
            {
                MessageBox.Show(
                    "Модель не привязана к директории",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("\n=========================================");
                System.Diagnostics.Debug.WriteLine("=== НАЧАЛО RunAxisCheck (проверка осей модели) ===");
                System.Diagnostics.Debug.WriteLine("=========================================\n");

                System.Diagnostics.Debug.WriteLine($"[RunAxisCheck] Текущая модель: {ModelName}");
                System.Diagnostics.Debug.WriteLine($"[RunAxisCheck] Привязана к директории ID: {BoundDirectoryId.Value}");

                // КЛЮЧЕВОЕ ИЗМЕНЕНИЕ: Получаем эталонные оси по ID директории
                System.Diagnostics.Debug.WriteLine($"\n[RunAxisCheck] >>> Получение эталонных осей из директории...");
                var referenceAxes = _databaseService.GetReferenceAxesByDirectoryId(BoundDirectoryId.Value);
                System.Diagnostics.Debug.WriteLine($"[RunAxisCheck] ✓ Получено эталонных осей: {referenceAxes.Count}");

                if (referenceAxes.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[RunAxisCheck] ✗ Эталонные оси не найдены!");

                    MessageBox.Show(
                        "В базе данных нет эталонных осей для данной директории.\n\n" +
                        "Сначала отправьте эталонные оси в БД из модели-координации.",
                        "Нет эталонных осей",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // 2. Запустить проверку
                System.Diagnostics.Debug.WriteLine($"\n[RunAxisCheck] >>> Запуск проверки осей текущей модели...");
                var validationService = new AxisValidationService();
                var results = validationService.ValidateAllAxes(_document, referenceAxes);
                System.Diagnostics.Debug.WriteLine($"[RunAxisCheck] ✓ Проверка завершена, найдено ошибок: {results.Count(r => r.HasErrors)}");

                // Подсчитываем количество осей в модели
                var axisCollectionService = new AxisCollectionService();
                var modelGrids = axisCollectionService.GetAllGridsFromDocument(_document);
                int totalAxesInModel = modelGrids.Count;
                System.Diagnostics.Debug.WriteLine($"[RunAxisCheck] Всего осей в текущей модели: {totalAxesInModel}");
                System.Diagnostics.Debug.WriteLine($"[RunAxisCheck] Всего эталонных осей: {referenceAxes.Count}");

                // 3. Сохранить результаты в БД
                // Создаём запись для текущей модели (не эталонной!)
                var currentModelId = _databaseService.CreateOrUpdateModel(ModelName, BoundDirectoryId.Value);
                System.Diagnostics.Debug.WriteLine($"[RunAxisCheck] Текущая модель ID: {currentModelId}");

                var reportService = new ReportService(_databaseService);
                reportService.SaveCheckResults(currentModelId, CheckType.Manual, results, totalAxesInModel, referenceAxes.Count);
                System.Diagnostics.Debug.WriteLine($"[RunAxisCheck] ✓ Результаты сохранены в БД");

                // 4. Создать отчет
                var directoryCode = _modelBindingService.GetBoundDirectoryCode(_document);
                var report = reportService.GenerateReport(results, ModelName, directoryCode, totalAxesInModel, referenceAxes.Count);

                System.Diagnostics.Debug.WriteLine($"\n=========================================");
                System.Diagnostics.Debug.WriteLine($"=== ПРОВЕРКА ЗАВЕРШЕНА ===");
                System.Diagnostics.Debug.WriteLine($"=========================================\n");

                // 5. Показать окно с результатами
                var viewModel = new CheckResultsViewModel(report, _uiDocument);
                var window = new Views.CheckResultsWindow
                {
                    DataContext = viewModel
                };
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"\n[RunAxisCheck] ✗✗✗ ОШИБКА: {ex.Message}");

                MessageBox.Show(
                    $"Ошибка при проверке осей:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Загрузить список директорий из БД
        /// </summary>
        private void LoadDirectories()
        {
            try
            {
                var directories = _directoryService.GetAllDirectoriesWithBinding(BoundDirectoryId);
                Directories = new ObservableCollection<Directory>(directories);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки директорий:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Directories = new ObservableCollection<Directory>();
            }
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

    #region Helper Dialog

    /// <summary>
    /// Простой диалог ввода текста
    /// </summary>
    public class InputDialog : Window
    {
        public string InputText { get; private set; }

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            Title = title;
            Width = 400;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(10) };

            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 10) });

            var textBox = new System.Windows.Controls.TextBox { Text = defaultValue };
            panel.Children.Add(textBox);

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 10, 0) };
            okButton.Click += (s, e) =>
            {
                InputText = textBox.Text;
                DialogResult = true;
                Close();
            };

            var cancelButton = new System.Windows.Controls.Button { Content = "Отмена", Width = 75 };
            cancelButton.Click += (s, e) =>
            {
                DialogResult = false;
                Close();
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            panel.Children.Add(buttonPanel);

            Content = panel;
        }
    }

    #endregion
}