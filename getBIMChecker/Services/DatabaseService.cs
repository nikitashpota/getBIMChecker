using System;
using System.Collections.Generic;
using System.Linq;
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
                            System.Diagnostics.Debug.WriteLine($"[DB]     ✗ StackTrace:\n{ex.StackTrace}");
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

        #region Сохранение результатов проверки

        /// <summary>
        /// Сохранить результат проверки осей в БД
        /// </summary>
        public int SaveCheckResult(int modelId, CheckType checkType, int totalAxesInModel, int totalReferenceAxes, int errorCount)
        {
            System.Diagnostics.Debug.WriteLine($"\n[DB] >>> SaveCheckResult");
            System.Diagnostics.Debug.WriteLine($"[DB]     modelId: {modelId}");
            System.Diagnostics.Debug.WriteLine($"[DB]     checkType: {checkType}");
            System.Diagnostics.Debug.WriteLine($"[DB]     totalAxesInModel: {totalAxesInModel}");
            System.Diagnostics.Debug.WriteLine($"[DB]     totalReferenceAxes: {totalReferenceAxes}");
            System.Diagnostics.Debug.WriteLine($"[DB]     errorCount: {errorCount}");

            try
            {
                using (var connection = new MySqlConnection(_settings.GetConnectionString()))
                {
                    connection.Open();

                    // Конвертируем CheckType в строку для ENUM('manual', 'auto')
                    string checkTypeStr = checkType == CheckType.Manual ? "manual" : "auto";

                    string query = @"
                        INSERT INTO AxisCheckResults 
                        (model_id, check_type, total_axes_in_model, total_reference_axes, error_count)
                        VALUES 
                        (@modelId, @checkType, @totalAxesInModel, @totalReferenceAxes, @errorCount)";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@modelId", modelId);
                        cmd.Parameters.AddWithValue("@checkType", checkTypeStr);
                        cmd.Parameters.AddWithValue("@totalAxesInModel", totalAxesInModel);
                        cmd.Parameters.AddWithValue("@totalReferenceAxes", totalReferenceAxes);
                        cmd.Parameters.AddWithValue("@errorCount", errorCount);

                        cmd.ExecuteNonQuery();
                        int checkResultId = (int)cmd.LastInsertedId;

                        System.Diagnostics.Debug.WriteLine($"[DB]     ✓ Результат проверки сохранен: ID={checkResultId}");
                        return checkResultId;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB]     ✗ ОШИБКА: {ex.Message}");
                throw new Exception($"Ошибка сохранения результата проверки: {ex.Message}");
            }
        }

        /// <summary>
        /// Сохранить детальные ошибки проверки осей
        /// </summary>
        public void SaveAxisErrors(int checkResultId, List<AxisValidationResult> errors)
        {
            System.Diagnostics.Debug.WriteLine($"\n[DB] >>> SaveAxisErrors");
            System.Diagnostics.Debug.WriteLine($"[DB]     checkResultId: {checkResultId}");
            System.Diagnostics.Debug.WriteLine($"[DB]     errors.Count: {errors?.Count ?? 0}");

            if (errors == null || errors.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[DB]     ⚠ Список ошибок пуст!");
                return;
            }

            try
            {
                using (var connection = new MySqlConnection(_settings.GetConnectionString()))
                {
                    connection.Open();

                    string query = @"
                        INSERT INTO AxisErrors 
                        (check_result_id, axis_name, element_id, error_types, deviation_mm, is_pinned, workset_name)
                        VALUES 
                        (@checkResultId, @axisName, @elementId, @errorTypes, @deviationMm, @isPinned, @worksetName)";

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            int insertedCount = 0;

                            foreach (var error in errors)
                            {
                                using (var cmd = new MySqlCommand(query, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@checkResultId", checkResultId);
                                    cmd.Parameters.AddWithValue("@axisName", error.AxisName);

                                    // ElementId может быть NULL для "отсутствует в модели"
                                    if (error.ElementId.HasValue)
                                        cmd.Parameters.AddWithValue("@elementId", error.ElementId.Value);
                                    else
                                        cmd.Parameters.AddWithValue("@elementId", DBNull.Value);

                                    // Типы ошибок через ";"
                                    string errorTypesStr = string.Join("; ", error.ErrorTypes.Select(GetErrorTypeString));
                                    cmd.Parameters.AddWithValue("@errorTypes", errorTypesStr);

                                    // Смещение
                                    if (error.DeviationMm.HasValue)
                                        cmd.Parameters.AddWithValue("@deviationMm", error.DeviationMm.Value);
                                    else
                                        cmd.Parameters.AddWithValue("@deviationMm", DBNull.Value);

                                    // Закрепление
                                    if (error.IsPinned.HasValue)
                                        cmd.Parameters.AddWithValue("@isPinned", error.IsPinned.Value);
                                    else
                                        cmd.Parameters.AddWithValue("@isPinned", DBNull.Value);

                                    // Рабочий набор
                                    if (!string.IsNullOrEmpty(error.WorksetName))
                                        cmd.Parameters.AddWithValue("@worksetName", error.WorksetName);
                                    else
                                        cmd.Parameters.AddWithValue("@worksetName", DBNull.Value);

                                    cmd.ExecuteNonQuery();
                                    insertedCount++;
                                }
                            }

                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine($"[DB]     ✓✓✓ Сохранено ошибок: {insertedCount}");
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            System.Diagnostics.Debug.WriteLine($"[DB]     ✗✗✗ ОТКАТ ТРАНЗАКЦИИ!");
                            throw new Exception($"Ошибка сохранения ошибок: {ex.Message}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB]     ✗ ОШИБКА: {ex.Message}");
                throw new Exception($"Ошибка сохранения детальных ошибок: {ex.Message}");
            }
        }

        /// <summary>
        /// Конвертация ErrorType в строку для БД
        /// </summary>
        private string GetErrorTypeString(ErrorType type)
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
        /// Получить историю проверок модели
        /// </summary>
        public List<CheckReport> GetCheckHistory(int modelId, DateTime? from = null, DateTime? to = null)
        {
            System.Diagnostics.Debug.WriteLine($"\n[DB] >>> GetCheckHistory");
            System.Diagnostics.Debug.WriteLine($"[DB]     modelId: {modelId}");

            var reports = new List<CheckReport>();

            try
            {
                using (var connection = new MySqlConnection(_settings.GetConnectionString()))
                {
                    connection.Open();

                    string query = @"
                        SELECT 
                            acr.id,
                            acr.check_date,
                            acr.check_type,
                            acr.total_axes_in_model,
                            acr.total_reference_axes,
                            acr.error_count,
                            m.model_name,
                            d.code AS directory_code
                        FROM AxisCheckResults acr
                        JOIN Models m ON acr.model_id = m.id
                        JOIN Directories d ON m.directory_id = d.id
                        WHERE acr.model_id = @modelId";

                    if (from.HasValue)
                        query += " AND acr.check_date >= @from";
                    if (to.HasValue)
                        query += " AND acr.check_date <= @to";

                    query += " ORDER BY acr.check_date DESC";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@modelId", modelId);
                        if (from.HasValue)
                            cmd.Parameters.AddWithValue("@from", from.Value);
                        if (to.HasValue)
                            cmd.Parameters.AddWithValue("@to", to.Value);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var report = new CheckReport
                                {
                                    Id = reader.GetInt32("id"),
                                    CheckDate = reader.GetDateTime("check_date"),
                                    CheckType = reader.GetString("check_type") == "manual" ? CheckType.Manual : CheckType.Auto,
                                    TotalAxesInModel = reader.GetInt32("total_axes_in_model"),
                                    TotalReferenceAxes = reader.GetInt32("total_reference_axes"),
                                    ErrorCount = reader.GetInt32("error_count"),
                                    ModelName = reader.GetString("model_name"),
                                    DirectoryCode = reader.GetString("directory_code")
                                };

                                reports.Add(report);
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[DB]     ✓ Найдено записей истории: {reports.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB]     ✗ ОШИБКА: {ex.Message}");
                throw new Exception($"Ошибка получения истории проверок: {ex.Message}");
            }

            return reports;
        }

        #endregion
    }
}