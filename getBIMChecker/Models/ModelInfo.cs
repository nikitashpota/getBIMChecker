using System;

namespace getBIMChecker.Models
{
    /// <summary>
    /// Модель информации о Revit-модели
    /// </summary>
    public class ModelInfo
    {
        /// <summary>
        /// ID модели в БД
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID директории, к которой привязана модель
        /// </summary>
        public int DirectoryId { get; set; }

        /// <summary>
        /// Имя модели (название документа Revit)
        /// </summary>
        public string ModelName { get; set; }

        /// <summary>
        /// Дата последнего обновления
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}