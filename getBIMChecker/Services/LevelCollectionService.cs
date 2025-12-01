using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using getBIMChecker.Models;

namespace getBIMChecker.Services
{
    public class LevelCollectionService
    {
        public List<LevelData> CollectLevelData(List<Level> levels)
        {
            var levelDataList = new List<LevelData>();
            if (levels == null) return levelDataList;

            foreach (var level in levels)
            {
                try
                {
                    levelDataList.Add(new LevelData
                    {
                        LevelName = level.Name ?? "Без имени",
                        Elevation = level.Elevation * 304.8
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка обработки уровня {level.Name}: {ex.Message}");
                }
            }
            return levelDataList;
        }

        public List<Level> GetAllLevelsFromDocument(Document doc)
        {
            var levels = new List<Level>();
            var collector = new FilteredElementCollector(doc);
            foreach (Element element in collector.OfClass(typeof(Level)).ToElements())
                if (element is Level level) levels.Add(level);
            return levels;
        }

        public bool ValidateLevelData(List<LevelData> levelDataList, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (levelDataList == null || levelDataList.Count == 0)
            {
                errorMessage = "Список уровней пуст";
                return false;
            }
            return true;
        }

        public List<string> FindDuplicateLevelNames(List<LevelData> levelDataList)
        {
            var duplicates = new List<string>();
            var nameCount = new Dictionary<string, int>();

            foreach (var level in levelDataList)
            {
                if (nameCount.ContainsKey(level.LevelName))
                {
                    nameCount[level.LevelName]++;
                    if (nameCount[level.LevelName] == 2)
                        duplicates.Add(level.LevelName);
                }
                else
                    nameCount[level.LevelName] = 1;
            }
            return duplicates;
        }
    }
}