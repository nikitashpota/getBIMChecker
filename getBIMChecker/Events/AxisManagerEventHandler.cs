using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using getBIMChecker.Services;

namespace getBIMChecker.Events
{
    public enum AxisManagerAction
    {
        BindDirectory,
        UnbindDirectory,
        SelectAxes,
        SendAxes
    }

    public class AxisManagerEventHandler : IExternalEventHandler
    {
        private readonly ModelBindingService _modelBindingService;

        public AxisManagerAction CurrentAction { get; set; }
        public string DirectoryCode { get; set; }
        public Document Document { get; set; }

        // Callbacks
        public Action<bool, string> OnBindCompleted { get; set; }
        public Action<bool, string> OnUnbindCompleted { get; set; }

        public AxisManagerEventHandler()
        {
            _modelBindingService = new ModelBindingService();
        }

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = Document ?? app.ActiveUIDocument?.Document;
                if (doc == null) return;

                switch (CurrentAction)
                {
                    case AxisManagerAction.BindDirectory:
                        ExecuteBindDirectory(doc);
                        break;

                    case AxisManagerAction.UnbindDirectory:
                        ExecuteUnbindDirectory(doc);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AxisManagerEvent] Ошибка: {ex.Message}");
            }
        }

        private void ExecuteBindDirectory(Document doc)
        {
            try
            {
                _modelBindingService.BindModelToDirectory(doc, DirectoryCode);
                OnBindCompleted?.Invoke(true, null);
            }
            catch (Exception ex)
            {
                OnBindCompleted?.Invoke(false, ex.Message);
            }
        }

        private void ExecuteUnbindDirectory(Document doc)
        {
            try
            {
                _modelBindingService.UnbindModel(doc);
                OnUnbindCompleted?.Invoke(true, null);
            }
            catch (Exception ex)
            {
                OnUnbindCompleted?.Invoke(false, ex.Message);
            }
        }

        public string GetName() => "getBIMChecker Axis Manager Handler";
    }
}