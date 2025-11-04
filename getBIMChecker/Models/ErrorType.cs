namespace getBIMChecker.Models
{
    /// <summary>
    /// Типы ошибок при проверке осей
    /// </summary>
    public enum ErrorType
    {
        /// <summary>
        /// Отклонение от эталона (ось параллельна, но смещена)
        /// </summary>
        Deviation,

        /// <summary>
        /// Непараллельность (ось не параллельна эталонной)
        /// </summary>
        NonParallel,

        /// <summary>
        /// Ось не закреплена
        /// </summary>
        NotPinned,

        /// <summary>
        /// Неправильный рабочий набор
        /// </summary>
        WrongWorkset,

        /// <summary>
        /// Ось отсутствует в эталоне
        /// </summary>
        NotInReference,

        /// <summary>
        /// Ось отсутствует в модели
        /// </summary>
        NotInModel
    }
}
