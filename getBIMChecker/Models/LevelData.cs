using System;

namespace getBIMChecker.Models
{
    public class LevelData
    {
        public int Id { get; set; }
        public int ModelId { get; set; }
        public string LevelName { get; set; }
        public double Elevation { get; set; } // в миллиметрах
        public DateTime CreatedAt { get; set; }
    }
}