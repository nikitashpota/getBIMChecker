using System;
using System.IO;
using Autodesk.Revit.DB;

namespace getBIMChecker.Services
{
    /// <summary>
    /// Сервис для привязки модели Revit к директории проекта
    /// </summary>
    public class ModelBindingService
    {
        private const string PARAMETER_NAME = "#_Код площадки";

        /// <summary>
        /// Получить код директории, к которой привязана модель
        /// </summary>
        public string GetBoundDirectoryCode(Document doc)
        {
            try
            {
                // Получаем информацию о проекте
                ProjectInfo projectInfo = doc.ProjectInformation;

                if (projectInfo == null)
                    return null;

                // Ищем параметр "#_Код площадки"
                Parameter parameter = projectInfo.LookupParameter(PARAMETER_NAME);

                if (parameter != null && parameter.HasValue)
                {
                    string value = parameter.AsString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Привязать модель к директории (записать код директории в параметр "#_Код площадки")
        /// </summary>
        public void BindModelToDirectory(Document doc, string directoryCode)
        {
            using (Transaction trans = new Transaction(doc, "Привязка модели к директории"))
            {
                trans.Start();

                try
                {
                    ProjectInfo projectInfo = doc.ProjectInformation;

                    if (projectInfo == null)
                    {
                        throw new Exception("Не удалось получить информацию о проекте");
                    }

                    // Ищем параметр "#_Код площадки"
                    Parameter parameter = projectInfo.LookupParameter(PARAMETER_NAME);

                    if (parameter == null)
                    {
                        throw new Exception($"Параметр '{PARAMETER_NAME}' не найден в Сведениях о проекте.\n" +
                                          "Убедитесь, что параметр существует.");
                    }

                    // Проверяем, что параметр доступен для записи
                    if (parameter.IsReadOnly)
                    {
                        throw new Exception($"Параметр '{PARAMETER_NAME}' доступен только для чтения");
                    }

                    // Устанавливаем значение
                    parameter.Set(directoryCode);

                    trans.Commit();
                }
                catch
                {
                    trans.RollBack();
                    throw;
                }
            }
        }

        /// <summary>
        /// Отвязать модель от директории (очистить параметр)
        /// </summary>
        public void UnbindModel(Document doc)
        {
            using (Transaction trans = new Transaction(doc, "Отвязка модели от директории"))
            {
                trans.Start();

                try
                {
                    ProjectInfo projectInfo = doc.ProjectInformation;
                    Parameter parameter = projectInfo?.LookupParameter(PARAMETER_NAME);

                    if (parameter != null && !parameter.IsReadOnly)
                    {
                        parameter.Set(string.Empty);
                    }

                    trans.Commit();
                }
                catch
                {
                    trans.RollBack();
                    throw;
                }
            }
        }

        /// <summary>
        /// Получить имя модели из документа Revit (для локальных копий получает имя центральной модели)
        /// </summary>
        public string GetModelName(Document doc)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("\n[ModelName] >>> Получение имени модели...");

                // Проверяем, является ли модель файлом совместной работы
                if (doc.IsWorkshared)
                {
                    System.Diagnostics.Debug.WriteLine("[ModelName] Модель в режиме совместной работы");

                    try
                    {
                        // Получаем путь к центральной модели
                        ModelPath centralPath = doc.GetWorksharingCentralModelPath();

                        if (centralPath != null)
                        {
                            string centralModelPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(centralPath);

                            if (!string.IsNullOrEmpty(centralModelPath))
                            {
                                // Извлекаем имя файла из пути
                                string fileName = Path.GetFileNameWithoutExtension(centralModelPath);
                                System.Diagnostics.Debug.WriteLine($"[ModelName] ✓ Имя центральной модели: {fileName}");
                                System.Diagnostics.Debug.WriteLine($"[ModelName]   Полный путь: {centralModelPath}");
                                return fileName;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ModelName] ⚠ Не удалось получить путь центральной модели: {ex.Message}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ModelName] Модель НЕ в режиме совместной работы");
                }

                // Если не удалось получить имя центральной модели, используем Title
                string title = Path.GetFileNameWithoutExtension(doc.Title);
                System.Diagnostics.Debug.WriteLine($"[ModelName] Используем Title: {title}");

                // Пытаемся удалить суффикс пользователя (если есть)
                string cleanedName = RemoveUserSuffix(title);

                if (cleanedName != title)
                {
                    System.Diagnostics.Debug.WriteLine($"[ModelName] ✓ Удален суффикс пользователя: {cleanedName}");
                }

                return cleanedName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelName] ✗ ОШИБКА: {ex.Message}");
                // В крайнем случае возвращаем Title как есть
                return Path.GetFileNameWithoutExtension(doc.Title ?? "Unknown");
            }
        }

        /// <summary>
        /// Удалить суффикс пользователя из имени локальной копии
        /// Например: "Проект1_Иван Иванов" -> "Проект1"
        /// </summary>
        private string RemoveUserSuffix(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return fileName;

            // Ищем последнее подчеркивание
            int lastUnderscoreIndex = fileName.LastIndexOf('_');

            if (lastUnderscoreIndex > 0)
            {
                // Проверяем, что после подчеркивания идет текст (имя пользователя)
                string potentialSuffix = fileName.Substring(lastUnderscoreIndex + 1);

                // Если после подчеркивания есть буквы (имя пользователя), удаляем суффикс
                if (potentialSuffix.Length > 0 && ContainsLetters(potentialSuffix))
                {
                    return fileName.Substring(0, lastUnderscoreIndex);
                }
            }

            return fileName;
        }

        /// <summary>
        /// Проверить, содержит ли строка буквы
        /// </summary>
        private bool ContainsLetters(string text)
        {
            foreach (char c in text)
            {
                if (char.IsLetter(c))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Получить полную информацию о модели (для отладки)
        /// </summary>
        public string GetModelInfo(Document doc)
        {
            var info = new System.Text.StringBuilder();

            info.AppendLine("=== ИНФОРМАЦИЯ О МОДЕЛИ ===");
            info.AppendLine($"Title: {doc.Title}");
            info.AppendLine($"PathName: {doc.PathName}");
            info.AppendLine($"IsWorkshared: {doc.IsWorkshared}");

            if (doc.IsWorkshared)
            {
                try
                {
                    ModelPath centralPath = doc.GetWorksharingCentralModelPath();
                    if (centralPath != null)
                    {
                        string centralModelPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(centralPath);
                        info.AppendLine($"Central Model Path: {centralModelPath}");
                    }
                }
                catch (Exception ex)
                {
                    info.AppendLine($"Central Model Path: ERROR - {ex.Message}");
                }
            }

            info.AppendLine($"GetModelName(): {GetModelName(doc)}");
            info.AppendLine("===========================");

            return info.ToString();
        }

        /// <summary>
        /// Проверить существование параметра "#_Код площадки"
        /// </summary>
        public bool CheckParameterExists(Document doc)
        {
            try
            {
                ProjectInfo projectInfo = doc.ProjectInformation;
                if (projectInfo == null)
                    return false;

                Parameter parameter = projectInfo.LookupParameter(PARAMETER_NAME);
                return parameter != null;
            }
            catch
            {
                return false;
            }
        }
    }
}