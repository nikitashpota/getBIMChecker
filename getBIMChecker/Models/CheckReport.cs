using System;
using System.Collections.Generic;
using System.Linq;

namespace getBIMChecker.Models
{
    /// <summary>
    /// Отчет о проверке осей
    /// </summary>
    public class CheckReport
    {
        /// <summary>
        /// ID записи в БД
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Дата проверки
        /// </summary>
        public DateTime CheckDate { get; set; }

        /// <summary>
        /// Тип проверки
        /// </summary>
        public CheckType CheckType { get; set; }

        /// <summary>
        /// Название модели
        /// </summary>
        public string ModelName { get; set; }

        /// <summary>
        /// Код директории
        /// </summary>
        public string DirectoryCode { get; set; }

        /// <summary>
        /// Всего осей в модели
        /// </summary>
        public int TotalAxesInModel { get; set; }

        /// <summary>
        /// Всего эталонных осей
        /// </summary>
        public int TotalReferenceAxes { get; set; }

        /// <summary>
        /// Количество осей с ошибками
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Список ошибок (только оси с ошибками)
        /// </summary>
        public List<AxisValidationResult> Errors { get; set; }

        /// <summary>
        /// Конструктор
        /// </summary>
        public CheckReport()
        {
            Errors = new List<AxisValidationResult>();
            CheckDate = DateTime.Now;
        }

        /// <summary>
        /// Получить статистику по типам ошибок
        /// </summary>
        public Dictionary<ErrorType, int> GetErrorStatistics()
        {
            var stats = new Dictionary<ErrorType, int>();

            foreach (var error in Errors)
            {
                foreach (var errorType in error.ErrorTypes)
                {
                    if (stats.ContainsKey(errorType))
                        stats[errorType]++;
                    else
                        stats[errorType] = 1;
                }
            }

            return stats;
        }

        /// <summary>
        /// Количество осей без ошибок
        /// </summary>
        public int SuccessCount => TotalAxesInModel - ErrorCount;
    }
}
