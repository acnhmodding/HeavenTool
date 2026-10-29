#nullable enable

namespace HeavenTool.ModManager.NookKit;

/// <summary>
/// Adds or replaces messages in one msbt. <see cref="File"/> is the msbt's name without extension, and <see cref="Archive"/>
/// the sarc that holds it (<c>String</c> for <c>Message/String_USen.sarc.zs</c>), which NookKit only uses to tell tools apart.
/// </summary>
public sealed record MessagePatch(string File, string Archive, IReadOnlyList<MessageEntry> Messages);

/// <summary>
/// One message. <see cref="Text"/> maps a language (<c>USen</c>, <c>JPja</c>, ...) to its text, and <see cref="DefaultLanguage"/>
/// covers every language not listed.
/// </summary>
public sealed record MessageEntry(string Label, IReadOnlyDictionary<string, string> Text, MessageAttributes? Attributes = null)
{
    public const string DefaultLanguage = "default";
}

/// <summary>
/// The message's attribute bytes: copied from another message in the same msbt (<see cref="Like"/>), or given as hex.
/// </summary>
public sealed record MessageAttributes(string? Like = null, string? Hex = null)
{
    public static MessageAttributes LikeLabel(string label) => new(Like: label);
}

public static class MessageText
{
    /// <summary>Escapes plain text for a NookKit message, where <c>{{</c> starts a tag and a literal one is written twice.</summary>
    public static string Escape(string text) => text.Replace("{{", "{{{{", StringComparison.Ordinal);
}
