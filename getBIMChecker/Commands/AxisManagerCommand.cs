using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace getBIMChecker.Commands
{
    /// <summary>
    /// Команда запуска главного окна управления эталонными осями
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class AxisManagerCommand : IExternalCommand
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

                // Создаём ExternalEvent'ы ЗДЕСЬ (в контексте API)
                var managerEventHandler = new Events.AxisManagerEventHandler();
                var managerExternalEvent = ExternalEvent.Create(managerEventHandler);

                var fixEventHandler = new Events.AxisFixEventHandler();
                var fixExternalEvent = ExternalEvent.Create(fixEventHandler);

                // Передаём в ViewModel
                var viewModel = new ViewModels.AxisManagerViewModel(
                    commandData,
                    managerEventHandler,
                    managerExternalEvent,
                    fixEventHandler,
                    fixExternalEvent);

                var window = new Views.AxisManagerWindow
                {
                    DataContext = viewModel
                };

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
