using System;

namespace getBIMChecker.Models
{
    /// <summary>
    /// Модель данных оси
    /// </summary>
    public class AxisData
    {
        /// <summary>
        /// ID оси в БД
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID модели, к которой относится ось
        /// </summary>
        public int ModelId { get; set; }

        /// <summary>
        /// Имя оси (например "1", "А", "Б/1")
        /// </summary>
        public string AxisName { get; set; }

        /// <summary>
        /// Координата X начальной точки оси
        /// </summary>
        public double X1 { get; set; }

        /// <summary>
        /// Координата Y начальной точки оси
        /// </summary>
        public double Y1 { get; set; }

        /// <summary>
        /// Координата X конечной точки оси
        /// </summary>
        public double X2 { get; set; }

        /// <summary>
        /// Координата Y конечной точки оси
        /// </summary>
        public double Y2 { get; set; }

        /// <summary>
        /// Дата создания записи
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}