using System;
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
        /// Получить имя модели из документа Revit
        /// </summary>
        public string GetModelName(Document doc)
        {
            return System.IO.Path.GetFileNameWithoutExtension(doc.Title);
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