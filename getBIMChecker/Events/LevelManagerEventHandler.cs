using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using getBIMChecker.Services;

namespace getBIMChecker.Events
{
    public enum LevelManagerAction
    {
        BindDirectory,
        UnbindDirectory
    }

    public class LevelManagerEventHandler : IExternalEventHandler
    {
        private readonly ModelBindingService _modelBindingService = new ModelBindingService();

        public LevelManagerAction CurrentAction { get; set; }
        public string DirectoryCode { get; set; }
        public Document Document { get; set; }

        public Action<bool, string> OnBindCompleted { get; set; }
        public Action<bool, string> OnUnbindCompleted { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = Document ?? app.ActiveUIDocument?.Document;
                if (doc == null) return;

                switch (CurrentAction)
                {
                    case LevelManagerAction.BindDirectory:
                        ExecuteBindDirectory(doc);
                        break;
                    case LevelManagerAction.UnbindDirectory:
                        ExecuteUnbindDirectory(doc);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LevelManagerEvent] Ошибка: {ex.Message}");
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

        public string GetName() => "getBIMChecker Level Manager Handler";
    }
}