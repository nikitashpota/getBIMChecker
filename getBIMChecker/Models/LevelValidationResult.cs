using System.Collections.Generic;
using System.Linq;

namespace getBIMChecker.Models
{
    public class LevelValidationResult
    {
        public string LevelName { get; set; }
        public long? ElementId { get; set; }
        public List<LevelErrorType> ErrorTypes { get; set; }
        public double? DeviationMm { get; set; }
        public bool? IsPinned { get; set; }
        public string WorksetName { get; set; }
        public LevelData ModelLevel { get; set; }
        public LevelData ReferenceLevel { get; set; }
        public bool HasErrors => ErrorTypes != null && ErrorTypes.Count > 0;

        public LevelValidationResult() { ErrorTypes = new List<LevelErrorType>(); }

        public string GetErrorTypesString()
        {
            if (ErrorTypes == null || ErrorTypes.Count == 0) return "-";
            return string.Join("; ", ErrorTypes.Select(GetErrorTypeName));
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

        public string GetPinnedString() => IsPinned.HasValue ? (IsPinned.Value ? "Да" : "Нет") : "-";
        public string GetDeviationString() => DeviationMm.HasValue ? DeviationMm.Value.ToString("F2") : "-";
    }
}