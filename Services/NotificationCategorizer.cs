using System.Text.RegularExpressions;

namespace interview_dotnet_api.Services
{
    public static class NotificationCategorizer
    {
        public const string DefaultCategory = "General";

        // Order matters: first matching category wins when text contains keywords from multiple groups.
        private static readonly (string Category, string[] Keywords)[] Dictionary =
        {
            ("Security", new[] { "password", "login", "log in", "sign in", "2fa", "two-factor", "security", "suspicious", "breach", "verify", "verification" }),
            ("Billing", new[] { "invoice", "payment", "billing", "charge", "subscription", "refund", "receipt", "renewal", "credit card" }),
            ("Social", new[] { "friend request", "comment", "mention", "follow", "follower", "like", "liked", "tagged" }),
            ("System", new[] { "maintenance", "outage", "downtime", "update", "upgrade", "deploy", "deployment", "error", "failure" }),
            ("Reminder", new[] { "reminder", "due", "deadline", "upcoming", "scheduled", "appointment" }),
        };

        private static readonly (string Category, Regex Pattern)[] Rules = BuildRules();

        private static (string Category, Regex Pattern)[] BuildRules()
        {
            var rules = new (string Category, Regex Pattern)[Dictionary.Length];
            for (var i = 0; i < Dictionary.Length; i++)
            {
                var (category, keywords) = Dictionary[i];
                var pattern = string.Join("|", Array.ConvertAll(keywords, Regex.Escape));
                rules[i] = (category, new Regex(@"\b(" + pattern + @")\b", RegexOptions.IgnoreCase | RegexOptions.Compiled));
            }
            return rules;
        }

        public static string Categorize(string? title, string? message)
        {
            var text = $"{title} {message}";
            foreach (var (category, pattern) in Rules)
            {
                if (pattern.IsMatch(text))
                {
                    return category;
                }
            }

            return DefaultCategory;
        }
    }
}
