using System.Text;

namespace CIM.Model
{
    public class ConcreteModelBuildingResult
    {
        private StringBuilder missingValues = new StringBuilder();
        private StringBuilder report = new StringBuilder();
        private bool success = true;
        private int errorCount = 0;
        private int warrningCount = 0;

        public StringBuilder MissingValues
        {
            get => missingValues;
            set => missingValues = value;
        }

        public int ErrorCount
        {
            get => errorCount;
            set
            {
                success = false;
                errorCount = value;
            }
        }

        public int WarrningCount
        {
            get => warrningCount;
            set => warrningCount = value;
        }

        public bool Success => success;

        public StringBuilder Report
        {
            get => report;
            set => report = value;
        }
    }
}
