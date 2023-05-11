
namespace CIM.ModelCompare
{
	public class CIMEntity
	{
		private string rdfID;	
		private string mRID;

		private string source;
		private string hash;
		private int startLine;
		private int startColumn;
		private int endLine;
		private int endColumn;


		public CIMEntity()
		{
		}


		public string RdfID
        {
            get => rdfID;
            set => rdfID = value;
        }

        public string MRID
        {
            get => mRID;
            set => mRID = value;
        }

        public string Source
        {
            get => source;
            set => source = value;
        }

        public string Hash
        {
            get => hash;
            set => hash = value;
        }

        public int StartLine
        {
            get => startLine;
            set => startLine = value;
        }

        public int StartColumn
        {
            get => startColumn;
            set => startColumn = value;
        }

        public int EndLine
        {
            get => endLine;
            set => endLine = value;
        }

        public int EndColumn
        {
            get => endColumn;
            set => endColumn = value;
        }

    }
}
