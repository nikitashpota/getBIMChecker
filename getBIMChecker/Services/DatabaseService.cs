using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using getBIMChecker.Models;

namespace getBIMChecker.Services
{
    public class DatabaseService
    {
        private readonly DatabaseSettings _settings;

        // Константа для имени эталонной модели директории
        private const string REFERENCE_MODEL_NAME = "_Reference";

        public DatabaseService(DatabaseSettings settings)
        {
            _settings = settings;
        }

        #region Работа с директориями

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
                        LEFT JOIN Models m ON d.id = m.directory_id AND m.model_name = @referenceName
                        LEFT JOIN Axes a ON m.id = a.model_id
                        GROUP BY d.id, d.code, d.created_at
                        ORDER BY d.created_at DESC";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@referenceName", REFERENCE_MODEL_NAME);

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
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка получения списка директорий: {ex.Message}");
            }

            return directories;
        }

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
                        return (int)cmd.LastInsertedId;
                    }
                }
            }
            catch (MySqlException ex)
            {
                if (ex.Number == 1062)
                    throw new Exception($"Директория с кодом '{code}' уже существует");
                throw new Exception($"Ошибка создания директории: {ex.Message}");
            }
        }

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
                if (ex.Number == 1062)
                    throw new Exception($"Директория с кодом '{newCode}' уже существует");
                throw new Exception($"Ошибка обновления директории: {ex.Message}");
            }
        }

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

        public int CreateOrUpdateModel(string modelName, int directoryId)
        {
            System.Diagnostics.Debug.WriteLine($"\n[DB] >>> CreateOrUpdateModel");
            System.Diagnostics.Debug.WriteLine($"[DB]     modelName: {modelName}");
            System.Diagnostics.Debug.WriteLine($"[DB]     directoryId: {directoryId}");

            try
            {
                using (var connection = new MySqlConnection(_settings.GetConnectionString()))
                {
                    connection.Open();
                    System.Diagnostics.Debug.WriteLine($"[DB]     ✓ Соединение открыто");

                    var existingModel = GetModel(modelName, directoryId);

                    if (existingModel != null)
                    {
                        string updateQuery = "UPDATE Models SET updated_at = CURRENT_TIMESTAMP WHERE id = @id";
                        using (var cmd = new MySqlCommand(updateQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@id", existingModel.Id);
                            cmd.ExecuteNonQuery();
                        }

                        System.Diagnostics.Debug.WriteLine($"[DB]     ✓ Модель обновлена: ID={existingModel.Id}");
                        return existingModel.Id;
                    }
                    else
                    {
                        string insertQuery = "INSERT INTO Models (directory_id, model_name) VALUES (@directoryId, @modelName)";
                        using (var cmd = new MySqlCommand(insertQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@directoryId", directoryId);
                            cmd.Parameters.AddWithValue("@modelName", modelName);
                            cmd.ExecuteNonQuery();

                            int newId = (int)cmd.LastInsertedId;
                            System.Diagnostics.Debug.WriteLine($"[DB]     ✓ Модель создана: ID={newId}");
                            return newId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB]     ✗ ОШИБКА: {ex.Message}");
                throw new Exception($"Ошибка создания/обновления модели: {ex.Message}");
            }
        }

        /// <summary>
        /// Получить ID эталонной модели для директории (создаёт если не существует)
        /// </summary>
        public int GetOrCreateReferenceModelId(int directoryId)
        {
            System.Diagnostics.Debug.WriteLine($"\n[DB] >>> GetOrCreateReferenceModelId");
            System.Diagnostics.Debug.WriteLine($"[DB]     directoryId: {directoryId}");

            return CreateOrUpdateModel(REFERENCE_MODEL_NAME, directoryId);
        }

        #endregion

        #region Работа с осями

        public void DeleteAxesByModelId(int modelId)
        {
            System.Diagnostics.Debug.WriteLine($"\n[DB] >>> DeleteAxesByModelId");
            System.Diagnostics.Debug.WriteLine($"[DB]     modelId: {modelId}");

            try
            {
                using (var connection = new MySqlConnection(_settings.GetConnectionString()))
                {
                    connection.Open();
                    string query = "DELETE FROM Axes WHERE model_id = @modelId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@modelId", modelId);
                        int deleted = cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine($"[DB]     ✓ Удалено старых осей: {deleted}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB]     ✗ ОШИБКА: {ex.Message}");
                throw new Exception($"Ошибка удаления осей: {ex.Message}");
            }
        }

        public void InsertAxes(int modelId, List<AxisData> axes)
        {
            System.Diagnostics.Debug.WriteLine($"\n[DB] >>> InsertAxes");
            System.Diagnostics.Debug.WriteLine($"[DB]     modelId: {modelId}");
            System.Diagnostics.Debug.WriteLine($"[DB]     axes.Count: {axes?.Count ?? 0}");

            if (axes == null || axes.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[DB]     ⚠ Список осей пуст!");
                return;
            }

            try
            {
                using (var connection = new MySqlConnection(_settings.GetConnectionString()))
                {
                    connection.Open();
                    System.Diagnostics.Debug.WriteLine($"[DB]     ✓ Соединение открыто");

                    string query = @"
                        INSERT INTO Axes (model_id, axis_name, x1, y1, x2, y2)
                        VALUES (@modelId, @axisName, @x1, @y1, @x2, @y2)";

                    using (var transaction = connection.BeginTransaction())
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB]     Начало транзакции...");

                        try
                        {
                            int insertedCount = 0;

                            for (int i = 0; i < axes.Count; i++)
                            {
                                var axis = axes[i];

                                using (var cmd = new MySqlCommand(query, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@modelId", modelId);
                                    cmd.Parameters.AddWithValue("@axisName", axis.AxisName);
                                    cmd.Parameters.AddWithValue("@x1", axis.X1);
                                    cmd.Parameters.AddWithValue("@y1", axis.Y1);
                                    cmd.Parameters.AddWithValue("@x2", axis.X2);
                                    cmd.Parameters.AddWithValue("@y2", axis.Y2);

                                    cmd.ExecuteNonQuery();
                                    insertedCount++;

                                    // Показываем первые 3 и последнюю ось
                                    if (i < 3 || i == axes.Count - 1)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[DB]       {i + 1}. '{axis.AxisName}': ({axis.X1:F1}, {axis.Y1:F1}) -> ({axis.X2:F1}, {axis.Y2:F1})");
                                    }
                                    else if (i == 3)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[DB]       ... вставка {axes.Count - 4} осей ...");
                                    }
                                }
                            }

                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine($"[DB]     ✓✓✓ УСПЕХ! Транзакция завершена!");
                            System.Diagnostics.Debug.WriteLine($"[DB]     ✓✓✓ Вставлено осей: {insertedCount}");
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            System.Diagnostics.Debug.WriteLine($"[DB]     ✗✗✗ ОТКАТ ТРАНЗАКЦИИ!");
                            System.Diagnostics.Debug.WriteLine($"[DB]     ✗ Ошибка: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"[DB]     ✗ StackTrace: {ex.StackTrace}");
                            throw new Exception($"Ошибка вставки осей: {ex.Message}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB]     ✗✗✗ КРИТИЧЕСКАЯ ОШИБКА!");
                System.Diagnostics.Debug.WriteLine($"[DB]     {ex.Message}");
                throw new Exception($"Ошибка добавления осей: {ex.Message}", ex);
            }
        }

        public List<AxisData> GetAxesByModelId(int modelId)
        {
            System.Diagnostics.Debug.WriteLine($"\n[DB] >>> GetAxesByModelId");
            System.Diagnostics.Debug.WriteLine($"[DB]     modelId: {modelId}");

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

                System.Diagnostics.Debug.WriteLine($"[DB]     ✓ Найдено осей в БД: {axes.Count}");
                if (axes.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[DB]     Примеры: {string.Join(", ", axes.Take(3).Select(a => a.AxisName))}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB]     ✗ ОШИБКА: {ex.Message}");
                throw new Exception($"Ошибка получения осей: {ex.Message}");
            }

            return axes;
        }

        /// <summary>
        /// Получить эталонные оси для директории
        /// </summary>
        public List<AxisData> GetReferenceAxesByDirectoryId(int directoryId)
        {
            System.Diagnostics.Debug.WriteLine($"\n[DB] >>> GetReferenceAxesByDirectoryId");
            System.Diagnostics.Debug.WriteLine($"[DB]     directoryId: {directoryId}");

            var axes = new List<AxisData>();

            try
            {
                using (var connection = new MySqlConnection(_settings.GetConnectionString()))
                {
                    connection.Open();

                    string query = @"
                        SELECT a.id, a.model_id, a.axis_name, a.x1, a.y1, a.x2, a.y2, a.created_at
                        FROM Axes a
                        INNER JOIN Models m ON a.model_id = m.id
                        WHERE m.directory_id = @directoryId AND m.model_name = @referenceName";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@directoryId", directoryId);
                        cmd.Parameters.AddWithValue("@referenceName", REFERENCE_MODEL_NAME);

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

                System.Diagnostics.Debug.WriteLine($"[DB]     ✓ Найдено эталонных осей: {axes.Count}");
                if (axes.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[DB]     Примеры эталонных осей: {string.Join(", ", axes.Take(3).Select(a => a.AxisName))}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DB]     ⚠ Эталонные оси не найдены для директории {directoryId}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB]     ✗ ОШИБКА: {ex.Message}");
                throw new Exception($"Ошибка получения эталонных осей: {ex.Message}");
            }

            return axes;
        }

        #endregion

        #region Методы для ReportService (заглушки)

        public int SaveCheckResult(int modelId, CheckType checkType, int totalAxesInModel, int totalReferenceAxes, int errorCount)
        {
            System.Diagnostics.Debug.WriteLine($"[DB] SaveCheckResult (заглушка): errors={errorCount}");
            return 1;
        }

        public void SaveAxisErrors(int checkResultId, List<AxisValidationResult> errors)
        {
            System.Diagnostics.Debug.WriteLine($"[DB] SaveAxisErrors (заглушка): count={errors?.Count ?? 0}");
        }

        public List<CheckReport> GetCheckHistory(int modelId, DateTime? from = null, DateTime? to = null)
        {
            System.Diagnostics.Debug.WriteLine($"[DB] GetCheckHistory (заглушка)");
            return new List<CheckReport>();
        }

        #endregion
    }
}