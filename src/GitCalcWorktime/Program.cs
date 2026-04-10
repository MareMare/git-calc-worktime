using GitCalcWorktime;
using Spectre.Console.Cli;

var app = new CommandApp<WorktimeCommand>();
app.Configure(config =>
{
    config.SetApplicationName("gcw-net");
    config.SetApplicationVersion("1.0.0");
    config.AddExample(["--user", "MareMare", "--dir", "/path/to/repo"]);
    config.AddExample(["--user", "MareMare", "--dir", "/path/to/repo", "--tz", "Asia/Tokyo"]);
    config.AddExample(["--user", "MareMare", "--dir", "/path/to/repo", "--lower-date", "2024-01-01", "--upper-date", "2024-12-31"]);
});

return app.Run(args);
