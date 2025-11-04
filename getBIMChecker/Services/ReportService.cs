using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using getBIMChecker.Models;

namespace getBIMChecker.Services
{
    /// <summary>
    /// Сервис формирования отчетов о проверке осей
    /// </summary>
    public class ReportService
    {
        private readonly DatabaseService _databaseService;

        public ReportService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// Создание отчета проверки
        /// </summary>
        public CheckReport GenerateReport(
            List<AxisValidationResult> results,
            string modelName,
            string directoryCode,
            int totalAxesInModel,
            int totalReferenceAxes)
        {
            var report = new CheckReport
            {
                ModelName = modelName,
                DirectoryCode = directoryCode,
                CheckType = CheckType.Manual,
                CheckDate = DateTime.Now,
                TotalAxesInModel = totalAxesInModel,
                TotalReferenceAxes = totalReferenceAxes,
                Errors = results.Where(r => r.HasErrors).ToList(),
                ErrorCount = results.Count(r => r.HasErrors)
            };

            return report;
        }

        /// <summary>
        /// Сохранение результатов проверки в БД
        /// </summary>
        public int SaveCheckResults(int modelId, CheckType checkType, List<AxisValidationResult> results, int totalAxesInModel, int totalReferenceAxes)
        {
            try
            {
                // Подсчитываем количество осей с ошибками
                int errorCount = results.Count(r => r.HasErrors);

                // Сохраняем основную запись о проверке
                int checkResultId = _databaseService.SaveCheckResult(
                    modelId,
                    checkType,
                    totalAxesInModel,
                    totalReferenceAxes,
                    errorCount);

                // Сохраняем детали ошибок
                var errors = results.Where(r => r.HasErrors).ToList();
                if (errors.Count > 0)
                {
                    _databaseService.SaveAxisErrors(checkResultId, errors);
                }

                return checkResultId;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка сохранения результатов проверки: {ex.Message}");
            }
        }

        /// <summary>
        /// Экспорт отчета в CSV файл
        /// </summary>
        public void ExportToCsv(CheckReport report, string filePath)
        {
            try
            {
                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    // Записываем заголовок
                    writer.WriteLine("ElementId;Имя оси;Тип ошибки;Смещение (мм);Закреплена;Рабочий набор");

                    // Записываем данные
                    foreach (var error in report.Errors)
                    {
                        string elementId = error.ElementId?.ToString() ?? "NULL";
                        string axisName = EscapeCsvField(error.AxisName);
                        string errorTypes = EscapeCsvField(error.GetErrorTypesString());
                        string deviation = error.DeviationMm.HasValue ? error.DeviationMm.Value.ToString("F4") : "-";
                        string pinned = error.GetPinnedString();
                        string workset = EscapeCsvField(error.WorksetName ?? "-");

                        writer.WriteLine($"{elementId};{axisName};{errorTypes};{deviation};{pinned};{workset}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка экспорта в CSV: {ex.Message}");
            }
        }

        /// <summary>
        /// Экранирование поля CSV (если содержит точку с запятой, кавычки или перенос строки)
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return field;

            if (field.Contains(";") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                // Экранируем кавычки
                field = field.Replace("\"", "\"\"");
                // Оборачиваем в кавычки
                return $"\"{field}\"";
            }

            return field;
        }

        /// <summary>
        /// Получить историю проверок модели
        /// </summary>
        public List<CheckReport> GetCheckHistory(int modelId, DateTime? from = null, DateTime? to = null)
        {
            try
            {
                return _databaseService.GetCheckHistory(modelId, from, to);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка получения истории проверок: {ex.Message}");
            }
        }

        /// <summary>
        /// Форматировать статистику ошибок для отображения
        /// </summary>
        public string FormatErrorStatistics(CheckReport report)
        {
            var stats = report.GetErrorStatistics();
            var lines = new List<string>();

            foreach (var stat in stats.OrderByDescending(s => s.Value))
            {
                string errorName = GetErrorTypeName(stat.Key);
                lines.Add($"  • {errorName}: {stat.Value}");
            }

            return string.Join("\n", lines);
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
    }
}