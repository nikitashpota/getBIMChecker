using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using getBIMChecker.Models;

namespace getBIMChecker.Services
{
    public class LevelValidationService
    {
        private const double TOLERANCE_MM = 0.1;
        private const string EXPECTED_WORKSET_NAME = "#_Общие уровни и сетки";

        public List<LevelValidationResult> ValidateAllLevels(Document doc, List<LevelData> referenceLevels)
        {
            var results = new List<LevelValidationResult>();
            var modelLevels = GetAllLevelsFromDocument(doc);
            var referenceDict = referenceLevels.ToDictionary(l => l.LevelName, l => l);

            foreach (var level in modelLevels)
            {
                var modelLevel = ConvertLevelToLevelData(level);
                if (referenceDict.TryGetValue(modelLevel.LevelName, out var referenceLevel))
                {
                    var result = ValidateLevel(level, modelLevel, referenceLevel);
                    if (result.HasErrors) results.Add(result);
                }
                else
                {
                    var result = new LevelValidationResult
                    {
                        LevelName = modelLevel.LevelName,
                        ElementId = level.Id.IntegerValue,
                        ModelLevel = modelLevel,
                        IsPinned = level.Pinned,
                        WorksetName = GetWorksetName(level)
                    };
                    result.ErrorTypes.Add(LevelErrorType.NotInReference);
                    results.Add(result);
                }
            }

            var modelLevelNames = modelLevels.Select(l => l.Name).ToHashSet();
            foreach (var referenceLevel in referenceLevels)
            {
                if (!modelLevelNames.Contains(referenceLevel.LevelName))
                {
                    var result = new LevelValidationResult
                    {
                        LevelName = referenceLevel.LevelName,
                        ReferenceLevel = referenceLevel
                    };
                    result.ErrorTypes.Add(LevelErrorType.NotInModel);
                    results.Add(result);
                }
            }

            return results;
        }

        private LevelValidationResult ValidateLevel(Level modelLevel, LevelData modelData, LevelData referenceData)
        {
            var result = new LevelValidationResult
            {
                LevelName = modelData.LevelName,
                ElementId = modelLevel.Id.IntegerValue,
                ModelLevel = modelData,
                ReferenceLevel = referenceData,
                IsPinned = modelLevel.Pinned,
                WorksetName = GetWorksetName(modelLevel)
            };

            double elevationDiff = Math.Abs(modelData.Elevation - referenceData.Elevation);
            if (elevationDiff > TOLERANCE_MM)
            {
                result.ErrorTypes.Add(LevelErrorType.ElevationDeviation);
                result.DeviationMm = elevationDiff;
            }

            if (!modelLevel.Pinned)
                result.ErrorTypes.Add(LevelErrorType.NotPinned);

            string worksetName = GetWorksetName(modelLevel);
            if (!string.IsNullOrEmpty(worksetName) && worksetName != EXPECTED_WORKSET_NAME)
                result.ErrorTypes.Add(LevelErrorType.WrongWorkset);

            return result;
        }

        private List<Level> GetAllLevelsFromDocument(Document doc)
        {
            var levels = new List<Level>();
            var collector = new FilteredElementCollector(doc);
            foreach (Element element in collector.OfClass(typeof(Level)).ToElements())
                if (element is Level level) levels.Add(level);
            return levels;
        }

        private LevelData ConvertLevelToLevelData(Level level) => new LevelData
        {
            LevelName = level.Name ?? "Без имени",
            Elevation = level.Elevation * 304.8
        };

        private string GetWorksetName(Level level)
        {
            try
            {
                if (level.Document.IsWorkshared)
                {
                    var worksetId = level.WorksetId;
                    if (worksetId != null && worksetId.IntegerValue != -1)
                        return level.Document.GetWorksetTable().GetWorkset(worksetId).Name;
                }
            }
            catch { }
            return null;
        }
    }
}