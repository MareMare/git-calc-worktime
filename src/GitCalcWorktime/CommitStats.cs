namespace GitCalcWorktime;

/// <summary>
/// 時間帯ごとの平日・週末コミット数を集計した結果。
/// </summary>
internal sealed class CommitStats
{
    private readonly int[] _weekday = new int[24];
    private readonly int[] _weekend = new int[24];

    /// <summary>平日 (月〜金) の時間帯別コミット数。インデックスは 0〜23 時。</summary>
    public IReadOnlyList<int> Weekday => _weekday;

    /// <summary>週末 (土・日) の時間帯別コミット数。インデックスは 0〜23 時。</summary>
    public IReadOnlyList<int> Weekend => _weekend;

    /// <summary>平日の合計コミット数。</summary>
    public int TotalWeekday => _weekday.Sum();

    /// <summary>週末の合計コミット数。</summary>
    public int TotalWeekend => _weekend.Sum();

    /// <summary>全コミット数。</summary>
    public int TotalCommits => this.TotalWeekday + this.TotalWeekend;

    /// <summary>平日・週末を含む全時間帯の最大コミット数。</summary>
    public int MaxCount => Math.Max(_weekday.Max(), _weekend.Max());

    /// <summary>
    /// コミットエントリを集計に追加します。
    /// </summary>
    public void Add(CommitEntry entry)
    {
        var bucket = entry.IsWeekday ? _weekday : _weekend;
        bucket[entry.LocalHour]++;
    }

    /// <summary>
    /// <see cref="IAsyncEnumerable{T}"/> から集計を構築します。
    /// </summary>
    public static async Task<CommitStats> BuildAsync(
        IAsyncEnumerable<CommitEntry> entries,
        CancellationToken cancellationToken = default)
    {
        var stats = new CommitStats();
        await foreach (var entry in entries.WithCancellation(cancellationToken))
        {
            stats.Add(entry);
        }

        return stats;
    }
}
