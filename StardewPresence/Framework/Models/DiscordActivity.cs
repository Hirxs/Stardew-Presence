using System.Text.Json.Serialization;

namespace StardewPresence.Framework.Models
{
    public enum DiscordOpCode
    {
        Handshake = 0,
        Frame = 1,
        Close = 2,
        Ping = 3,
        Pong = 4
    }

    public class DiscordActivity
    {
        [JsonPropertyName("details")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Details { get; set; }

        [JsonPropertyName("state")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? State { get; set; }

        [JsonPropertyName("timestamps")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DiscordTimestamps? Timestamps { get; set; }

        [JsonPropertyName("assets")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DiscordAssets? Assets { get; set; }

        [JsonPropertyName("party")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DiscordParty? Party { get; set; }

        [JsonPropertyName("secrets")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DiscordSecrets? Secrets { get; set; }

        [JsonPropertyName("buttons")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DiscordButton[]? Buttons { get; set; }

        [JsonPropertyName("instance")]
        public bool Instance { get; set; } = true;
    }

    public class DiscordTimestamps
    {
        [JsonPropertyName("start")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? Start { get; set; }

        [JsonPropertyName("end")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? End { get; set; }
    }

    public class DiscordAssets
    {
        [JsonPropertyName("large_image")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LargeImage { get; set; }

        [JsonPropertyName("large_text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LargeText { get; set; }

        [JsonPropertyName("small_image")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SmallImage { get; set; }

        [JsonPropertyName("small_text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SmallText { get; set; }
    }

    public class DiscordParty
    {
        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Id { get; set; }

        [JsonPropertyName("size")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int[]? Size { get; set; }
    }

    public class DiscordSecrets
    {
        [JsonPropertyName("join")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Join { get; set; }
    }

    public class DiscordButton
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }

    public class DiscordUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("discriminator")]
        public string Discriminator { get; set; } = string.Empty;

        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }
    }
}
