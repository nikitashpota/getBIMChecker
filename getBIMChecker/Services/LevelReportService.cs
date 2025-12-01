using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using getBIMChecker.Models;

namespace getBIMChecker.Services
{
    public class LevelReportService
    {
        private readonly DatabaseService _databaseService;

        public LevelReportService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public LevelCheckReport GenerateReport(
            List<LevelValidationResult> results,
            string modelName,
            string directoryCode,
            int totalLevelsInModel,
            int totalReferenceLevels)
        {
            return new LevelCheckReport
            {
                ModelName = modelName,
                DirectoryCode = directoryCode,
                CheckType = CheckType.Manual,
                CheckDate = DateTime.Now,
                TotalLevelsInModel = totalLevelsInModel,
                TotalReferenceLevels = totalReferenceLevels,
                Errors = results.Where(r => r.HasErrors).ToList(),
                ErrorCount = results.Count(r => r.HasErrors)
            };
        }

        public int SaveCheckResults(int modelId, CheckType checkType, List<LevelValidationResult> results, int totalLevelsInModel, int totalReferenceLevels)
        {
            int errorCount = results.Count(r => r.HasErrors);
            int checkResultId = _databaseService.SaveLevelCheckResult(modelId, checkType, totalLevelsInModel, totalReferenceLevels, errorCount);

            var errors = results.Where(r => r.HasErrors).ToList();
            if (errors.Count > 0)
                _databaseService.SaveLevelErrors(checkResultId, errors);

            return checkResultId;
        }

        public void ExportToCsv(LevelCheckReport report, string filePath)
        {
            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                writer.WriteLine("ElementId;Имя уровня;Тип ошибки;Отклонение (мм);Закреплен;Рабочий набор");
                foreach (var error in report.Errors)
                {
                    writer.WriteLine($"{error.ElementId?.ToString() ?? "NULL"};{error.LevelName};{error.GetErrorTypesString()};{error.GetDeviationString()};{error.GetPinnedString()};{error.WorksetName ?? "-"}");
                }
            }
        }
    }
}