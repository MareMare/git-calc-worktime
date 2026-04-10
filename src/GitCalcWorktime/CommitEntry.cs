namespace GitCalcWorktime;

/// <summary>
/// git log の 1 行分のコミット情報。
/// </summary>
/// <param name="Hash">コミットハッシュ。</param>
/// <param name="LocalDateTime">ローカルタイムゾーンに変換済みのコミット日時。</param>
internal readonly record struct CommitEntry(string Hash, DateTimeOffset LocalDateTime)
{
    /// <summary>ローカル日付を取得します。</summary>
    public DateOnly LocalDate => DateOnly.FromDateTime(this.LocalDateTime.DateTime);

    /// <summary>ローカル時刻の時 (0〜23) を取得します。</summary>
    public int LocalHour => this.LocalDateTime.Hour;

    /// <summary>
    /// 月曜〜金曜であれば <see langword="true"/>、土曜・日曜であれば <see langword="false"/> を返します。
    /// </summary>
    public bool IsWeekday => this.LocalDateTime.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
}
