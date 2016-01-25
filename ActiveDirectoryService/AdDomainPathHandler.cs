using System.Text;
using System.Text.RegularExpressions;

namespace Affecto.ActiveDirectoryService
{
    internal static class AdDomainPathHandler
    {
        public static string Escape(string adDisplayName)
        {
            int currentStringPosition = 0;
            StringBuilder stringBuilder = new StringBuilder();

            if (!string.IsNullOrEmpty(adDisplayName))
            {
                Regex regex = new Regex(@"(\\*)/");
                MatchCollection matches = regex.Matches(adDisplayName);

                foreach (Match match in matches)
                {
                    Capture capture = match.Captures[0];
                    Group @group = match.Groups[1];

                    if (@group.Length % 2 == 0)
                    {
                        string lettersBeforeCapture = adDisplayName.Substring(currentStringPosition, (capture.Index - currentStringPosition));
                        stringBuilder.Append(lettersBeforeCapture);
                        string escapedSlash = capture.Value.Replace("/", @"\/");
                        stringBuilder.Append(escapedSlash);
                        currentStringPosition = capture.Index + capture.Length;
                    }
                }
                stringBuilder.Append(adDisplayName.Substring(currentStringPosition));
            }
            return stringBuilder.ToString();
        }
    }
}