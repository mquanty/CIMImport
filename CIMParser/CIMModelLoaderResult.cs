using System.Text;

namespace CIMParser
{
    public class CIMModelLoaderResult
    {
        private StringBuilder report = new StringBuilder();
        private bool success = true;

        public CIMModelLoaderResult()
        {
            this.success = true;
        }

        public CIMModelLoaderResult(bool success) 
        {
            this.success = success;
        }

        public StringBuilder Report
        {
            get => report;
            set => report = value;
        }

        public bool Success
        {
            get => success;
            set => success = value;
        }
    }
}
