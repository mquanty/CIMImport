namespace CIM.Model
{
    internal class BasicConversions
    {
        public static bool StrToInt(string str, out int num)
        {
            return int.TryParse(str, out num);
        }
    }
}
