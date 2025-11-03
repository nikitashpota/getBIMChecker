using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using getBIMChecker.Models;

namespace getBIMChecker.Services
{
    /// <summary>
    /// Основной сервис для работы с базой данных
    /// </summary>
    public class DatabaseService
    {
        private readonly DatabaseSettings _settings;

        public DatabaseService(DatabaseSettings settings)
        {
            _settings = settings;
        }

        #region Работа с директориями

        /// <summary>
        /// Получить список всех директорий
        /// </summary>
        public List<Directory> GetAllDirectories()
        {
            var directories = new List<Directory>();

            try
            {
                using (var connection = new MySqlConnection(_settings.GetConnectionString()))
                {
                    connection.Open();

                    string query = @"
                        SELECT d.id, d.code, d.created_at, COUNT(a.id) as axis_count
                        FROM Directories d
                        LEFT JOIN Models m ON d.id = m.directory_id
                        LEFT JOIN Axes a ON m.id = a.model_id
                        GROUP BY d.id, d.code, d.created_at
                        ORDER BY d.created_at DESC";

                    using (var cmd = new MySqlCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            directories.Add(new Directory
                            {
                                Id = reader.GetInt32("id"),
                                Code = reader.GetString("code"),
                                CreatedAt = reader.GetDateTime("created_at"),
                                AxisCount = reader.IsDBNull(reader.GetOrdinal("axis_count")) ? 0 : reader.GetInt32("axis_count")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка получения списка директорий: {ex.Message}");
            }

            return directories;
        }

        /// <summary>
        /// Создать новую директорию
        /// </summary>
        public int CreateDirectory(string code)
        {
            try
            {
                using (var connection = new MySqlConnection(_settings.GetConnectionString()))
                {
                    connection.Open();

                    string query = "INSERT INTO Directories (code) VALUES (@code)";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@code", code);
                        cmd.ExecuteNonQuery();

                        // Получаем ID созданной директории
                        return (int)cmd.LastInsertedId;
                    }
                }
            }
            catch (MySqlException ex)
            {
                if (ex.Number == 1062) // Duplicate entry
                {
                    throw new Exception($"Директория с кодом '{code}' уже существует");
                }
                throw new Exception($"Ошибка создания директории: {ex.Message}");
            }
        }

        /// <summary>
        /// Обновить код директории
        /// </summary>
        public void UpdateDirectory(int directoryId, string newCode)
        {
            try
            {
                using (var connection = new MySqlConnection(_settings.GetConnectionString()))
                {
                    connection.Open();

                    string query = "UPDATE Directories SET code = @code WHERE id = @id";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@code", newCode);
                        cmd.Parameters.AddWithValue("@id", directoryId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (MySqlException ex)
            {
                if (ex.Number == 1062) // Duplicate entry
                {
                    throw new Exception($"Директория с кодом '{newCode}' уже существует");
                }
                throw new Exception($"Ошибка обновления директории: {ex.Message}");
            }
        }

        /// <summary>
        /// Удалить директорию (каскадно удалятся связанные модели и оси)
        /// </summary>
        public void DeleteDirectory(int directoryId)
        {
            try
            {
                using (var connection = new MySqlConnection(_settings.GetConnectionString()))
                {
                    connection.Open();

                    string query = "DELETE FROM Directories WHERE id = @id";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", directoryId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка удаления директории: {ex.Message}");
            }
        }

        #endregion

        #region Работа с моделями

        /// <summary>
        /// Получить модель по имени и директории
        /// </summary>
        public ModelInfo GetModel(string modelName, int directoryId)
        {
            try
            {
                using (var connection = new MySqlConnection(_settings.GetConnectionString()))
                {
                    connection.Open();

                    string query = @"
                        SELECT id, directory_id, model_name, updated_at
                        FROM Models
                        WHERE model_name = @modelName AND directory_id = @directoryId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@modelName", modelName);
                        cmd.Parameters.AddWithValue("@directoryId", directoryId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new ModelInfo
                                {
                                    Id = reader.GetInt32("id"),
                                    DirectoryId = reader.GetInt32("directory_id"),
                                    ModelName = reader.GetString("model_name"),
                                    UpdatedAt = reader.GetDateTime("updated_at")
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка получения модели: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Создать или обновить модель
        /// </summary>
        public int CreateOrUpdateModel(string modelName, int directoryId)
        {
            try
            {
                using (var connection = new MySqlConnection(_settings.GetConnectionString()))
                {
                    connection.Open();

                    // Проверяем существование модели
                    var existingModel = GetModel(modelName, directoryId);

                    if (existingModel != null)
                    {
                        // Модель существует, обновляем дату
                        string updateQuery = "UPDATE Models SET updated_at = CURRENT_TIMESTAMP WHERE id = @id";
                        using (var cmd = new MySqlCommand(updateQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@id", existingModel.Id);
                            cmd.ExecuteNonQuery();
                        }
                        return existingModel.Id;
                    }
                    else
                    {
                        // Создаем новую модель
                        string insertQuery = "INSERT INTO Models (directory_id, model_name) VALUES (@directoryId, @modelName)";
                        using (var cmd = new MySqlCommand(insertQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@directoryId", directoryId);
                            cmd.Parameters.AddWithValue("@modelName", modelName);
                            cmd.ExecuteNonQuery();
                            return (int)cmd.LastInsertedId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка создания/обновления модели: {ex.Message}");
            }
        }

        #endregion

        #region Работа с осями

        /// <summary>
        /// Удалить все оси модели
        /// </summary>
        public void DeleteAxesByModelId(int modelId)
        {
            try
            {
                using (var connection = new MySqlConnection(_settings.GetConnectionString()))
                {
                    connection.Open();

                    string query = "DELETE FROM Axes WHERE model_id = @modelId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@modelId", modelId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка удаления осей: {ex.Message}");
            }
        }

        /// <summary>
        /// Добавить оси в БД (пакетная вставка)
        /// </summary>
        public void InsertAxes(int modelId, List<AxisData> axes)
        {
            if (axes == null || axes.Count == 0)
                return;

            try
            {
                using (var connection = new MySqlConnection(_settings.GetConnectionString()))
                {
                    connection.Open();

                    string query = @"
                        INSERT INTO Axes (model_id, axis_name, x1, y1, x2, y2)
                        VALUES (@modelId, @axisName, @x1, @y1, @x2, @y2)";

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var axis in axes)
                            {
                                using (var cmd = new MySqlCommand(query, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@modelId", modelId);
                                    cmd.Parameters.AddWithValue("@axisName", axis.AxisName);
                                    cmd.Parameters.AddWithValue("@x1", axis.X1);
                                    cmd.Parameters.AddWithValue("@y1", axis.Y1);
                                    cmd.Parameters.AddWithValue("@x2", axis.X2);
                                    cmd.Parameters.AddWithValue("@y2", axis.Y2);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка добавления осей: {ex.Message}");
            }
        }

        /// <summary>
        /// Получить все оси модели
        /// </summary>
        public List<AxisData> GetAxesByModelId(int modelId)
        {
            var axes = new List<AxisData>();

            try
            {
                using (var connection = new MySqlConnection(_settings.GetConnectionString()))
                {
                    connection.Open();

                    string query = @"
                        SELECT id, model_id, axis_name, x1, y1, x2, y2, created_at
                        FROM Axes
                        WHERE model_id = @modelId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@modelId", modelId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                axes.Add(new AxisData
                                {
                                    Id = reader.GetInt32("id"),
                                    ModelId = reader.GetInt32("model_id"),
                                    AxisName = reader.GetString("axis_name"),
                                    X1 = reader.GetDouble("x1"),
                                    Y1 = reader.GetDouble("y1"),
                                    X2 = reader.GetDouble("x2"),
                                    Y2 = reader.GetDouble("y2"),
                                    CreatedAt = reader.GetDateTime("created_at")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка получения осей: {ex.Message}");
            }

            return axes;
        }

        #endregion
    }
}