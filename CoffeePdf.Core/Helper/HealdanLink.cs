using CoffeePdf.Core.Logging;
using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Helper;

/// <summary>
/// What a window needs of Healdan (spec §5, §9): whether Healdan support is on, the Helper's folders, its channel, and
/// the log. The app has one. A test makes its own, with a fake Helper's folders and settings of its own
/// (<c>FakeHelper.Link</c>).
/// </summary>
public sealed class HealdanLink(SettingsStore settings, HelperFolders folders, CoffeeLog log, HelperChannel? channel = null)
{
    public SettingsStore Settings => settings;

    public HelperFolders Folders => folders;

    public CoffeeLog Log => log;

    public HelperChannel Channel { get; } = channel ?? new HelperChannel(folders, log);

    /// <summary>Healdan support (§5), read from <c>settings.json</c> each time, as the Helper reads it too.</summary>
    public bool Enabled => settings.HealdanEnabled;
}
