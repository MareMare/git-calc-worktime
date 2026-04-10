using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace GitCalcWorktime;

/// <summary>
/// <c>git log</c> を実行してコミット情報を非同期に読み取るクラス。
/// </summary>
internal static class GitLogReader
{
    // git log の出力フォーマット: "<hash> <ISO 8601 形式の日時+オフセット>"
    // 例: 181971ff7774853fceb0459966177d51eeab032c 2024-04-26 19:53:58 +0900
    private const string _logFormat = "%H %ai";

    /// <summary>
    /// 指定リポジトリで <c>git log</c> を実行し、コミット情報を非同期に列挙します。
    /// </summary>
    /// <param name="repositoryDir">リポジトリのディレクトリパス。</param>
    /// <param name="authorName">フィルタする Git ユーザ名。</param>
    /// <param name="localTimeZone">コミット日時を変換するローカルタイムゾーン。</param>
    /// <param name="lowerDate">集計開始日 (ローカルタイムゾーン基準、省略可)。</param>
    /// <param name="upperDate">集計終了日 (ローカルタイムゾーン基準、省略可)。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns><see cref="CommitEntry"/> の非同期シーケンス。</returns>
    public static async IAsyncEnumerable<CommitEntry> ReadAsync(
        string repositoryDir,
        string authorName,
        TimeZoneInfo localTimeZone,
        DateOnly? lowerDate,
        DateOnly? upperDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var process = StartGitLog(repositoryDir, authorName);

        await foreach (var line in ReadLinesAsync(process, cancellationToken).ConfigureAwait(false))
        {
            if (!TryParseLine(line, localTimeZone, out var entry))
            {
                continue;
            }

            // 日付フィルタ
            if (lowerDate.HasValue && entry.LocalDate < lowerDate.Value)
            {
                continue;
            }

            if (upperDate.HasValue && entry.LocalDate > upperDate.Value)
            {
                continue;
            }

            yield return entry;
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"git log failed (exit code {process.ExitCode}): {stderr.Trim()}");
        }
    }

    private static Process StartGitLog(string repositoryDir, string authorName)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = "git",
            WorkingDirectory       = repositoryDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding  = System.Text.Encoding.UTF8,
        };

        psi.ArgumentList.Add("log");
        psi.ArgumentList.Add($"--author={authorName}");
        psi.ArgumentList.Add($"--format={_logFormat}");

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        return process;
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(
        Process process,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line;
            }
        }
    }

    /// <summary>
    /// git log の 1 行を解析して <see cref="CommitEntry"/> に変換します。
    /// </summary>
    /// <remarks>
    /// フォーマット: <c>HASH yyyy-MM-dd HH:mm:ss ±HHmm</c>
    /// 例: <c>abc123 2024-04-26 19:53:58 +0900</c>
    /// </remarks>
    private static bool TryParseLine(
        string line,
        TimeZoneInfo localTimeZone,
        out CommitEntry entry)
    {
        entry = default;

        // トークンに分割: [hash, date, time, offset]
        var tokens = line.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 4)
        {
            return false;
        }

        var hash       = tokens[0];
        var datePart   = tokens[1]; // yyyy-MM-dd
        var timePart   = tokens[2]; // HH:mm:ss
        var offsetPart = tokens[3]; // ±HHmm  (例: +0900, -0500)

        // DateTimeOffset を解析
        // git の %ai は "+0900" 形式 (コロンなし) のオフセットを出力する。
        // "zzz" は "+09:00" (コロンあり)、"zzzz" は "+0900" (コロンなし) に対応する。
        if (!DateTimeOffset.TryParseExact(
                $"{datePart} {timePart} {offsetPart}",
                "yyyy-MM-dd HH:mm:ss zzzz",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var commitTime))
        {
            return false;
        }

        // ローカルタイムゾーンに変換
        var localTime = TimeZoneInfo.ConvertTime(commitTime, localTimeZone);

        entry = new CommitEntry(hash, localTime);
        return true;
    }
}
