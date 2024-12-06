using System;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace SimPlazaManager.Commands;

public class SettingsCommand : Command<SettingsCommand.Arguments>
{
    public class Arguments : CommandSettings
    {
        [CommandOption("-e|--edit")]
        [Description("Edit configuration")]
        public bool Edit { get; set; }

        [CommandOption("-r|--reset")]
        [Description("Reset configuration")]
        public bool Reset { get; set; }
    }

    public override ValidationResult Validate([NotNull] CommandContext context, [NotNull] Arguments args)
    {
        if (args.Edit && args.Reset)
            return ValidationResult.Error("Can't reset and edit at the same time !");
        return base.Validate(context, args);
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arguments args)
    {
        if (!args.Edit && !args.Reset)
        {
            TextPath community_path = new(Settings.CommunityFolder())
            {
                RootStyle = new Style(Color.Red),
                SeparatorStyle = new Style(Color.Green3_1),
                StemStyle = new Style(Color.LightYellow3),
                LeafStyle = new Style(Color.DeepSkyBlue1)
            };

            TextPath mods_path = new(Settings.ModsFolder())
            {
                RootStyle = new Style(Color.Red),
                SeparatorStyle = new Style(Color.Green3_1),
                StemStyle = new Style(Color.LightYellow3),
                LeafStyle = new Style(Color.DeepSkyBlue1)
            };

            Panel community_panel = new(community_path)
            {
                Header = new PanelHeader("| Community Folder |"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(community_panel);

            Panel mods_panel = new(mods_path)
            {
                Header = new PanelHeader("| Mods Folder |"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(mods_panel);

            TextPath version_path = new(Settings.SimVersion().ToString())
            {
                RootStyle = new Style(Color.Red),
                SeparatorStyle = new Style(Color.Green3_1),
                StemStyle = new Style(Color.LightYellow3),
                LeafStyle = new Style(Color.DeepSkyBlue1)
            };

            Panel sim_version_panel = new (version_path)
            {
                Header = new PanelHeader("| MSFS Version |"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(sim_version_panel);
        }

        if (args.Reset)
            Settings.ResetConfig();

        if (args.Edit)
        {
            string community = AnsiConsole.Prompt(
                                    new TextPrompt<string>("Enter [green]Community Folder[/] full path [grey](empty to stay with old config)[/]:")
                                    .AllowEmpty());
            string mods = AnsiConsole.Prompt(
                                    new TextPrompt<string>("Enter [green]Mods Folder[/] full path [grey](empty to stay with old config)[/]:")
                                    .AllowEmpty());
            uint sim_version = AnsiConsole.Prompt(
                                    new TextPrompt<uint>("Enter [green]MSFS Version[/] [grey](empty to stay with old config)[/]:")
                                    .AllowEmpty());

            if (community.Length == 0)
                community = Settings.CommunityFolder();
            if (mods.Length == 0)
                mods = Settings.ModsFolder();
            if (sim_version == 0)
                sim_version = Settings.SimVersion();

            if (sim_version != 2024 && sim_version != 2020)
                throw new InvalidOperationException("Invalid MSFS version (2020 and 2024 are supported)");

            Settings.SetConfig(community, mods, sim_version);
        }
        return 0;
    }
}
