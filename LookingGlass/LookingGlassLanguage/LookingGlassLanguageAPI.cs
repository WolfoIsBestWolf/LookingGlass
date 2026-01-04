using RoR2;

namespace LookingGlass.LookingGlassLanguage
{
    public static class LookingGlassLanguageAPI //calling this an API is really stretching it...
    {
        public static void SetupToken(Language language, string token, string value)
        {
            //Log.Debug($"{token}   {value}");
            language.stringsByToken["LG_TOKEN_" + token] = value;
        }
    }
}
