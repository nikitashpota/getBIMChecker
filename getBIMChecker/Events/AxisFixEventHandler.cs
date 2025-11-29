using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using getBIMChecker.Models;
using getBIMChecker.Services;

namespace getBIMChecker.Events
{
    /// <summary>
    /// Типы действий для внешнего события
    /// </summary>
    public enum AxisFixAction
    {
        FixSingle,
        FixAll,
        SelectAxis
    }

    /// <summary>
    /// Обработчик внешних событий для исправления осей
    /// Необходим для выполнения транзакций из немодального окна
    /// </summary>
    public class AxisFixEventHandler : IExternalEventHandler
    {
        private readonly AxisFixService _fixService;

        /// <summary>
        /// Текущее действие
        /// </summary>
        public AxisFixAction CurrentAction { get; set; }

        /// <summary>
        /// Ошибка для исправления (для FixSingle)
        /// </summary>
        public AxisValidationResult ErrorToFix { get; set; }

        /// <summary>
        /// Список ошибок для исправления (для FixAll)
        /// </summary>
        public List<AxisValidationResult> ErrorsToFix { get; set; }

        /// <summary>
        /// ElementId для выделения (для SelectAxis)
        /// </summary>
        public long? ElementIdToSelect { get; set; }

        /// <summary>
        /// Callback после выполнения действия
        /// </summary>
        public Action<List<FixResult>> OnFixCompleted { get; set; }

        /// <summary>
        /// Callback после выделения оси
        /// </summary>
        public Action<bool> OnSelectCompleted { get; set; }

        public AxisFixEventHandler()
        {
            _fixService = new AxisFixService();
        }

        public void Execute(UIApplication app)
        {
            try
            {
                UIDocument uidoc = app.ActiveUIDocument;
                Document doc = uidoc?.Document;

                if (doc == null)
                {
                    System.Diagnostics.Debug.WriteLine("[AxisFixEvent] Нет активного документа");
                    return;
                }

                switch (CurrentAction)
                {
                    case AxisFixAction.FixSingle:
                        ExecuteFixSingle(doc);
                        break;

                    case AxisFixAction.FixAll:
                        ExecuteFixAll(doc);
                        break;

                    case AxisFixAction.SelectAxis:
                        ExecuteSelectAxis(uidoc);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AxisFixEvent] Ошибка: {ex.Message}");
            }
        }

        /// <summary>
        /// Исправить одну ось
        /// </summary>
        private void ExecuteFixSingle(Document doc)
        {
            if (ErrorToFix == null)
            {
                System.Diagnostics.Debug.WriteLine("[AxisFixEvent] ErrorToFix is null");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[AxisFixEvent] Исправление оси: {ErrorToFix.AxisName}");

            var result = _fixService.FixAxis(doc, ErrorToFix);
            var results = new List<FixResult> { result };

            System.Diagnostics.Debug.WriteLine($"[AxisFixEvent] Результат: {result.GetSummary()}");

            OnFixCompleted?.Invoke(results);
        }

        /// <summary>
        /// Исправить все оси
        /// </summary>
        private void ExecuteFixAll(Document doc)
        {
            if (ErrorsToFix == null || ErrorsToFix.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[AxisFixEvent] ErrorsToFix пуст");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[AxisFixEvent] Исправление {ErrorsToFix.Count} осей");

            var results = new List<FixResult>();

            foreach (var error in ErrorsToFix)
            {
                var result = _fixService.FixAxis(doc, error);
                results.Add(result);
                System.Diagnostics.Debug.WriteLine($"[AxisFixEvent] {result.GetSummary()}");
            }

            OnFixCompleted?.Invoke(results);
        }

        /// <summary>
        /// Выделить ось в модели
        /// </summary>
        private void ExecuteSelectAxis(UIDocument uidoc)
        {
            if (!ElementIdToSelect.HasValue)
            {
                OnSelectCompleted?.Invoke(false);
                return;
            }

            try
            {
                var elementId = new ElementId((int)ElementIdToSelect.Value);
                var ids = new List<ElementId> { elementId };

                uidoc.Selection.SetElementIds(ids);
                uidoc.ShowElements(elementId);

                OnSelectCompleted?.Invoke(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AxisFixEvent] Ошибка выделения: {ex.Message}");
                OnSelectCompleted?.Invoke(false);
            }
        }

        public string GetName()
        {
            return "getBIMChecker Axis Fix Handler";
        }
    }
}
