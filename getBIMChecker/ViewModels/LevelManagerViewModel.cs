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
using getBIMChecker.Events;
using getBIMChecker.Models;
using getBIMChecker.Services;

namespace getBIMChecker.ViewModels
{
    public class LevelManagerViewModel : INotifyPropertyChanged
    {
        private readonly ExternalCommandData _commandData;
        private readonly Document _document;
        private readonly UIDocument _uiDocument;

        private readonly DatabaseService _databaseService;
        private readonly DirectoryService _directoryService;
        private readonly ModelBindingService _modelBindingService;
        private readonly LevelCollectionService _levelCollectionService;

        private readonly LevelManagerEventHandler _eventHandler;
        private readonly ExternalEvent _externalEvent;
        private readonly LevelFixEventHandler _fixEventHandler;
        private readonly ExternalEvent _fixExternalEvent;

        private ObservableCollection<Directory> _directories;
        private Directory _selectedDirectory;
        private int? _boundDirectoryId;
        private string _modelName;
        private int _selectedLevelCount;
        private List<Level> _selectedLevels;
        private string _statusMessage;
        private Views.LevelCheckResultsWindow _checkResultsWindow;

        public ObservableCollection<Directory> Directories
        {
            get => _directories;
            set { _directories = value; OnPropertyChanged(nameof(Directories)); }
        }

        public Directory SelectedDirectory
        {
            get => _selectedDirectory;
            set { _selectedDirectory = value; OnPropertyChanged(nameof(SelectedDirectory)); }
        }

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

        public bool IsBound => _boundDirectoryId.HasValue;
        public string ModelName { get => _modelName; set { _modelName = value; OnPropertyChanged(nameof(ModelName)); } }
        public int SelectedLevelCount { get => _selectedLevelCount; set { _selectedLevelCount = value; OnPropertyChanged(nameof(SelectedLevelCount)); OnPropertyChanged(nameof(LevelCountText)); } }
        public string LevelCountText => SelectedLevelCount > 0 ? $"Выбрано уровней: {SelectedLevelCount}" : "Уровни не выбраны";
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); } }

        public ICommand CreateDirectoryCommand { get; }
        public ICommand EditDirectoryCommand { get; }
        public ICommand DeleteDirectoryCommand { get; }
        public ICommand BindDirectoryCommand { get; }
        public ICommand SelectLevelCommand { get; }
        public ICommand SendLevelCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand RunLevelCheckCommand { get; }

        public LevelManagerViewModel(
            ExternalCommandData commandData,
            LevelManagerEventHandler managerEventHandler,
            ExternalEvent managerExternalEvent,
            LevelFixEventHandler fixEventHandler,
            ExternalEvent fixExternalEvent)
        {
            _commandData = commandData;
            _uiDocument = commandData.Application.ActiveUIDocument;
            _document = _uiDocument.Document;

            _eventHandler = managerEventHandler;
            _externalEvent = managerExternalEvent;
            _fixEventHandler = fixEventHandler;
            _fixExternalEvent = fixExternalEvent;

            _eventHandler.Document = _document;
            _eventHandler.OnBindCompleted = OnBindCompleted;
            _eventHandler.OnUnbindCompleted = OnUnbindCompleted;

            var dbSettings = DatabaseSettings.Default;
            if (!DatabaseInitializer.Initialize(dbSettings))
            {
                StatusMessage = "Ошибка подключения к БД";
                return;
            }

            _databaseService = new DatabaseService(dbSettings);
            _directoryService = new DirectoryService(_databaseService);
            _modelBindingService = new ModelBindingService();
            _levelCollectionService = new LevelCollectionService();

            CreateDirectoryCommand = new RelayCommand(CreateDirectory);
            EditDirectoryCommand = new RelayCommand(EditDirectory, () => SelectedDirectory != null);
            DeleteDirectoryCommand = new RelayCommand(DeleteDirectory, () => SelectedDirectory != null);
            BindDirectoryCommand = new RelayCommand(BindDirectory, () => SelectedDirectory != null);
            SelectLevelCommand = new RelayCommand(SelectLevel);
            SendLevelCommand = new RelayCommand(SendLevel, () => SelectedLevelCount > 0 && IsBound);
            RefreshCommand = new RelayCommand(LoadDirectories);
            RunLevelCheckCommand = new RelayCommand(RunLevelCheck);

            ModelName = _modelBindingService.GetModelName(_document);

            string boundDirectoryCode = _modelBindingService.GetBoundDirectoryCode(_document);
            if (!string.IsNullOrWhiteSpace(boundDirectoryCode))
            {
                var boundDirectory = _directoryService.GetDirectoryByCode(boundDirectoryCode);
                BoundDirectoryId = boundDirectory?.Id;
            }

            LoadDirectories();
            StatusMessage = IsBound ? "Модель привязана к директории" : "Модель не привязана";
        }

        private void OnBindCompleted(bool success, string error)
        {
            if (success)
            {
                LoadDirectories();
                StatusMessage = $"Модель привязана к директории '{_eventHandler.DirectoryCode}'";
            }
            else
            {
                BoundDirectoryId = null;
                MessageBox.Show($"Ошибка привязки:\n{error}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnUnbindCompleted(bool success, string error)
        {
            if (success)
            {
                BoundDirectoryId = null;
                LoadDirectories();
                StatusMessage = "Модель отвязана от директории";
            }
            else
                MessageBox.Show($"Ошибка отвязки:\n{error}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void CreateDirectory()
        {
            var dialog = new InputDialog("Создание директории", "Введите код директории:");
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string code = dialog.InputText;
                    int newDirectoryId = _directoryService.CreateDirectory(code);

                    if (_modelBindingService.CheckParameterExists(_document))
                    {
                        BoundDirectoryId = newDirectoryId;
                        _eventHandler.CurrentAction = LevelManagerAction.BindDirectory;
                        _eventHandler.DirectoryCode = code;
                        _externalEvent.Raise();
                        StatusMessage = $"Директория '{code}' создана, выполняется привязка...";
                    }
                    else
                    {
                        StatusMessage = $"Директория '{code}' создана";
                        LoadDirectories();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка создания:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

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

                    if (BoundDirectoryId == directoryId)
                    {
                        _eventHandler.CurrentAction = LevelManagerAction.BindDirectory;
                        _eventHandler.DirectoryCode = newCode;
                        _externalEvent.Raise();
                    }
                    else
                        LoadDirectories();

                    StatusMessage = $"Директория обновлена с '{oldCode}' на '{newCode}'";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка редактирования:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteDirectory()
        {
            if (SelectedDirectory == null) return;

            try
            {
                int directoryId = SelectedDirectory.Id;
                string directoryCode = SelectedDirectory.Code;

                bool deleted = _directoryService.DeleteDirectory(directoryId, directoryCode);
                if (deleted)
                {
                    if (BoundDirectoryId == directoryId)
                    {
                        _eventHandler.CurrentAction = LevelManagerAction.UnbindDirectory;
                        _externalEvent.Raise();
                    }
                    else
                        LoadDirectories();

                    StatusMessage = $"Директория '{directoryCode}' удалена";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BindDirectory()
        {
            if (SelectedDirectory == null) return;

            try
            {
                if (!_modelBindingService.CheckParameterExists(_document))
                {
                    MessageBox.Show("Параметр '#_Код площадки' не найден в Сведениях о проекте.", "Параметр не найден", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                BoundDirectoryId = SelectedDirectory.Id;
                _eventHandler.CurrentAction = LevelManagerAction.BindDirectory;
                _eventHandler.DirectoryCode = SelectedDirectory.Code;
                _externalEvent.Raise();
                StatusMessage = "Выполняется привязка...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка привязки:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectLevel()
        {
            try
            {
                var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
                window?.Hide();

                var selectCommand = new SelectLevelCommand();
                string message = string.Empty;
                ElementSet elements = new ElementSet();

                var result = selectCommand.Execute(_commandData, ref message, elements);

                window?.Show();

                if (result == Result.Succeeded)
                {
                    _selectedLevels = Commands.SelectLevelCommand.SelectedLevels;
                    SelectedLevelCount = _selectedLevels?.Count ?? 0;
                    StatusMessage = $"Выбрано уровней: {SelectedLevelCount}";
                }
                else if (result == Result.Cancelled)
                    StatusMessage = "Выбор уровней отменен";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка выбора уровней:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SendLevel()
        {
            if (!IsBound || _selectedLevels == null || _selectedLevels.Count == 0) return;

            try
            {
                var levelDataList = _levelCollectionService.CollectLevelData(_selectedLevels);

                if (!_levelCollectionService.ValidateLevelData(levelDataList, out string errorMessage))
                {
                    MessageBox.Show($"Ошибка валидации:\n{errorMessage}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var duplicates = _levelCollectionService.FindDuplicateLevelNames(levelDataList);
                if (duplicates.Count > 0)
                {
                    var result = MessageBox.Show($"Найдены уровни с одинаковыми именами: {string.Join(", ", duplicates)}\n\nПродолжить?", "Предупреждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.No) return;
                }

                int referenceModelId = _databaseService.GetOrCreateLevelReferenceModelId(BoundDirectoryId.Value);
                _databaseService.DeleteLevelsByModelId(referenceModelId);
                _databaseService.InsertLevels(referenceModelId, levelDataList);

                LoadDirectories();

                MessageBox.Show($"Успешно отправлено эталонных уровней: {levelDataList.Count}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusMessage = $"Отправлено уровней: {levelDataList.Count}";

                _selectedLevels = null;
                SelectedLevelCount = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отправки:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RunLevelCheck()
        {
            if (!IsBound)
            {
                MessageBox.Show("Модель не привязана к директории", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var referenceLevels = _databaseService.GetReferenceLevelsByDirectoryId(BoundDirectoryId.Value);

                if (referenceLevels.Count == 0)
                {
                    MessageBox.Show("В базе данных нет эталонных уровней для данной директории.", "Нет эталонных уровней", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var validationService = new LevelValidationService();
                var results = validationService.ValidateAllLevels(_document, referenceLevels);

                var modelLevels = _levelCollectionService.GetAllLevelsFromDocument(_document);
                int totalLevelsInModel = modelLevels.Count;

                var currentModelId = _databaseService.CreateOrUpdateModel(ModelName, BoundDirectoryId.Value);

                var reportService = new LevelReportService(_databaseService);
                reportService.SaveCheckResults(currentModelId, CheckType.Manual, results, totalLevelsInModel, referenceLevels.Count);

                var directoryCode = _modelBindingService.GetBoundDirectoryCode(_document);
                var report = reportService.GenerateReport(results, ModelName, directoryCode, totalLevelsInModel, referenceLevels.Count);

                if (_checkResultsWindow != null && _checkResultsWindow.IsVisible)
                    _checkResultsWindow.Close();

                var viewModel = new LevelCheckResultsViewModel(report, _uiDocument, _fixEventHandler, _fixExternalEvent);
                _checkResultsWindow = new Views.LevelCheckResultsWindow { DataContext = viewModel };
                _checkResultsWindow.Show();

                StatusMessage = $"Проверка завершена. Ошибок: {report.ErrorCount}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке уровней:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}