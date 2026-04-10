using System.ComponentModel;
using Spectre.Console.Cli;

namespace GitCalcWorktime;

/// <summary>
/// <c>git-calc-worktime</c> コマンドの引数設定。
/// </summary>
#pragma warning disable CA1812
// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class WorktimeSettings : CommandSettings
#pragma warning restore CA1812
{
    /// <summary>
    /// Git ユーザ名を取得または設定します。
    /// </summary>
    [CommandOption("--user <USER>")]
    [Description("Git username (e.g. MareMare)")]
    public required string User { get; init; }

    /// <summary>
    /// リポジトリのパスを取得または設定します。
    /// </summary>
    [CommandOption("--dir <DIR>")]
    [Description("Path of the git repository")]
    [DefaultValue(".")]
    public string Dir { get; init; } = ".";

    /// <summary>
    /// ローカルタイムゾーンを取得または設定します。
    /// </summary>
    [CommandOption("--tz <TZ>")]
    [Description("Local timezone ID (IANA format, e.g. Asia/Tokyo)")]
    [DefaultValue("Asia/Tokyo")]
    public string Tz { get; init; } = "Asia/Tokyo";

    /// <summary>
    /// 集計対象の開始日 (指定タイムゾーン基準) を取得または設定します。
    /// </summary>
    [CommandOption("--lower-date <DATE>")]
    [Description("Start date of aggregation range in local timezone (yyyy-MM-dd, optional)")]
    public string? LowerDate { get; init; }

    /// <summary>
    /// 集計対象の終了日 (指定タイムゾーン基準) を取得または設定します。
    /// </summary>
    [CommandOption("--upper-date <DATE>")]
    [Description("End date of aggregation range in local timezone (yyyy-MM-dd, optional)")]
    public string? UpperDate { get; init; }
}
