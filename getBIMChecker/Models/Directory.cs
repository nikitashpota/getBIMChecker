using System;

namespace getBIMChecker.Models
{
    /// <summary>
    /// Модель директории проекта (кода проекта)
    /// </summary>
    public class Directory
    {
        /// <summary>
        /// ID директории в БД
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Код директории (уникальное имя проекта, например "Сертолово", "Химки К1")
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Количество эталонных осей в этой директории
        /// </summary>
        public int AxisCount { get; set; }

        /// <summary>
        /// Дата создания директории
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Флаг - привязана ли текущая модель к этой директории
        /// </summary>
        public bool IsBound { get; set; }
    }
}