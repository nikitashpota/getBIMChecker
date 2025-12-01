using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using getBIMChecker.Models;
using getBIMChecker.Services;

namespace getBIMChecker.Events
{
    public enum LevelFixAction { FixSingle, FixAll, SelectLevel }

    public class LevelFixEventHandler : IExternalEventHandler
    {
        private readonly LevelFixService _fixService = new LevelFixService();

        public LevelFixAction CurrentAction { get; set; }
        public LevelValidationResult ErrorToFix { get; set; }
        public List<LevelValidationResult> ErrorsToFix { get; set; }
        public long? ElementIdToSelect { get; set; }
        public Action<List<LevelFixResult>> OnFixCompleted { get; set; }
        public Action<bool> OnSelectCompleted { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                var uidoc = app.ActiveUIDocument;
                var doc = uidoc?.Document;
                if (doc == null) return;

                switch (CurrentAction)
                {
                    case LevelFixAction.FixSingle:
                        if (ErrorToFix != null)
                        {
                            var result = _fixService.FixLevel(doc, ErrorToFix);
                            OnFixCompleted?.Invoke(new List<LevelFixResult> { result });
                        }
                        break;

                    case LevelFixAction.FixAll:
                        if (ErrorsToFix != null && ErrorsToFix.Count > 0)
                        {
                            var results = new List<LevelFixResult>();
                            foreach (var error in ErrorsToFix)
                                results.Add(_fixService.FixLevel(doc, error));
                            OnFixCompleted?.Invoke(results);
                        }
                        break;

                    case LevelFixAction.SelectLevel:
                        if (ElementIdToSelect.HasValue)
                        {
                            try
                            {
                                var elementId = new ElementId((int)ElementIdToSelect.Value);
                                uidoc.Selection.SetElementIds(new List<ElementId> { elementId });
                                uidoc.ShowElements(elementId);
                                OnSelectCompleted?.Invoke(true);
                            }
                            catch { OnSelectCompleted?.Invoke(false); }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LevelFixEvent] Ошибка: {ex.Message}");
            }
        }

        public string GetName() => "getBIMChecker Level Fix Handler";
    }
}