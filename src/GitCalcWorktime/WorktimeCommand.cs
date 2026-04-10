using Spectre.Console;
using Spectre.Console.Cli;

namespace GitCalcWorktime;

/// <summary>
/// <c>git log</c> のコミット時刻を集計し、平日・週末ごとの時間帯分布を表示するコマンド。
/// </summary>
#pragma warning disable CA1812
// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class WorktimeCommand : AsyncCommand<WorktimeSettings>
#pragma warning restore CA1812
{
    // バーグラフで使用する最大スター数 (Perl スクリプトに合わせて 25)
    private const int _maxBarWidth = 25;

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        WorktimeSettings settings,
        CancellationToken cancellationToken)
    {
        // ── バリデーション ────────────────────────────────────────────────────
        // CommandSettings.Validate() の戻り値型が internal のためオーバーライド不可。
        // 代わりに ExecuteAsync 冒頭で検証し、エラーがあれば早期リターンする。
        if (!TryValidate(settings, out var validationError))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(validationError)}");
            return 1;
        }

        // ── 前処理 ───────────────────────────────────────────────────────────
        var tz        = TimeZoneInfo.FindSystemTimeZoneById(settings.Tz);
        var lowerDate = settings.LowerDate is not null
            ? DateOnly.ParseExact(settings.LowerDate, "yyyy-MM-dd")
            : (DateOnly?)null;
        var upperDate = settings.UpperDate is not null
            ? DateOnly.ParseExact(settings.UpperDate, "yyyy-MM-dd")
            : (DateOnly?)null;

        // ヘッダー情報の表示
        RenderHeader(settings, tz, lowerDate, upperDate);

        CommitStats stats;

        try
        {
            stats = await AnsiConsole
                .Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync(
                    $"Running [cyan]git log[/] for [green]{Markup.Escape(settings.User)}[/] ...",
                    async _ =>
                    {
                        var entries = GitLogReader.ReadAsync(
                            settings.Dir,
                            settings.User,
                            tz,
                            lowerDate,
                            upperDate,
                            cancellationToken);

                        return await CommitStats.BuildAsync(entries, cancellationToken).ConfigureAwait(false);
                    })
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }

        if (stats.TotalCommits == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No commits found for the specified criteria.[/]");
            return 0;
        }

        RenderStats(stats);
        return 0;
    }

    // ─────────────────────────────────────────────────
    // Validation
    // ─────────────────────────────────────────────────

    private static bool TryValidate(WorktimeSettings settings, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(settings.User))
        {
            errorMessage = "--user is required.";
            return false;
        }

        if (!Directory.Exists(settings.Dir))
        {
            errorMessage = $"Directory not found: {settings.Dir}";
            return false;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(settings.Tz);
        }
        catch (TimeZoneNotFoundException)
        {
            errorMessage = $"Unknown timezone: '{settings.Tz}'. Use IANA timezone ID (e.g. Asia/Tokyo).";
            return false;
        }

        if (settings.LowerDate is not null && !DateOnly.TryParseExact(settings.LowerDate, "yyyy-MM-dd", out _))
        {
            errorMessage = $"--lower-date must be in yyyy-MM-dd format, got: '{settings.LowerDate}'";
            return false;
        }

        if (settings.UpperDate is not null && !DateOnly.TryParseExact(settings.UpperDate, "yyyy-MM-dd", out _))
        {
            errorMessage = $"--upper-date must be in yyyy-MM-dd format, got: '{settings.UpperDate}'";
            return false;
        }

        if (settings.LowerDate is not null && settings.UpperDate is not null)
        {
            var lower = DateOnly.ParseExact(settings.LowerDate, "yyyy-MM-dd");
            var upper = DateOnly.ParseExact(settings.UpperDate, "yyyy-MM-dd");
            if (lower > upper)
            {
                errorMessage = "--lower-date must be earlier than or equal to --upper-date.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    // ─────────────────────────────────────────────────
    // Rendering helpers
    // ─────────────────────────────────────────────────

    private static void RenderHeader(
        WorktimeSettings settings,
        TimeZoneInfo tz,
        DateOnly? lowerDate,
        DateOnly? upperDate)
    {
        AnsiConsole.WriteLine();

        var panel = new Panel(
            new Rows(
                new Markup($"  [bold]User :[/]  [green]{Markup.Escape(settings.User)}[/]"),
                new Markup($"  [bold]Dir  :[/]  [cyan]{Markup.Escape(Path.GetFullPath(settings.Dir))}[/]"),
                new Markup($"  [bold]TZ   :[/]  {Markup.Escape(tz.DisplayName)} ([dim]{Markup.Escape(settings.Tz)}[/])"),
                new Markup($"  [bold]Range:[/]  {FormatDateRange(lowerDate, upperDate)}")))
        {
            Header  = new PanelHeader(" git-calc-worktime ", Justify.Center),
            Border  = BoxBorder.Rounded,
            Padding = new Padding(1, 0),
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private static string FormatDateRange(DateOnly? lower, DateOnly? upper) =>
        (lower, upper) switch
        {
            (null,  null)  => "[dim]all time[/]",
            (var l, null)  => $"[yellow]{l:yyyy-MM-dd}[/] ~ [dim](no limit)[/]",
            (null,  var u) => "[dim](no limit)[/] ~ " + $"[yellow]{u:yyyy-MM-dd}[/]",
            (var l, var u) => $"[yellow]{l:yyyy-MM-dd}[/] ~ [yellow]{u:yyyy-MM-dd}[/]",
        };

    private static void RenderStats(CommitStats stats)
    {
        var table = BuildTable(stats);
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static Table BuildTable(CommitStats stats)
    {
        var total         = stats.TotalCommits;
        var weekdayTotal  = stats.TotalWeekday;
        var weekendTotal  = stats.TotalWeekend;
        var max           = stats.MaxCount;

        var table = new Table()
            .Border(TableBorder.Simple)
            .BorderStyle(Style.Parse("dim"))
            .AddColumn(new TableColumn("[bold]hour[/]").RightAligned().Width(6))
            .AddColumn(new TableColumn("[bold]Monday to Friday[/]").LeftAligned().Width(36))
            .AddColumn(new TableColumn("[bold]Saturday and Sunday[/]").LeftAligned().Width(36));

        for (var hour = 0; hour < 24; hour++)
        {
            var wd = stats.Weekday[hour];
            var we = stats.Weekend[hour];

            var wdBar = BuildBar(wd, max);
            var weBar = BuildBar(we, max);

            table.AddRow(
                $"[dim]{hour:D2}[/]",
                wd > 0
                    ? $"[cyan]{wd,6}[/] [green]{wdBar}[/]"
                    : $"[dim]{wd,6}[/]",
                we > 0
                    ? $"[yellow]{we,6}[/] [green]{weBar}[/]"
                    : $"[dim]{we,6}[/]");
        }

        // Separator 行
        table.AddEmptyRow();

        // Total 行
        var wdPct = weekdayTotal * 100.0 / total;
        var wePct = weekendTotal * 100.0 / total;

        table.AddRow(
            "[bold]Total[/]",
            $"[bold cyan]{weekdayTotal,6}[/] [bold]({wdPct:F1}%)[/]",
            $"[bold yellow]{weekendTotal,6}[/] [bold]({wePct:F1}%)[/]");

        // 参考: 均等配分時の期待値 (平日 5/7 ≈ 71.4%、週末 2/7 ≈ 28.6%)
        table.AddEmptyRow();
        table.AddRow(
            "[dim]expect[/]",
            "[dim]  71.4%[/] [dim](if evenly distributed)[/]",
            "[dim]  28.6%[/] [dim](if evenly distributed)[/]");

        return table;
    }

    /// <summary>
    /// コミット数を最大値に対する割合でスター文字列に変換します。
    /// </summary>
    private static string BuildBar(int count, int max)
    {
        if (max == 0 || count == 0)
        {
            return string.Empty;
        }

        var stars = (int)Math.Round((double)count / max * _maxBarWidth);
        return new string('*', Math.Max(stars, count > 0 ? 1 : 0));
    }
}
