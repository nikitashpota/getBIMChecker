using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using getBIMChecker.Models;

namespace getBIMChecker.Services
{
    /// <summary>
    /// Сервис для исправления ошибок осей
    /// </summary>
    public class AxisFixService
    {
        private const string EXPECTED_WORKSET_NAME = "#_Общие уровни и сетки";
        private const double FEET_PER_MM = 1.0 / 304.8;

        /// <summary>
        /// Исправить все ошибки оси
        /// </summary>
        /// <param name="doc">Документ Revit</param>
        /// <param name="error">Результат проверки с ошибками</param>
        /// <returns>Результат исправления</returns>
        public FixResult FixAxis(Document doc, AxisValidationResult error)
        {
            var result = new FixResult
            {
                AxisName = error.AxisName,
                Success = true,
                FixedErrors = new List<ErrorType>(),
                FailedErrors = new List<string>()
            };

            try
            {
                using (Transaction trans = new Transaction(doc, $"Исправление оси {error.AxisName}"))
                {
                    trans.Start();

                    try
                    {
                        foreach (var errorType in error.ErrorTypes)
                        {
                            try
                            {
                                bool fixed_ = FixSingleError(doc, error, errorType);
                                if (fixed_)
                                {
                                    result.FixedErrors.Add(errorType);
                                }
                                else
                                {
                                    result.FailedErrors.Add($"{GetErrorTypeName(errorType)}: не удалось исправить");
                                }
                            }
                            catch (Exception ex)
                            {
                                result.FailedErrors.Add($"{GetErrorTypeName(errorType)}: {ex.Message}");
                            }
                        }

                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();
                        result.Success = false;
                        result.FailedErrors.Add($"Транзакция отменена: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.FailedErrors.Add($"Критическая ошибка: {ex.Message}");
            }

            result.Success = result.FailedErrors.Count == 0;
            return result;
        }

        /// <summary>
        /// Исправить одну ошибку
        /// </summary>
        private bool FixSingleError(Document doc, AxisValidationResult error, ErrorType errorType)
        {
            switch (errorType)
            {
                case ErrorType.Deviation:
                case ErrorType.NonParallel:
                    return FixAxisPosition(doc, error);

                case ErrorType.NotPinned:
                    return FixAxisPinning(doc, error);

                case ErrorType.WrongWorkset:
                    return FixAxisWorkset(doc, error);

                case ErrorType.NotInModel:
                    return CreateMissingAxis(doc, error);

                case ErrorType.NotInReference:
                    // Эту ошибку нельзя автоматически исправить - 
                    // ось есть в модели, но отсутствует в эталоне
                    // Решение: либо удалить ось, либо добавить в эталон
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Исправить положение оси (смещение или непараллельность)
        /// </summary>
        private bool FixAxisPosition(Document doc, AxisValidationResult error)
        {
            if (!error.ElementId.HasValue || error.ReferenceAxis == null)
                return false;

            var elementId = new ElementId((int)error.ElementId.Value);
            var grid = doc.GetElement(elementId) as Grid;

            if (grid == null)
                return false;

            var refAxis = error.ReferenceAxis;

            // Конвертируем из мм в футы
            XYZ startPoint = new XYZ(
                refAxis.X1 * FEET_PER_MM,
                refAxis.Y1 * FEET_PER_MM,
                0);

            XYZ endPoint = new XYZ(
                refAxis.X2 * FEET_PER_MM,
                refAxis.Y2 * FEET_PER_MM,
                0);

            // Если ось закреплена, временно открепляем
            bool wasPinned = grid.Pinned;
            if (wasPinned)
            {
                grid.Pinned = false;
            }

            // Вычисляем вектор смещения
            Curve currentCurve = grid.Curve;
            XYZ currentMidpoint = (currentCurve.GetEndPoint(0) + currentCurve.GetEndPoint(1)) / 2;
            XYZ targetMidpoint = (startPoint + endPoint) / 2;
            XYZ translation = targetMidpoint - currentMidpoint;

            // Перемещаем ось
            ElementTransformUtils.MoveElement(doc, grid.Id, translation);

            // Восстанавливаем закрепление
            if (wasPinned)
            {
                grid.Pinned = true;
            }

            return true;
        }

        /// <summary>
        /// Закрепить ось
        /// </summary>
        private bool FixAxisPinning(Document doc, AxisValidationResult error)
        {
            if (!error.ElementId.HasValue)
                return false;

            var elementId = new ElementId((int)error.ElementId.Value);
            var grid = doc.GetElement(elementId) as Grid;

            if (grid == null)
                return false;

            grid.Pinned = true;
            return true;
        }

        /// <summary>
        /// Исправить рабочий набор оси
        /// </summary>
        private bool FixAxisWorkset(Document doc, AxisValidationResult error)
        {
            if (!error.ElementId.HasValue)
                return false;

            if (!doc.IsWorkshared)
                return false;

            var elementId = new ElementId((int)error.ElementId.Value);
            var grid = doc.GetElement(elementId) as Grid;

            if (grid == null)
                return false;

            // Находим нужный рабочий набор
            Workset targetWorkset = FindOrCreateWorkset(doc, EXPECTED_WORKSET_NAME);

            if (targetWorkset == null)
                return false;

            // Получаем параметр рабочего набора
            Parameter worksetParam = grid.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);

            if (worksetParam == null || worksetParam.IsReadOnly)
                return false;

            worksetParam.Set(targetWorkset.Id.IntegerValue);
            return true;
        }

        /// <summary>
        /// Создать отсутствующую ось
        /// </summary>
        private bool CreateMissingAxis(Document doc, AxisValidationResult error)
        {
            if (error.ReferenceAxis == null)
                return false;

            var refAxis = error.ReferenceAxis;

            // Конвертируем из мм в футы
            XYZ startPoint = new XYZ(
                refAxis.X1 * FEET_PER_MM,
                refAxis.Y1 * FEET_PER_MM,
                0);

            XYZ endPoint = new XYZ(
                refAxis.X2 * FEET_PER_MM,
                refAxis.Y2 * FEET_PER_MM,
                0);

            // Создаем линию
            Line line = Line.CreateBound(startPoint, endPoint);

            // Создаем ось
            Grid newGrid = Grid.Create(doc, line);

            if (newGrid == null)
                return false;

            // Устанавливаем имя
            try
            {
                newGrid.Name = refAxis.AxisName;
            }
            catch
            {
                // Имя может быть занято - продолжаем без переименования
            }

            // Закрепляем ось
            newGrid.Pinned = true;

            // Устанавливаем рабочий набор (если модель в режиме совместной работы)
            if (doc.IsWorkshared)
            {
                Workset targetWorkset = FindOrCreateWorkset(doc, EXPECTED_WORKSET_NAME);

                if (targetWorkset != null)
                {
                    Parameter worksetParam = newGrid.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                    if (worksetParam != null && !worksetParam.IsReadOnly)
                    {
                        worksetParam.Set(targetWorkset.Id.IntegerValue);
                    }
                }
            }

            // Обновляем ElementId в результате проверки
            error.ElementId = newGrid.Id.IntegerValue;

            return true;
        }

        /// <summary>
        /// Найти или создать рабочий набор
        /// </summary>
        private Workset FindOrCreateWorkset(Document doc, string worksetName)
        {
            if (!doc.IsWorkshared)
                return null;

            var worksetTable = doc.GetWorksetTable();

            // Ищем существующий рабочий набор
            FilteredWorksetCollector collector = new FilteredWorksetCollector(doc);
            var worksets = collector.OfKind(WorksetKind.UserWorkset).ToWorksets();

            foreach (var ws in worksets)
            {
                if (ws.Name == worksetName)
                {
                    return ws;
                }
            }

            // Создаем новый рабочий набор
            if (WorksetTable.IsWorksetNameUnique(doc, worksetName))
            {
                return Workset.Create(doc, worksetName);
            }

            return null;
        }

        /// <summary>
        /// Получить название типа ошибки
        /// </summary>
        private string GetErrorTypeName(ErrorType type)
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
    }

    /// <summary>
    /// Результат исправления оси
    /// </summary>
    public class FixResult
    {
        public string AxisName { get; set; }
        public bool Success { get; set; }
        public List<ErrorType> FixedErrors { get; set; }
        public List<string> FailedErrors { get; set; }

        public string GetSummary()
        {
            if (Success)
            {
                return $"Ось '{AxisName}': исправлено ошибок - {FixedErrors.Count}";
            }
            else
            {
                return $"Ось '{AxisName}': {string.Join("; ", FailedErrors)}";
            }
        }
    }
}
