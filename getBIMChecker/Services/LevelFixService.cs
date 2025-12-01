using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using getBIMChecker.Models;

namespace getBIMChecker.Services
{
    public class LevelFixService
    {
        private const string EXPECTED_WORKSET_NAME = "#_Общие уровни и сетки";
        private const double FEET_PER_MM = 1.0 / 304.8;

        public LevelFixResult FixLevel(Document doc, LevelValidationResult error)
        {
            var result = new LevelFixResult
            {
                LevelName = error.LevelName,
                Success = true,
                FixedErrors = new List<LevelErrorType>(),
                FailedErrors = new List<string>()
            };

            try
            {
                using (var trans = new Transaction(doc, $"Исправление уровня {error.LevelName}"))
                {
                    trans.Start();
                    try
                    {
                        foreach (var errorType in error.ErrorTypes)
                        {
                            try
                            {
                                bool fixed_ = FixSingleError(doc, error, errorType);
                                if (fixed_) result.FixedErrors.Add(errorType);
                                else result.FailedErrors.Add($"{GetErrorTypeName(errorType)}: не удалось исправить");
                            }
                            catch (Exception ex)
                            {
                                result.FailedErrors.Add($"{GetErrorTypeName(errorType)}: {ex.Message}");
                            }
                        }
                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();
                        result.Success = false;
                        result.FailedErrors.Add($"Транзакция отменена: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.FailedErrors.Add($"Критическая ошибка: {ex.Message}");
            }

            result.Success = result.FailedErrors.Count == 0;
            return result;
        }

        private bool FixSingleError(Document doc, LevelValidationResult error, LevelErrorType errorType)
        {
            return errorType switch
            {
                LevelErrorType.ElevationDeviation => FixLevelElevation(doc, error),
                LevelErrorType.NotPinned => FixLevelPinning(doc, error),
                LevelErrorType.WrongWorkset => FixLevelWorkset(doc, error),
                LevelErrorType.NotInModel => CreateMissingLevel(doc, error),
                LevelErrorType.NotInReference => false,
                _ => false
            };
        }

        private bool FixLevelElevation(Document doc, LevelValidationResult error)
        {
            if (!error.ElementId.HasValue || error.ReferenceLevel == null) return false;

            var level = doc.GetElement(new ElementId((int)error.ElementId.Value)) as Level;
            if (level == null) return false;

            bool wasPinned = level.Pinned;
            if (wasPinned) level.Pinned = false;

            level.Elevation = error.ReferenceLevel.Elevation * FEET_PER_MM;

            if (wasPinned) level.Pinned = true;
            return true;
        }

        private bool FixLevelPinning(Document doc, LevelValidationResult error)
        {
            if (!error.ElementId.HasValue) return false;
            var level = doc.GetElement(new ElementId((int)error.ElementId.Value)) as Level;
            if (level == null) return false;
            level.Pinned = true;
            return true;
        }

        private bool FixLevelWorkset(Document doc, LevelValidationResult error)
        {
            if (!error.ElementId.HasValue || !doc.IsWorkshared) return false;

            var level = doc.GetElement(new ElementId((int)error.ElementId.Value)) as Level;
            if (level == null) return false;

            var targetWorkset = FindOrCreateWorkset(doc, EXPECTED_WORKSET_NAME);
            if (targetWorkset == null) return false;

            var worksetParam = level.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
            if (worksetParam == null || worksetParam.IsReadOnly) return false;

            worksetParam.Set(targetWorkset.Id.IntegerValue);
            return true;
        }

        private bool CreateMissingLevel(Document doc, LevelValidationResult error)
        {
            if (error.ReferenceLevel == null) return false;

            var newLevel = Level.Create(doc, error.ReferenceLevel.Elevation * FEET_PER_MM);
            if (newLevel == null) return false;

            try { newLevel.Name = error.ReferenceLevel.LevelName; } catch { }
            newLevel.Pinned = true;

            if (doc.IsWorkshared)
            {
                var targetWorkset = FindOrCreateWorkset(doc, EXPECTED_WORKSET_NAME);
                if (targetWorkset != null)
                {
                    var worksetParam = newLevel.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                    if (worksetParam != null && !worksetParam.IsReadOnly)
                        worksetParam.Set(targetWorkset.Id.IntegerValue);
                }
            }

            error.ElementId = newLevel.Id.IntegerValue;
            return true;
        }

        private Workset FindOrCreateWorkset(Document doc, string worksetName)
        {
            if (!doc.IsWorkshared) return null;

            var collector = new FilteredWorksetCollector(doc);
            foreach (var ws in collector.OfKind(WorksetKind.UserWorkset).ToWorksets())
                if (ws.Name == worksetName) return ws;

            if (WorksetTable.IsWorksetNameUnique(doc, worksetName))
                return Workset.Create(doc, worksetName);

            return null;
        }

        private string GetErrorTypeName(LevelErrorType type) => type switch
        {
            LevelErrorType.ElevationDeviation => "Отклонение отметки",
            LevelErrorType.NotPinned => "Не закреплен",
            LevelErrorType.WrongWorkset => "Неправильный рабочий набор",
            LevelErrorType.NotInReference => "Отсутствует в эталоне",
            LevelErrorType.NotInModel => "Отсутствует в модели",
            _ => "Неизвестная ошибка"
        };
    }

    public class LevelFixResult
    {
        public string LevelName { get; set; }
        public bool Success { get; set; }
        public List<LevelErrorType> FixedErrors { get; set; }
        public List<string> FailedErrors { get; set; }

        public string GetSummary() => Success
            ? $"Уровень '{LevelName}': исправлено - {FixedErrors.Count}"
            : $"Уровень '{LevelName}': {string.Join("; ", FailedErrors)}";
    }
}