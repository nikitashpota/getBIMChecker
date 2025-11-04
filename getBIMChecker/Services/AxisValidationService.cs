using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using getBIMChecker.Models;

namespace getBIMChecker.Services
{
    /// <summary>
    /// Сервис проверки осей модели относительно эталонных
    /// </summary>
    public class AxisValidationService
    {
        private const double TOLERANCE_MM = 0.0001; // Допуск в миллиметрах
        private const string EXPECTED_WORKSET_NAME = "#_Общие уровни и сетки";

        /// <summary>
        /// Проверка всех осей модели
        /// </summary>
        public List<AxisValidationResult> ValidateAllAxes(Document doc, List<AxisData> referenceAxes)
        {
            var results = new List<AxisValidationResult>();

            // Получаем все оси из модели
            var modelGrids = GetAllGridsFromDocument(doc);

            // Создаем словарь эталонных осей для быстрого поиска
            var referenceDict = referenceAxes.ToDictionary(a => a.AxisName, a => a);

            // Проверяем каждую ось из модели
            foreach (var grid in modelGrids)
            {
                var modelAxis = ConvertGridToAxisData(grid);
                var axisName = modelAxis.AxisName;

                // Ищем эталонную ось с таким же именем
                if (referenceDict.TryGetValue(axisName, out var referenceAxis))
                {
                    // Эталонная ось найдена - проверяем соответствие
                    var result = ValidateAxis(grid, modelAxis, referenceAxis);
                    if (result.HasErrors)
                    {
                        results.Add(result);
                    }
                }
                else
                {
                    // Эталонная ось не найдена
                    var result = new AxisValidationResult
                    {
                        AxisName = axisName,
                        ElementId = grid.Id.IntegerValue,
                        ModelAxis = modelAxis,
                        ReferenceAxis = null,
                        IsPinned = grid.Pinned,
                        WorksetName = GetWorksetName(grid)
                    };
                    result.ErrorTypes.Add(ErrorType.NotInReference);
                    results.Add(result);
                }
            }

            // Проверяем, есть ли эталонные оси, которых нет в модели
            var modelAxisNames = modelGrids.Select(g => GetAxisName(g)).ToHashSet();
            foreach (var referenceAxis in referenceAxes)
            {
                if (!modelAxisNames.Contains(referenceAxis.AxisName))
                {
                    // Эталонная ось отсутствует в модели
                    var result = new AxisValidationResult
                    {
                        AxisName = referenceAxis.AxisName,
                        ElementId = null,
                        ModelAxis = null,
                        ReferenceAxis = referenceAxis
                    };
                    result.ErrorTypes.Add(ErrorType.NotInModel);
                    results.Add(result);
                }
            }

            return results;
        }

        /// <summary>
        /// Проверка одной оси модели относительно эталонной
        /// </summary>
        public AxisValidationResult ValidateAxis(Grid modelGrid, AxisData modelAxis, AxisData referenceAxis)
        {
            var result = new AxisValidationResult
            {
                AxisName = modelAxis.AxisName,
                ElementId = modelGrid.Id.IntegerValue,
                ModelAxis = modelAxis,
                ReferenceAxis = referenceAxis,
                IsPinned = modelGrid.Pinned,
                WorksetName = GetWorksetName(modelGrid)
            };

            // Проверка параллельности
            bool areParallel = AreAxesParallel(modelAxis, referenceAxis, TOLERANCE_MM);

            if (!areParallel)
            {
                // Оси непараллельны
                result.ErrorTypes.Add(ErrorType.NonParallel);
                result.DeviationMm = CalculateMaxPointToLineDistance(modelAxis, referenceAxis);
            }
            else
            {
                // Оси параллельны, проверяем расстояние между ними
                double distance = CalculateParallelDistance(modelAxis, referenceAxis);

                if (distance > TOLERANCE_MM)
                {
                    result.ErrorTypes.Add(ErrorType.Deviation);
                    result.DeviationMm = distance;
                }
            }

            // Проверка закрепления
            if (!modelGrid.Pinned)
            {
                result.ErrorTypes.Add(ErrorType.NotPinned);
            }

            // Проверка рабочего набора
            string worksetName = GetWorksetName(modelGrid);
            if (!string.IsNullOrEmpty(worksetName) && worksetName != EXPECTED_WORKSET_NAME)
            {
                result.ErrorTypes.Add(ErrorType.WrongWorkset);
            }

            return result;
        }

        /// <summary>
        /// Проверка параллельности двух осей
        /// </summary>
        public bool AreAxesParallel(AxisData axis1, AxisData axis2, double toleranceMm = TOLERANCE_MM)
        {
            // Вычисляем векторы направления
            double dx1 = axis1.X2 - axis1.X1;
            double dy1 = axis1.Y2 - axis1.Y1;

            double dx2 = axis2.X2 - axis2.X1;
            double dy2 = axis2.Y2 - axis2.Y1;

            // Нормализуем векторы
            double len1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
            double len2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);

            if (len1 < toleranceMm || len2 < toleranceMm)
                return false;

            dx1 /= len1;
            dy1 /= len1;
            dx2 /= len2;
            dy2 /= len2;

            // Проверяем параллельность через скалярное произведение
            // Векторы параллельны, если |dot product| = 1
            double dotProduct = Math.Abs(dx1 * dx2 + dy1 * dy2);

            return Math.Abs(dotProduct - 1.0) < toleranceMm;
        }

        /// <summary>
        /// Вычисление расстояния между параллельными прямыми
        /// </summary>
        public double CalculateParallelDistance(AxisData axis1, AxisData axis2)
        {
            // Формула: расстояние от точки до прямой
            // d = |ax0 + by0 + c| / sqrt(a^2 + b^2)
            
            // Уравнение прямой axis2: (y2-y1)*x - (x2-x1)*y + (x2-x1)*y1 - (y2-y1)*x1 = 0
            double a = axis2.Y2 - axis2.Y1;
            double b = -(axis2.X2 - axis2.X1);
            double c = (axis2.X2 - axis2.X1) * axis2.Y1 - (axis2.Y2 - axis2.Y1) * axis2.X1;

            // Расстояние от точки (x1, y1) оси axis1 до прямой axis2
            double distance = Math.Abs(a * axis1.X1 + b * axis1.Y1 + c) / Math.Sqrt(a * a + b * b);

            return distance;
        }

        /// <summary>
        /// Вычисление максимального расстояния от точек одной оси до прямой другой
        /// </summary>
        public double CalculateMaxPointToLineDistance(AxisData modelAxis, AxisData referenceAxis)
        {
            // Уравнение прямой referenceAxis
            double a = referenceAxis.Y2 - referenceAxis.Y1;
            double b = -(referenceAxis.X2 - referenceAxis.X1);
            double c = (referenceAxis.X2 - referenceAxis.X1) * referenceAxis.Y1 - (referenceAxis.Y2 - referenceAxis.Y1) * referenceAxis.X1;

            double denom = Math.Sqrt(a * a + b * b);

            // Расстояние от начальной точки modelAxis
            double dist1 = Math.Abs(a * modelAxis.X1 + b * modelAxis.Y1 + c) / denom;

            // Расстояние от конечной точки modelAxis
            double dist2 = Math.Abs(a * modelAxis.X2 + b * modelAxis.Y2 + c) / denom;

            return Math.Max(dist1, dist2);
        }

        /// <summary>
        /// Получить все оси из документа
        /// </summary>
        private List<Grid> GetAllGridsFromDocument(Document doc)
        {
            var grids = new List<Grid>();

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            ICollection<Element> gridElements = collector.OfClass(typeof(Grid)).ToElements();

            foreach (Element element in gridElements)
            {
                if (element is Grid grid)
                {
                    grids.Add(grid);
                }
            }

            return grids;
        }

        /// <summary>
        /// Конвертировать Grid в AxisData
        /// </summary>
        private AxisData ConvertGridToAxisData(Grid grid)
        {
            Curve curve = grid.Curve;
            XYZ startPoint = curve.GetEndPoint(0);
            XYZ endPoint = curve.GetEndPoint(1);

            return new AxisData
            {
                AxisName = GetAxisName(grid),
                X1 = ConvertToMillimeters(startPoint.X),
                Y1 = ConvertToMillimeters(startPoint.Y),
                X2 = ConvertToMillimeters(endPoint.X),
                Y2 = ConvertToMillimeters(endPoint.Y)
            };
        }

        /// <summary>
        /// Получить имя оси
        /// </summary>
        private string GetAxisName(Grid grid)
        {
            Parameter nameParam = grid.LookupParameter("Имя");
            if (nameParam != null && nameParam.HasValue)
            {
                return nameParam.AsString();
            }

            return grid.Name ?? "Без имени";
        }

        /// <summary>
        /// Получить название рабочего набора
        /// </summary>
        private string GetWorksetName(Grid grid)
        {
            try
            {
                if (grid.Document.IsWorkshared)
                {
                    WorksetId worksetId = grid.WorksetId;
                    if (worksetId != null && worksetId.IntegerValue != -1)
                    {
                        Workset workset = grid.Document.GetWorksetTable().GetWorkset(worksetId);
                        return workset.Name;
                    }
                }
            }
            catch
            {
                // Если модель не работает с рабочими наборами
            }

            return null;
        }

        /// <summary>
        /// Конвертировать футы в миллиметры
        /// </summary>
        private double ConvertToMillimeters(double feet)
        {
            return feet * 304.8;
        }
    }
}
