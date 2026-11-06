using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;

string? url = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("TIME_ENTRIES_URL");
string outputFile = "EmployeeTimeWorked_PieChart.png";

string json;
if (!string.IsNullOrWhiteSpace(url))
{
    try
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        json = await http.GetStringAsync(url);
        Console.WriteLine("Data fetched from REST endpoint.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Could not fetch endpoint ({ex.Message}). Using sample data.");
        json = SampleData.Json;
    }
}
else
{
    Console.WriteLine("No endpoint supplied. Using sample data.");
    json = SampleData.Json;
}


var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var entries = JsonSerializer.Deserialize<List<TimeEntry>>(json, options) ?? new();

var totals = entries
    .Where(e => e.DeletedOn == null
             && !string.IsNullOrWhiteSpace(e.EmployeeName)
             && e.EndTimeUtc > e.StarTimeUtc)
    .GroupBy(e => e.EmployeeName!.Trim(), StringComparer.OrdinalIgnoreCase)
    .Select(g => new
    {
        Name = g.Key,
        Hours = g.Sum(e => (e.EndTimeUtc - e.StarTimeUtc).TotalHours)
    })
    .OrderByDescending(x => x.Hours)
    .ToList();

if (totals.Count == 0)
{
    Console.WriteLine("No valid time entries found.");
    return;
}

double grandTotal = totals.Sum(t => t.Hours);


Color[] palette =
{
    Color.FromArgb(66,133,244),  Color.FromArgb(234,67,53),   Color.FromArgb(251,188,5),
    Color.FromArgb(52,168,83),   Color.FromArgb(171,71,188),  Color.FromArgb(255,112,67),
    Color.FromArgb(0,172,193),   Color.FromArgb(158,157,36),  Color.FromArgb(92,107,192),
    Color.FromArgb(141,110,99),  Color.FromArgb(236,64,122),  Color.FromArgb(38,166,154)
};

int legendRowHeight = 28;
int width = 1000;
int height = Math.Max(700, 150 + totals.Count * legendRowHeight + 60);

using var bmp = new Bitmap(width, height);
using var g = Graphics.FromImage(bmp);
g.SmoothingMode = SmoothingMode.AntiAlias;
g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
g.Clear(Color.White);

using var titleFont = new Font("Segoe UI", 20, FontStyle.Bold);
using var legendFont = new Font("Segoe UI", 11);
using var subFont = new Font("Segoe UI", 10, FontStyle.Italic);

g.DrawString("Percentage of Total Time Worked by Employee", titleFont, Brushes.Black, 40, 25);
g.DrawString($"Total hours: {grandTotal:N1}", subFont, Brushes.Gray, 42, 65);

var pieRect = new Rectangle(50, 110, 480, 480);
float startAngle = -90f;

for (int i = 0; i < totals.Count; i++)
{
    float sweep = (float)(totals[i].Hours / grandTotal * 360.0);
    using var brush = new SolidBrush(palette[i % palette.Length]);
    g.FillPie(brush, pieRect, startAngle, sweep);
    using var pen = new Pen(Color.White, 2);
    g.DrawPie(pen, pieRect, startAngle, sweep);

    
    double pct = totals[i].Hours / grandTotal * 100;
    if (pct >= 4)
    {
        double mid = (startAngle + sweep / 2) * Math.PI / 180.0;
        float cx = pieRect.X + pieRect.Width / 2f + (float)(Math.Cos(mid) * pieRect.Width * 0.33);
        float cy = pieRect.Y + pieRect.Height / 2f + (float)(Math.Sin(mid) * pieRect.Height * 0.33);
        var label = $"{pct:0.#}%";
        var size = g.MeasureString(label, legendFont);
        g.DrawString(label, legendFont, Brushes.White, cx - size.Width / 2, cy - size.Height / 2);
    }
    startAngle += sweep;
}


int lx = 600, ly = 120;
for (int i = 0; i < totals.Count; i++)
{
    double pct = totals[i].Hours / grandTotal * 100;
    using var brush = new SolidBrush(palette[i % palette.Length]);
    g.FillRectangle(brush, lx, ly + i * legendRowHeight, 18, 18);
    g.DrawString($"{totals[i].Name} - {pct:0.0}% ({totals[i].Hours:N1} h)",
                 legendFont, Brushes.Black, lx + 28, ly + i * legendRowHeight - 2);
}

// 5. Save
bmp.Save(outputFile, ImageFormat.Png);
Console.WriteLine($"Pie chart saved: {Path.GetFullPath(outputFile)}");


public class TimeEntry
{
    public string? Id { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime StarTimeUtc { get; set; }   
    public DateTime EndTimeUtc { get; set; }
    public string? EntryNotes { get; set; }
    public DateTime? DeletedOn { get; set; }
}

public static class SampleData
{
    public const string Json = """
    [
      { "Id": "1",  "EmployeeName": "Raja",   "StarTimeUtc": "2026-10-05T04:00:00Z", "EndTimeUtc": "2026-10-05T12:30:00Z", "EntryNotes": "Backend work", "DeletedOn": null },
      { "Id": "2",  "EmployeeName": "Raja",   "StarTimeUtc": "2026-10-06T04:00:00Z", "EndTimeUtc": "2026-10-06T13:00:00Z", "EntryNotes": "Code review",  "DeletedOn": null },
      { "Id": "3",  "EmployeeName": "Monica", "StarTimeUtc": "2026-10-05T05:00:00Z", "EndTimeUtc": "2026-10-05T11:00:00Z", "EntryNotes": "UI design",    "DeletedOn": null },
      { "Id": "4",  "EmployeeName": "Monica", "StarTimeUtc": "2026-10-06T05:00:00Z", "EndTimeUtc": "2026-10-06T12:00:00Z", "EntryNotes": "Prototyping",  "DeletedOn": null },
      { "Id": "5",  "EmployeeName": "Vijay",  "StarTimeUtc": "2026-10-05T03:30:00Z", "EndTimeUtc": "2026-10-05T14:00:00Z", "EntryNotes": "Deployment",   "DeletedOn": null },
      { "Id": "6",  "EmployeeName": "Priya",  "StarTimeUtc": "2026-10-05T06:00:00Z", "EndTimeUtc": "2026-10-05T10:00:00Z", "EntryNotes": "Testing",      "DeletedOn": null },
      { "Id": "7",  "EmployeeName": "Priya",  "StarTimeUtc": "2026-10-07T06:00:00Z", "EndTimeUtc": "2026-10-07T12:00:00Z", "EntryNotes": "QA",           "DeletedOn": null },
      { "Id": "8",  "EmployeeName": "Arjun",  "StarTimeUtc": "2026-10-06T04:30:00Z", "EndTimeUtc": "2026-10-06T09:30:00Z", "EntryNotes": "Support",      "DeletedOn": null },
      { "Id": "9",  "EmployeeName": "Kavya",  "StarTimeUtc": "2026-10-07T05:00:00Z", "EndTimeUtc": "2026-10-07T13:30:00Z", "EntryNotes": "Data analysis","DeletedOn": null },
      { "Id": "10", "EmployeeName": "Vijay",  "StarTimeUtc": "2026-10-07T03:30:00Z", "EndTimeUtc": "2026-10-07T08:00:00Z", "EntryNotes": "Hotfix",       "DeletedOn": null }
    ]
    """;
}
