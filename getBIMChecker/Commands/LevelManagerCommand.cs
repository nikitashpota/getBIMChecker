using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace getBIMChecker.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class LevelManagerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                if (doc == null)
                {
                    message = "Нет активного документа";
                    return Result.Failed;
                }

                var managerEventHandler = new Events.LevelManagerEventHandler();
                var managerExternalEvent = ExternalEvent.Create(managerEventHandler);

                var fixEventHandler = new Events.LevelFixEventHandler();
                var fixExternalEvent = ExternalEvent.Create(fixEventHandler);

                var viewModel = new ViewModels.LevelManagerViewModel(
                    commandData,
                    managerEventHandler,
                    managerExternalEvent,
                    fixEventHandler,
                    fixExternalEvent);

                var window = new Views.LevelManagerWindow { DataContext = viewModel };
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Ошибка: {ex.Message}";
                return Result.Failed;
            }
        }
    }
}