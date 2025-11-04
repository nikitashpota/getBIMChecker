using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace getBIMChecker.Commands
{
    /// <summary>
    /// Команда для выбора осей в модели Revit
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SelectAxisCommand : IExternalCommand
    {
        /// <summary>
        /// Выбранные оси (статическое поле для передачи данных между командой и ViewModel)
        /// </summary>
        public static List<Grid> SelectedGrids { get; private set; }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                // Очищаем предыдущий выбор
                SelectedGrids = new List<Grid>();

                // Создаем фильтр для выбора только осей
                var selectionFilter = new GridSelectionFilter();

                // Запрашиваем выбор осей
                IList<Reference> references = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    selectionFilter,
                    "Выберите оси для отправки в базу данных");

                // Собираем выбранные оси
                foreach (Reference reference in references)
                {
                    Element element = doc.GetElement(reference);
                    if (element is Grid grid)
                    {
                        SelectedGrids.Add(grid);
                    }
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Пользователь отменил выбор
                message = "Выбор отменен";
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = $"Ошибка при выборе осей: {ex.Message}";
                return Result.Failed;
            }
        }
    }

    /// <summary>
    /// Фильтр для выбора только осей
    /// </summary>
    public class GridSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Grid;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
