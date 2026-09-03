using System;
using StardewModdingAPI;

namespace StardewPresence.Framework.Integrations
{
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void Unregister(IManifest mod);
        
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);
        void AddParagraph(IManifest mod, Func<string> text);
        
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string>? tooltip = null, string[]? allowedValues = null, Func<string, string>? formatAllowedValue = null, string? fieldId = null);
        void AddKeybind(IManifest mod, Func<SButton> getValue, Action<SButton> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
    }
}
