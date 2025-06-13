namespace ZeroGallery.Shared
{
    public static class StringExtensions
    {
        public static bool IsEqual(this string a, string b) 
        {
            if(a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Equals(b);
        }
    }
}
