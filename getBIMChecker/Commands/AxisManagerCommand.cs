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

                // Проверяем, что документ открыт
                if (doc == null)
                {
                    message = "Нет активного документа";
                    return Result.Failed;
                }

                // Создаем и показываем главное окно
                var viewModel = new ViewModels.AxisManagerViewModel(commandData);
                var window = new Views.AxisManagerWindow
                {
                    DataContext = viewModel
                };

                // Показываем окно модально
                bool? dialogResult = window.ShowDialog();

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
