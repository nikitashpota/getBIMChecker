using System;
using MySql.Data.MySqlClient;
using getBIMChecker.Models;

namespace getBIMChecker.Services
{
    /// <summary>
    /// Сервис инициализации базы данных и создания таблиц
    /// </summary>
    public class DatabaseInitializer
    {
        /// <summary>
        /// Инициализировать базу данных (создать таблицы, если их нет)
        /// </summary>
        /// <param name="settings">Настройки подключения к БД</param>
        /// <returns>True - успешно, False - ошибка</returns>
        public static bool Initialize(DatabaseSettings settings)
        {
            try
            {
                // Проверяем подключение и создаем базу данных, если её нет
                EnsureDatabaseExists(settings);

                // Создаем таблицы
                CreateTables(settings);

                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Ошибка инициализации базы данных:\n{ex.Message}",
                    "Ошибка БД",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Проверить существование базы данных и создать её, если нет
        /// </summary>
        private static void EnsureDatabaseExists(DatabaseSettings settings)
        {
            using (var connection = new MySqlConnection(settings.GetConnectionStringWithoutDatabase()))
            {
                connection.Open();

                string checkDbQuery = $"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{settings.Database}'";
                using (var cmd = new MySqlCommand(checkDbQuery, connection))
                {
                    var result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        // База данных не существует, создаем её
                        string createDbQuery = $"CREATE DATABASE {settings.Database} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
                        using (var createCmd = new MySqlCommand(createDbQuery, connection))
                        {
                            createCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Создать таблицы в базе данных
        /// </summary>
        private static void CreateTables(DatabaseSettings settings)
        {
            using (var connection = new MySqlConnection(settings.GetConnectionString()))
            {
                connection.Open();

                // Создаем таблицу Directories
                string createDirectoriesTable = @"
                    CREATE TABLE IF NOT EXISTS Directories (
                        id INT AUTO_INCREMENT PRIMARY KEY,
                        code VARCHAR(255) NOT NULL UNIQUE,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

                ExecuteNonQuery(connection, createDirectoriesTable);

                // Создаем таблицу Models
                string createModelsTable = @"
                    CREATE TABLE IF NOT EXISTS Models (
                        id INT AUTO_INCREMENT PRIMARY KEY,
                        directory_id INT NOT NULL,
                        model_name VARCHAR(500) NOT NULL,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                        FOREIGN KEY (directory_id) REFERENCES Directories(id) ON DELETE CASCADE
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

                ExecuteNonQuery(connection, createModelsTable);

                // Создаем таблицу Axes
                string createAxesTable = @"
                    CREATE TABLE IF NOT EXISTS Axes (
                        id INT AUTO_INCREMENT PRIMARY KEY,
                        model_id INT NOT NULL,
                        axis_name VARCHAR(255) NOT NULL,
                        x1 DOUBLE NOT NULL,
                        y1 DOUBLE NOT NULL,
                        x2 DOUBLE NOT NULL,
                        y2 DOUBLE NOT NULL,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (model_id) REFERENCES Models(id) ON DELETE CASCADE
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

                ExecuteNonQuery(connection, createAxesTable);

                // Создаем таблицу AxisCheckResults
                string createAxisCheckResultsTable = @"
                    CREATE TABLE IF NOT EXISTS AxisCheckResults (
                        id INT AUTO_INCREMENT PRIMARY KEY,
                        model_id INT NOT NULL,
                        check_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        check_type ENUM('manual', 'auto') NOT NULL,
                        total_axes_in_model INT NOT NULL,
                        total_reference_axes INT NOT NULL,
                        error_count INT NOT NULL,
                        FOREIGN KEY (model_id) REFERENCES Models(id) ON DELETE CASCADE,
                        INDEX idx_model_date (model_id, check_date)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

                ExecuteNonQuery(connection, createAxisCheckResultsTable);

                // Создаем таблицу AxisErrors
                string createAxisErrorsTable = @"
                    CREATE TABLE IF NOT EXISTS AxisErrors (
                        id INT AUTO_INCREMENT PRIMARY KEY,
                        check_result_id INT NOT NULL,
                        axis_name VARCHAR(255) NOT NULL,
                        element_id BIGINT NULL,
                        error_types VARCHAR(500) NOT NULL,
                        deviation_mm DOUBLE NULL,
                        is_pinned BOOLEAN NULL,
                        workset_name VARCHAR(255) NULL,
                        FOREIGN KEY (check_result_id) REFERENCES AxisCheckResults(id) ON DELETE CASCADE,
                        INDEX idx_check_result (check_result_id)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

                ExecuteNonQuery(connection, createAxisErrorsTable);
            }
        }

        /// <summary>
        /// Выполнить SQL-запрос без возврата данных
        /// </summary>
        private static void ExecuteNonQuery(MySqlConnection connection, string query)
        {
            using (var cmd = new MySqlCommand(query, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Проверить подключение к БД
        /// </summary>
        public static bool TestConnection(DatabaseSettings settings)
        {
            try
            {
                using (var connection = new MySqlConnection(settings.GetConnectionString()))
                {
                    connection.Open();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
