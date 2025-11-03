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
                    MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    
                    _directoryService.UpdateDirectory(SelectedDirectory.Id, newCode);
                    
                    // Если редактируем привязанную директорию, обновляем параметр
                    if (BoundDirectoryId == SelectedDirectory.Id)
                    {
                        _modelBindingService.BindModelToDirectory(_document, newCode);
                    }
                    
                    LoadDirectories();
                    StatusMessage = $"Директория обновлена с '{oldCode}' на '{newCode}'";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                bool deleted = _directoryService.DeleteDirectory(SelectedDirectory.Id, SelectedDirectory.Code);
                if (deleted)
                {
                    // Если удалили привязанную директорию, отвязываем модель
                    if (BoundDirectoryId == SelectedDirectory.Id)
                    {
                        _modelBindingService.UnbindModel(_document);
                        BoundDirectoryId = null;
                    }

                    LoadDirectories();
                    StatusMessage = $"Директория '{SelectedDirectory.Code}' удалена";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

                _modelBindingService.BindModelToDirectory(_document, SelectedDirectory.Code);
                BoundDirectoryId = SelectedDirectory.Id;
                LoadDirectories();
                StatusMessage = $"Модель привязана к директории '{SelectedDirectory.Code}'";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Ошибка выбора осей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Отправить оси в БД
        /// </summary>
        private void SendAxis()
        {
            if (!IsBound)
            {
                MessageBox.Show("Модель не привязана к директории", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_selectedGrids == null || _selectedGrids.Count == 0)
            {
                MessageBox.Show("Не выбраны оси", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Собираем данные осей
                var axisDataList = _axisCollectionService.CollectAxisData(_selectedGrids);

                // Валидация
                if (!_axisCollectionService.ValidateAxisData(axisDataList, out string errorMessage))
                {
                    MessageBox.Show($"Ошибка валидации осей:\n{errorMessage}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Проверка дубликатов
                var duplicates = _axisCollectionService.FindDuplicateAxisNames(axisDataList);
                if (duplicates.Count > 0)
                {
                    var duplicatesList = string.Join(", ", duplicates);
                    var result = MessageBox.Show(
                        $"Найдены оси с одинаковыми именами: {duplicatesList}\n\nПродолжить?",
                        "Предупреждение",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                        return;
                }

                // Создаем или обновляем модель в БД
                int modelId = _databaseService.CreateOrUpdateModel(ModelName, BoundDirectoryId.Value);

                // Удаляем старые оси модели
                _databaseService.DeleteAxesByModelId(modelId);

                // Добавляем новые оси
                _databaseService.InsertAxes(modelId, axisDataList);

                // Обновляем список директорий (для обновления количества осей)
                LoadDirectories();

                MessageBox.Show($"Успешно отправлено осей: {axisDataList.Count}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusMessage = $"Отправлено осей: {axisDataList.Count}";

                // Сбрасываем выбор
                _selectedGrids = null;
                SelectedAxisCount = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отправки осей:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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