using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using getBIMChecker.Models;

namespace getBIMChecker.Services
{
    /// <summary>
    /// Сервис автоматической проверки осей при синхронизации с Revit Server
    /// </summary>
    public class AutomaticCheckService
    {
        // ДЛЯ ДЕБАГА: 1 секунда. Потом изменить на 24 часа (24 * 60 * 60 = 86400)
        private const int CHECK_INTERVAL_SECONDS = 24 * 60 * 60;

        // Для продакшена раскомментировать:
        // private const int CHECK_INTERVAL_SECONDS = 86400; // 24 часа

        private readonly DatabaseService _databaseService;
        private readonly ModelBindingService _modelBindingService;
        private readonly AxisValidationService _validationService;
        private readonly AxisCollectionService _axisCollectionService;
        private readonly ReportService _reportService;

        public AutomaticCheckService()
        {
            System.Diagnostics.Debug.WriteLine("[AutoCheck] >>> Инициализация AutomaticCheckService");

            var dbSettings = DatabaseSettings.Default;
            _databaseService = new DatabaseService(dbSettings);
            _modelBindingService = new ModelBindingService();
            _validationService = new AxisValidationService();
            _axisCollectionService = new AxisCollectionService();
            _reportService = new ReportService(_databaseService);

            System.Diagnostics.Debug.WriteLine($"[AutoCheck] ✓ Интервал проверки: {CHECK_INTERVAL_SECONDS} секунд");
        }

        /// <summary>
        /// Обработчик события синхронизации с центральной моделью
        /// </summary>
        public void OnDocumentSynchronizedWithCentral(object sender, DocumentSynchronizedWithCentralEventArgs e)
        {
            try
            {
                Document doc = e.Document;

                System.Diagnostics.Debug.WriteLine("\n=========================================");
                System.Diagnostics.Debug.WriteLine("=== СОБЫТИЕ: Синхронизация с центральной моделью ===");
                System.Diagnostics.Debug.WriteLine("=========================================");
                System.Diagnostics.Debug.WriteLine($"[AutoCheck] Модель: {doc.Title}");
                System.Diagnostics.Debug.WriteLine($"[AutoCheck] Путь: {doc.PathName}");

                // Проверяем, что модель в режиме совместной работы
                if (!doc.IsWorkshared)
                {
                    System.Diagnostics.Debug.WriteLine("[AutoCheck] ⚠ Модель НЕ в режиме совместной работы - пропускаем");
                    return;
                }

                // Шаг 1: Проверяем привязку к директории
                string directoryCode = _modelBindingService.GetBoundDirectoryCode(doc);

                if (string.IsNullOrWhiteSpace(directoryCode))
                {
                    System.Diagnostics.Debug.WriteLine("[AutoCheck] ⚠ Модель НЕ привязана к директории (параметр '#_Код площадки' пуст)");
                    System.Diagnostics.Debug.WriteLine("[AutoCheck] Автоматическая проверка НЕ выполняется");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[AutoCheck] ✓ Модель привязана к директории: '{directoryCode}'");

                // Шаг 2: Получаем ID директории
                var directoryService = new DirectoryService(_databaseService);
                var directory = directoryService.GetDirectoryByCode(directoryCode);

                if (directory == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[AutoCheck] ✗ Директория '{directoryCode}' не найдена в БД");
                    return;
                }

                int directoryId = directory.Id;
                System.Diagnostics.Debug.WriteLine($"[AutoCheck] ✓ Directory ID: {directoryId}");

                // Шаг 3: Получаем имя модели
                string modelName = _modelBindingService.GetModelName(doc);
                System.Diagnostics.Debug.WriteLine($"[AutoCheck] ✓ Имя модели: '{modelName}'");

                // Шаг 4: Получаем или создаем ID модели в БД
                int modelId = _databaseService.CreateOrUpdateModel(modelName, directoryId);
                System.Diagnostics.Debug.WriteLine($"[AutoCheck] ✓ Model ID в БД: {modelId}");

                // Шаг 5: Проверяем время последней проверки
                DateTime? lastCheckTime = _databaseService.GetLastCheckTime(modelId);

                if (lastCheckTime.HasValue)
                {
                    TimeSpan timeSinceLastCheck = DateTime.Now - lastCheckTime.Value;
                    System.Diagnostics.Debug.WriteLine($"[AutoCheck] Последняя проверка: {lastCheckTime.Value:dd.MM.yyyy HH:mm:ss}");
                    System.Diagnostics.Debug.WriteLine($"[AutoCheck] Прошло времени: {timeSinceLastCheck.TotalSeconds:F1} секунд");

                    if (timeSinceLastCheck.TotalSeconds < CHECK_INTERVAL_SECONDS)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AutoCheck] ⏰ Проверка была недавно (менее {CHECK_INTERVAL_SECONDS} сек назад)");
                        System.Diagnostics.Debug.WriteLine("[AutoCheck] Автоматическая проверка ПРОПУЩЕНА");
                        return;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[AutoCheck] Это первая проверка модели");
                }

                System.Diagnostics.Debug.WriteLine($"[AutoCheck] ✓ Условия для проверки выполнены - запускаем проверку!");

                // Шаг 6: Запускаем проверку осей
                PerformAxisCheck(doc, directoryId, modelId, modelName, directoryCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"\n[AutoCheck] ✗✗✗ КРИТИЧЕСКАЯ ОШИБКА:");
                System.Diagnostics.Debug.WriteLine($"[AutoCheck] {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[AutoCheck] StackTrace:\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Выполнить проверку осей модели
        /// </summary>
        private void PerformAxisCheck(Document doc, int directoryId, int modelId, string modelName, string directoryCode)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("\n[AutoCheck] >>> Начало автоматической проверки осей...");

                // 1. Получаем эталонные оси директории
                System.Diagnostics.Debug.WriteLine($"[AutoCheck] Загрузка эталонных осей директории ID={directoryId}...");
                var referenceAxes = _databaseService.GetReferenceAxesByDirectoryId(directoryId);

                if (referenceAxes.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[AutoCheck] ⚠ Эталонные оси не найдены - проверка невозможна");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[AutoCheck] ✓ Загружено эталонных осей: {referenceAxes.Count}");

                // 2. Выполняем проверку
                System.Diagnostics.Debug.WriteLine("[AutoCheck] Выполнение проверки осей модели...");
                var results = _validationService.ValidateAllAxes(doc, referenceAxes);

                int errorsFound = results.Count(r => r.HasErrors);
                System.Diagnostics.Debug.WriteLine($"[AutoCheck] ✓ Проверка завершена. Найдено ошибок: {errorsFound}");

                // 3. Подсчитываем количество осей в модели
                var modelGrids = _axisCollectionService.GetAllGridsFromDocument(doc);
                int totalAxesInModel = modelGrids.Count;
                System.Diagnostics.Debug.WriteLine($"[AutoCheck] Всего осей в модели: {totalAxesInModel}");

                // 4. Сохраняем результаты в БД
                System.Diagnostics.Debug.WriteLine("[AutoCheck] Сохранение результатов в БД...");
                _reportService.SaveCheckResults(
                    modelId,
                    CheckType.Auto,  // АВТОМАТИЧЕСКАЯ проверка
                    results,
                    totalAxesInModel,
                    referenceAxes.Count);

                System.Diagnostics.Debug.WriteLine($"[AutoCheck] ✓✓✓ Автоматическая проверка успешно завершена!");
                System.Diagnostics.Debug.WriteLine($"[AutoCheck] Модель: {modelName}");
                System.Diagnostics.Debug.WriteLine($"[AutoCheck] Директория: {directoryCode}");
                System.Diagnostics.Debug.WriteLine($"[AutoCheck] Осей проверено: {totalAxesInModel}");
                System.Diagnostics.Debug.WriteLine($"[AutoCheck] Ошибок найдено: {errorsFound}");
                System.Diagnostics.Debug.WriteLine("=========================================\n");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"\n[AutoCheck] ✗ ОШИБКА при проверке осей:");
                System.Diagnostics.Debug.WriteLine($"[AutoCheck] {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[AutoCheck] StackTrace:\n{ex.StackTrace}");
            }
        }
    }
}
