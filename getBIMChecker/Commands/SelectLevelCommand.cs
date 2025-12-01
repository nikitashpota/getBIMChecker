using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace getBIMChecker.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class SelectLevelCommand : IExternalCommand
    {
        public static List<Level> SelectedLevels { get; private set; }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                SelectedLevels = new List<Level>();

                var selectionFilter = new LevelSelectionFilter();
                IList<Reference> references = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    selectionFilter,
                    "Выберите уровни для отправки в базу данных");

                foreach (Reference reference in references)
                {
                    Element element = doc.GetElement(reference);
                    if (element is Level level)
                        SelectedLevels.Add(level);
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                message = "Выбор отменен";
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = $"Ошибка при выборе уровней: {ex.Message}";
                return Result.Failed;
            }
        }
    }

    public class LevelSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is Level;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}