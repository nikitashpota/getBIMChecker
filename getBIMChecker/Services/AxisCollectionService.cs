using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using getBIMChecker.Models;

namespace getBIMChecker.Services
{
    /// <summary>
    /// Сервис для сбора данных осей из модели Revit
    /// </summary>
    public class AxisCollectionService
    {
        /// <summary>
        /// Собрать данные осей из списка Grid элементов
        /// </summary>
        /// <param name="grids">Список осей из Revit</param>
        /// <returns>Список данных осей для БД</returns>
        public List<AxisData> CollectAxisData(List<Grid> grids)
        {
            var axisDataList = new List<AxisData>();

            if (grids == null || grids.Count == 0)
                return axisDataList;

            foreach (var grid in grids)
            {
                try
                {
                    var axisData = ExtractAxisData(grid);
                    if (axisData != null)
                    {
                        axisDataList.Add(axisData);
                    }
                }
                catch (Exception ex)
                {
                    // Логируем ошибку, но продолжаем обработку остальных осей
                    System.Diagnostics.Debug.WriteLine($"Ошибка обработки оси {grid.Name}: {ex.Message}");
                }
            }

            return axisDataList;
        }

        /// <summary>
        /// Извлечь данные из одной оси Grid
        /// </summary>
        private AxisData ExtractAxisData(Grid grid)
        {
            if (grid == null)
                return null;

            // Получаем имя оси
            string axisName = GetAxisName(grid);

            // Получаем кривую оси
            Curve curve = grid.Curve;
            if (curve == null)
                return null;

            // Получаем координаты начала и конца оси
            XYZ startPoint = curve.GetEndPoint(0);
            XYZ endPoint = curve.GetEndPoint(1);

            // Создаем объект AxisData
            return new AxisData
            {
                AxisName = axisName,
                X1 = ConvertToMillimeters(startPoint.X),
                Y1 = ConvertToMillimeters(startPoint.Y),
                X2 = ConvertToMillimeters(endPoint.X),
                Y2 = ConvertToMillimeters(endPoint.Y)
            };
        }

        /// <summary>
        /// Получить имя оси из Grid элемента
        /// </summary>
        private string GetAxisName(Grid grid)
        {
            // Пытаемся получить имя из параметра "Имя"
            Parameter nameParam = grid.LookupParameter("Имя");
            if (nameParam != null && nameParam.HasValue)
            {
                return nameParam.AsString();
            }

            // Если параметр "Имя" не найден, используем свойство Name
            return grid.Name ?? "Без имени";
        }

        /// <summary>
        /// Конвертировать футы в миллиметры (Revit использует футы как внутренние единицы)
        /// </summary>
        private double ConvertToMillimeters(double feet)
        {
            // 1 фут = 304.8 мм
            return feet * 304.8;
        }

        /// <summary>
        /// Получить все оси из документа
        /// </summary>
        /// <param name="doc">Документ Revit</param>
        /// <returns>Список всех осей в документе</returns>
        public List<Grid> GetAllGridsFromDocument(Document doc)
        {
            var grids = new List<Grid>();

            try
            {
                // Создаем фильтр для поиска всех осей
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                ICollection<Element> gridElements = collector.OfClass(typeof(Grid)).ToElements();

                foreach (Element element in gridElements)
                {
                    if (element is Grid grid)
                    {
                        grids.Add(grid);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка получения осей из документа: {ex.Message}");
            }

            return grids;
        }

        /// <summary>
        /// Валидация данных осей перед отправкой в БД
        /// </summary>
        public bool ValidateAxisData(List<AxisData> axisDataList, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (axisDataList == null || axisDataList.Count == 0)
            {
                errorMessage = "Список осей пуст";
                return false;
            }

            // Проверяем каждую ось
            for (int i = 0; i < axisDataList.Count; i++)
            {
                var axis = axisDataList[i];

                // Проверка имени оси
                if (string.IsNullOrWhiteSpace(axis.AxisName))
                {
                    errorMessage = $"Ось #{i + 1}: отсутствует имя";
                    return false;
                }

                // Проверка координат (не должны быть NaN или Infinity)
                if (double.IsNaN(axis.X1) || double.IsInfinity(axis.X1) ||
                    double.IsNaN(axis.Y1) || double.IsInfinity(axis.Y1) ||
                    double.IsNaN(axis.X2) || double.IsInfinity(axis.X2) ||
                    double.IsNaN(axis.Y2) || double.IsInfinity(axis.Y2))
                {
                    errorMessage = $"Ось '{axis.AxisName}': некорректные координаты";
                    return false;
                }

                // Проверка, что начальная и конечная точки не совпадают
                if (Math.Abs(axis.X1 - axis.X2) < 0.001 && Math.Abs(axis.Y1 - axis.Y2) < 0.001)
                {
                    errorMessage = $"Ось '{axis.AxisName}': начальная и конечная точки совпадают";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Получить статистику по собранным осям
        /// </summary>
        public string GetAxisStatistics(List<AxisData> axisDataList)
        {
            if (axisDataList == null || axisDataList.Count == 0)
                return "Оси не выбраны";

            return $"Выбрано осей: {axisDataList.Count}";
        }

        /// <summary>
        /// Фильтровать оси по имени (для поиска дубликатов)
        /// </summary>
        public List<string> FindDuplicateAxisNames(List<AxisData> axisDataList)
        {
            var duplicates = new List<string>();
            var nameCount = new Dictionary<string, int>();

            foreach (var axis in axisDataList)
            {
                if (nameCount.ContainsKey(axis.AxisName))
                {
                    nameCount[axis.AxisName]++;
                    if (nameCount[axis.AxisName] == 2)
                    {
                        duplicates.Add(axis.AxisName);
                    }
                }
                else
                {
                    nameCount[axis.AxisName] = 1;
                }
            }

            return duplicates;
        }
    }
}
