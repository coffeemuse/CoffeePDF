using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Tests.Documents;

/// <summary>Spec §6.1: a window's mode, and what it decides.</summary>
public sealed class WindowDocumentTests
{
    private const string Copy = "/support/Copies/Settlement Agreement #1234 v2.1.pdf";
    private const string ReadOnlyCopy = "/support/Copies/Settlement Agreement #1234 v2.1 (read-only).pdf";

    private static CopyState State(bool readOnly = false, string? holdBy = null, string version = "2.1", bool pending = false) =>
        new(true, 1234, $"Settlement Agreement #1234 v{version}{(readOnly ? " (read-only)" : "")}", $"#1234 v{version}",
            readOnly, readOnly ? ReadOnlyCopy : Copy, holdBy, pending);

    private static WindowDocument Asking(string path = Copy) => WindowDocument.Opened(path, healdan: true, inCopies: true);

    [Fact]
    public void A_file_outside_Copies_is_local_whatever_the_setting()
    {
        Assert.Equal(DocumentMode.Local, WindowDocument.Opened("/docs/Report.pdf", healdan: true, inCopies: false).Mode);
        Assert.Equal(DocumentMode.Local, WindowDocument.Opened("/docs/Report.pdf", healdan: false, inCopies: false).Mode);
    }

    [Fact]
    public void A_copy_with_support_off_is_read_only_until_support_is_on() =>
        Assert.Equal(DocumentMode.SupportOff, WindowDocument.Opened(Copy, healdan: false, inCopies: true).Mode);

    [Fact]
    public void A_copy_with_support_on_asks_the_Helper() => Assert.Equal(DocumentMode.Asking, Asking().Mode);

    [Fact]
    public void The_Helpers_answer_sets_the_mode()
    {
        Assert.Equal(DocumentMode.Editable, Asking().Answered(State()).Mode);
        Assert.Equal(DocumentMode.ReadOnly, Asking(ReadOnlyCopy).Answered(State(readOnly: true)).Mode);
        Assert.False(Asking(ReadOnlyCopy).Answered(State(readOnly: true)).Refused);
        Assert.Equal(DocumentMode.HelperAway, Asking().Answered(null).Mode);
        Assert.Equal(DocumentMode.Local, Asking().Answered(CopyState.Unknown(Copy)).Mode);
    }

    [Fact]
    public void A_read_only_answer_for_an_editable_copy_is_a_refused_save()
    {
        var refused = Asking().Answered(State()).Answered(State(readOnly: true));

        Assert.Equal(DocumentMode.ReadOnly, refused.Mode);
        Assert.True(refused.Refused);
        Assert.True(Asking().Answered(State()).Answered(null).Answered(State(readOnly: true)).Refused);
    }

    [Fact]
    public void An_editable_copy_whose_Helper_goes_is_the_Helper_away_one_until_it_answers()
    {
        var away = Asking().Answered(State()).Answered(null);

        Assert.Equal(DocumentMode.HelperAway, away.Mode);
        Assert.Equal(DocumentMode.Editable, away.Answered(State(version: "2.2")).Mode);
    }

    [Fact]
    public void A_read_only_copy_stays_read_only()
    {
        var readOnly = Asking(ReadOnlyCopy).Answered(State(readOnly: true));

        Assert.Equal(DocumentMode.ReadOnly, readOnly.Answered(State()).Mode);
        Assert.Equal(DocumentMode.ReadOnly, readOnly.Answered(null).Mode);
    }

    [Fact]
    public void Local_and_support_off_windows_dont_take_the_Helpers_answers()
    {
        Assert.Equal(DocumentMode.Local, WindowDocument.Opened("/docs/Report.pdf", true, false).Answered(null).Mode);
        Assert.Equal(DocumentMode.SupportOff, WindowDocument.Opened(Copy, false, true).Answered(State()).Mode);
    }

    [Fact]
    public void A_copys_title_is_the_Helpers()
    {
        Assert.Equal("Settlement Agreement #1234 v2.1", Asking().Answered(State()).Title(edited: false, saving: false));
        Assert.Equal("Settlement Agreement #1234 v2.1 — Edited", Asking().Answered(State()).Title(edited: true, saving: false));
        // A save moves the Helper's title on, though the copy keeps the name it was opened with.
        Assert.Equal("Settlement Agreement #1234 v2.2 — Saving to Healdan…",
            Asking().Answered(State()).Answered(State(version: "2.2", pending: true)).Title(edited: false, saving: true));
    }

    [Fact]
    public void A_read_only_copys_title_says_so_as_the_Helpers_does() =>
        Assert.Equal("Settlement Agreement #1234 v2.1 (read-only)", Asking(ReadOnlyCopy).Answered(State(readOnly: true)).Title(false, false));

    [Fact]
    public void Without_the_Helpers_answer_the_title_is_the_copys_name_as_the_Helper_would_give_it()
    {
        Assert.Equal("Settlement Agreement #1234 v2.1", Asking().Title(false, false));
        Assert.Equal("Settlement Agreement #1234 v2.1", Asking().Answered(null).Title(false, false));
        Assert.Equal("Settlement Agreement #1234 v2.1", WindowDocument.Opened(Copy, false, true).Title(false, false));
        Assert.Equal("Settlement Agreement #1234 v2.1 (read-only)", Asking(ReadOnlyCopy).Answered(null).Title(false, false));
    }

    [Fact]
    public void The_Helpers_title_shows_as_it_is_and_an_empty_one_gives_way_to_the_copys_name()
    {
        Assert.Equal("Something else entirely", Asking().Answered(State() with { Title = "Something else entirely" }).Title(false, false));
        Assert.Equal("Settlement Agreement #1234 v2.1", Asking().Answered(State() with { Title = "" }).Title(false, false));
    }

    [Fact]
    public void Local_files_and_names_that_arent_a_copys_show_the_file_name()
    {
        Assert.Equal("Report.pdf — Edited", WindowDocument.Opened("/docs/Report.pdf", true, false).Title(edited: true, saving: false));
        Assert.Equal("notes.pdf", Asking("/support/Copies/notes.pdf").Answered(null).Title(false, false));
    }

    [Fact]
    public void Saving_to_Healdan_shows_only_where_saves_go_there() =>
        Assert.Equal("Report.pdf", WindowDocument.Opened("/docs/Report.pdf", true, false).Title(edited: false, saving: true));

    [Fact]
    public void Each_copy_mode_has_its_bar()
    {
        Assert.Null(WindowDocument.Opened("/docs/Report.pdf", true, false).Bar);
        Assert.Null(Asking().Bar);
        Assert.Null(Asking().Answered(State()).Bar);
        Assert.Equal(new WindowBar(WindowBar.HeldBy("Zoë Q. Reviewer"), BarAction.SaveAsNewDocument),
            Asking(ReadOnlyCopy).Answered(State(readOnly: true, holdBy: "Zoë Q. Reviewer")).Bar);
        Assert.Equal(new WindowBar(WindowBar.ReadOnlyOpen, BarAction.SaveAsNewDocument), Asking(ReadOnlyCopy).Answered(State(readOnly: true)).Bar);
        Assert.Equal(new WindowBar(WindowBar.Refused, BarAction.SaveAsNewDocument), Asking().Answered(State()).Answered(State(readOnly: true)).Bar);
        Assert.Equal(new WindowBar(WindowBar.HelperAway, BarAction.None), Asking().Answered(null).Bar);
        Assert.Equal(new WindowBar(WindowBar.HelperAwayReadOnly, BarAction.None), Asking(ReadOnlyCopy).Answered(null).Bar);
        Assert.Equal(new WindowBar(WindowBar.SupportOff, BarAction.TurnOnHealdan), WindowDocument.Opened(Copy, false, true).Bar);
        Assert.Equal("Read-only — checked out by Zoë Q. Reviewer", WindowBar.HeldBy("Zoë Q. Reviewer"));
    }

    [Fact]
    public void Each_mode_allows_what_spec_6_1_says()
    {
        static WindowCommands Of(DocumentMode mode, bool healdan = true, string path = Copy) => new WindowDocument(path, mode).Commands(healdan);

        Assert.Equal(new WindowCommands(Markup: true, Save: true, HealdanMenu: true, SaveToHealdan: true, SaveAsNewVersion: false,
            SaveAsNewDocument: false, Properties: true, HelperBoxes: true), Of(DocumentMode.Local));
        Assert.Equal(new WindowCommands(true, true, false, false, false, false, false, false), Of(DocumentMode.Local, healdan: false));
        Assert.Equal(new WindowCommands(true, true, true, true, true, true, true, true), Of(DocumentMode.Editable));
        Assert.Equal(new WindowCommands(false, false, true, false, false, true, false, true), Of(DocumentMode.ReadOnly));
        Assert.Equal(new WindowCommands(true, true, true, false, false, false, false, true), Of(DocumentMode.HelperAway));
        Assert.Equal(new WindowCommands(false, false, true, false, false, false, false, true), Of(DocumentMode.HelperAway, path: ReadOnlyCopy));
        Assert.Equal(new WindowCommands(false, false, true, false, false, false, false, true), Of(DocumentMode.Asking));
        Assert.Equal(new WindowCommands(false, false, false, false, false, false, false, false), Of(DocumentMode.SupportOff));
        // Turning support off waits for a copy's window to close (§5): a copy's commands don't follow the setting.
        Assert.Equal(Of(DocumentMode.Editable), Of(DocumentMode.Editable, healdan: false));
        Assert.Equal(new WindowCommands(false, false, true, false, false, false, false, true), WindowCommands.Empty(healdan: true));
        Assert.Equal(new WindowCommands(false, false, false, false, false, false, false, false), WindowCommands.Empty(healdan: false));
    }

    [Fact]
    public void Only_a_copy_the_window_may_edit_keeps_a_lock_and_follows_its_saves()
    {
        static bool Locks(WindowDocument document) => document.HoldsLock && document.FollowsSaves;

        Assert.True(Locks(Asking().Answered(State())));
        Assert.True(Locks(Asking().Answered(null)));
        Assert.False(Asking(ReadOnlyCopy).Answered(null).HoldsLock);
        Assert.False(Asking().HoldsLock);
        Assert.False(Asking(ReadOnlyCopy).Answered(State(readOnly: true)).HoldsLock);
        Assert.False(WindowDocument.Opened(Copy, false, true).HoldsLock);
        Assert.False(WindowDocument.Opened("/docs/Report.pdf", true, false).HoldsLock);
    }

    [Fact]
    public void The_Helper_hears_about_every_copy_with_support_on()
    {
        Assert.True(Asking().TellsHelper);
        Assert.True(Asking().Answered(State()).TellsHelper);
        Assert.True(Asking(ReadOnlyCopy).Answered(State(readOnly: true)).TellsHelper);
        Assert.True(Asking().Answered(null).TellsHelper);
        Assert.False(WindowDocument.Opened(Copy, false, true).TellsHelper);
        Assert.False(WindowDocument.Opened("/docs/Report.pdf", true, false).TellsHelper);
    }

    [Fact]
    public void The_Save_window_starts_with_the_documents_name()
    {
        Assert.Equal("Settlement Agreement", Asking().Answered(State()).NameForHealdan);
        Assert.Equal("Settlement Agreement", Asking().Answered(null).NameForHealdan);
        Assert.Equal("Report", WindowDocument.Opened("/docs/Report.pdf", true, false).NameForHealdan);
        Assert.Equal("Report #1 v2", WindowDocument.Opened("/docs/Report #1 v2.pdf", true, false).NameForHealdan);
    }
}
