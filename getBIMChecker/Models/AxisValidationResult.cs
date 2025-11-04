using System.Collections.Generic;
using System.Linq;

namespace getBIMChecker.Models
{
    /// <summary>
    /// Результат проверки одной оси
    /// </summary>
    public class AxisValidationResult
    {
        /// <summary>
        /// Имя оси
        /// </summary>
        public string AxisName { get; set; }

        /// <summary>
        /// ElementId оси в Revit (NULL для "отсутствует в модели")
        /// </summary>
        public long? ElementId { get; set; }

        /// <summary>
        /// Список типов ошибок
        /// </summary>
        public List<ErrorType> ErrorTypes { get; set; }

        /// <summary>
        /// Смещение в миллиметрах
        /// </summary>
        public double? DeviationMm { get; set; }

        /// <summary>
        /// Закреплена ли ось
        /// </summary>
        public bool? IsPinned { get; set; }

        /// <summary>
        /// Название рабочего набора
        /// </summary>
        public string WorksetName { get; set; }

        /// <summary>
        /// Данные оси из модели (NULL для "отсутствует в модели")
        /// </summary>
        public AxisData ModelAxis { get; set; }

        /// <summary>
        /// Данные эталонной оси
        /// </summary>
        public AxisData ReferenceAxis { get; set; }

        /// <summary>
        /// Проверка наличия ошибок
        /// </summary>
        public bool HasErrors => ErrorTypes != null && ErrorTypes.Count > 0;

        /// <summary>
        /// Конструктор
        /// </summary>
        public AxisValidationResult()
        {
            ErrorTypes = new List<ErrorType>();
        }

        /// <summary>
        /// Получить строку с типами ошибок через "; "
        /// </summary>
        public string GetErrorTypesString()
        {
            if (ErrorTypes == null || ErrorTypes.Count == 0)
                return "-";

            return string.Join("; ", ErrorTypes.Select(GetErrorTypeName));
        }

        /// <summary>
        /// Получить название типа ошибки на русском
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

        /// <summary>
        /// Получить строку для отображения закрепления
        /// </summary>
        public string GetPinnedString()
        {
            if (!IsPinned.HasValue)
                return "-";

            return IsPinned.Value ? "Да" : "Нет";
        }

        /// <summary>
        /// Получить строку для отображения смещения
        /// </summary>
        public string GetDeviationString()
        {
            if (!DeviationMm.HasValue)
                return "-";

            return DeviationMm.Value.ToString("F4");
        }
    }
}
