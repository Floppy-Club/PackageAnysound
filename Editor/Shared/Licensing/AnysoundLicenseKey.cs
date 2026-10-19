using System.Linq;
using System.Text;

namespace Anysound.Shared.Licensing
{
    // A key is "ANYS" and 26 Crockford base32 characters (130 random bits), shown as ANYS-XXXXX-XXXXX-XXXXX-XXXXX-XXXXXX.
    // Typing is forgiving: case, spaces and dashes don't matter, and O, I and L are read as 0, 1 and 1
    public static class AnysoundLicenseKey
    {
        public const string Prefix = "ANYS";
        public const string Placeholder = "ANYS-XXXXX-XXXXX-XXXXX-XXXXX-XXXXXX";
        const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        const int BodyLength = 26;
        static readonly int[] GroupLengths = { 5, 5, 5, 5, 6 };

        // The key in its standard form, or null if it isn't a key
        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            var compact = new StringBuilder();
            foreach (char c in input.ToUpperInvariant())
            {
                if (char.IsWhiteSpace(c) || c == '-') continue;
                compact.Append(c switch
                {
                    'O' => '0',
                    'I' or 'L' => '1',
                    _ => c
                });
            }

            string text = compact.ToString();
            if (!text.StartsWith(Prefix)) return null;
            string body = text.Substring(Prefix.Length);
            if (body.Length != BodyLength || body.Any(c => Alphabet.IndexOf(c) < 0)) return null;

            var formatted = new StringBuilder(Prefix);
            int index = 0;
            foreach (int length in GroupLengths)
            {
                formatted.Append('-').Append(body, index, length);
                index += length;
            }

            return formatted.ToString();
        }

        public static bool IsValid(string input) => Normalize(input) != null;
    }
}
