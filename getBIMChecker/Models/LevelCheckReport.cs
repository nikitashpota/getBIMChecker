using System;
using System.Collections.Generic;
using System.Linq;

namespace getBIMChecker.Models
{
    public class LevelCheckReport
    {
        public int Id { get; set; }
        public DateTime CheckDate { get; set; }
        public CheckType CheckType { get; set; }
        public string ModelName { get; set; }
        public string DirectoryCode { get; set; }
        public int TotalLevelsInModel { get; set; }
        public int TotalReferenceLevels { get; set; }
        public int ErrorCount { get; set; }
        public List<LevelValidationResult> Errors { get; set; }

        public LevelCheckReport()
        {
            Errors = new List<LevelValidationResult>();
            CheckDate = DateTime.Now;
        }

        public Dictionary<LevelErrorType, int> GetErrorStatistics()
        {
            var stats = new Dictionary<LevelErrorType, int>();
            foreach (var error in Errors)
                foreach (var errorType in error.ErrorTypes)
                    if (stats.ContainsKey(errorType)) stats[errorType]++;
                    else stats[errorType] = 1;
            return stats;
        }
    }
}