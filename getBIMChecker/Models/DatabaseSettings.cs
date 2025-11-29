namespace getBIMChecker.Models
{
    /// <summary>
    /// Настройки подключения к базе данных MySQL
    /// </summary>
    public class DatabaseSettings
    {
        /// <summary>
        /// Адрес сервера БД (например "localhost" или IP-адрес)
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Порт сервера БД (по умолчанию 3306)
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Имя базы данных
        /// </summary>
        public string Database { get; set; }

        /// <summary>
        /// Имя пользователя БД
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// Пароль пользователя БД
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Получить строку подключения для MySQL
        /// </summary>
        public string GetConnectionString()
        {
            return $"Server={Host};Port={Port};Database={Database};Uid={User};Pwd={Password};CharSet=utf8mb4;";
        }

        /// <summary>
        /// Получить строку подключения без указания базы данных (для создания БД)
        /// </summary>
        public string GetConnectionStringWithoutDatabase()
        {
            return $"Server={Host};Port={Port};Uid={User};Pwd={Password};CharSet=utf8mb4;";
        }

        /// <summary>
        /// Настройки по умолчанию (для локального сервера)
        /// </summary>
        public static DatabaseSettings Default => new DatabaseSettings
        {
            Host = "94.26.228.158",
            Port = 3306,
            Database = "getBIMChecker_db",
            User = "root",
            Password = "12345Qwert!"
        };
    }
}
