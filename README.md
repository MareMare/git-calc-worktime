# git-calc-worktime

`git log --author="$GIT_USER_NAME" --format="%H %ai"` の結果を集計し、  
コミット時刻の **平日 (月〜金) / 週末 (土・日)** 別・時間帯別の分布を表示する .NET コンソールアプリです。

[Ivan Bessarabov 氏のブログ記事](https://ivan.bessarabov.com/blog/famous-programmers-work-time-part-2-workweek-vs-weekend) で使用された
[Perl スクリプト](https://gist.github.com/bessarabov/30aee15c5a7c438fe5f9f3f623222b39) および
[devlights/git-calc-worktime (Go 版)](https://github.com/devlights/git-calc-worktime) を参考に、C#14 / .NET 10 で再実装したものです。

## 動作要件

| 項目 | バージョン |
|------|-----------|
| .NET | 10.0 以上 |
| git  | パスが通っていること |
| OS   | Windows / macOS / Linux |

## ビルド

```bash
cd src
dotnet build -c Release
```

## 使い方

```bash
gcw-net --user <Gitユーザ名> --dir <リポジトリパス> [オプション]
```

### オプション一覧

| オプション | 既定値 | 説明 |
|-----------|--------|------|
| `--user <USER>` | *(必須)* | 集計対象の Git ユーザ名 |
| `--dir <DIR>` | `.` | リポジトリのディレクトリパス |
| `--tz <TZ>` | `Asia/Tokyo` | ローカルタイムゾーン (IANA 形式) |
| `--lower-date <DATE>` | *(省略可)* | 集計開始日 `yyyy-MM-dd` (指定 TZ 基準、**当日を含む**) |
| `--upper-date <DATE>` | *(省略可)* | 集計終了日 `yyyy-MM-dd` (指定 TZ 基準、**当日を含む**) |

### 実行例

```bash
# 基本 (カレントディレクトリのリポジトリ、Asia/Tokyo)
gcw-net --user MareMare

# リポジトリパスとタイムゾーンを指定
gcw-net --user MareMare --dir /path/to/repo --tz Asia/Tokyo

# 集計期間を絞り込む
gcw-net --user MareMare --dir /path/to/repo \
    --lower-date 2024-01-01 --upper-date 2024-12-31
```

### 出力イメージ

実際には 00〜23 時の全行が出力されます。以下はコミットが存在する時間帯のみ抜粋したものです。

```
╭─ git-calc-worktime ──────────────────────╮
│                                           │
│  User :  MareMare                         │
│  Dir  :  /path/to/repo                   │
│  TZ   :  (UTC+09:00) 大阪、札幌、東京    │
│  Range:  2024-01-01 ~ 2024-12-31         │
│                                           │
╰───────────────────────────────────────────╯

 hour  Monday to Friday                      Saturday and Sunday
  00       0                                      0
  ...  (中略)
  09       4                                      0
  10     102 **************                       0
  11     115 ***************                      0
  12      27 ***                                  0
  13     132 ******************                   2
  14      92 ************                         4
  15     159 *********************               20 **
  16     182 *************************           25 ***
  17     148 ********************                 3
  18     167 **********************               8 *
  19     167 **********************               4
  20      68 *********                            2
  21      48 ******                               0
  ...  (中略)
  23       0                                      0

 Total   1431  (95.5%)                          68  (4.5%)
 expect  71.4% (if evenly distributed)        28.6% (if evenly distributed)
```

平日が均等配分の期待値 71.4% を上回っているほど「平日中心」の開発スタイル、  
週末が 28.6% を上回っているほど「週末も活発」な開発スタイルを示します。

## プロジェクト構成

```
src/
├── GitCalcWorktime.csproj   # プロジェクト定義
├── Program.cs               # エントリポイント (トップレベルステートメント)
├── WorktimeSettings.cs      # CLI 引数定義 (Spectre.Console.Cli)
├── WorktimeCommand.cs       # コマンド本体・バリデーション・画面出力
├── CommitEntry.cs           # git log 1 行分のデータ構造
├── CommitStats.cs           # 時間帯 × 平日/週末の集計ロジック
└── GitLogReader.cs          # git log 実行・行パース
```

## 依存パッケージ

| パッケージ | バージョン |
|-----------|-----------|
| [Spectre.Console](https://spectreconsole.net/) | 0.55.0 |
| [Spectre.Console.Cli](https://spectreconsole.net/cli/) | 0.55.0 |

## 実装メモ

**タイムゾーン変換**  
`git log --format="%ai"` が出力するオフセット付き日時 (`+0900` 形式) を `DateTimeOffset` で受け取り、`--tz` で指定されたローカルタイムゾーンに変換してから曜日・時刻を判定します。UTC ではなくローカル時刻基準で集計するため、深夜コミットが正しい曜日に分類されます。

**平日 / 週末の判定**  
`DayOfWeek.Saturday` および `DayOfWeek.Sunday` を週末、それ以外を平日として区別します。祝日は考慮しません (元の Perl スクリプトと同仕様)。

**ストリーミング処理**  
`git log` の標準出力を `IAsyncEnumerable<string>` で行ごとに読み込み、全出力をメモリに溜めることなく集計します。大規模リポジトリでもメモリ使用量が増大しません。

**集計期間のフィルタリング**  
`--lower-date` および `--upper-date` はいずれも**当日を含む閉区間**で適用されます。たとえば `--lower-date 2024-01-01 --upper-date 2024-12-31` と指定した場合、2024-01-01 と 2024-12-31 のコミットはどちらも集計対象に含まれます。日付の比較は `--tz` で指定したタイムゾーンに変換した後のローカル日付で行います。


`Spectre.Console.Cli` の `ValidationResult` が `internal` であるため `CommandSettings.Validate()` のオーバーライドは行わず、`ExecuteAsync` 冒頭の `TryValidate()` メソッドで検証しています。

## 参考

- [At what time of day do famous programmers work? Part 2. Workweek vs Weekend.](https://ivan.bessarabov.com/blog/famous-programmers-work-time-part-2-workweek-vs-weekend)
- [Script (Perl)](https://gist.github.com/bessarabov/30aee15c5a7c438fe5f9f3f623222b39)
- [devlights/git-calc-worktime (Go 版)](https://github.com/devlights/git-calc-worktime)

## ライセンス

MIT
