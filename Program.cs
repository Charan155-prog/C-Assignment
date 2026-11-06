
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

const string ApiUrl = "https://rc-vault-fap-live-1.azurewebsites.net/api/gettimeentries?code=vO17RnE8vuzXzPJo5eaLLjXjmRW07law99QTD90zat9FfOQJKKUcgQ==";
const int ThresholdHours = 100;
const string OutputFile = "employees.html";

// Get employee time entries from the API
List<TimeEntry> entries;

try
{
    using var client = new HttpClient();
    var json = await client.GetStringAsync(ApiUrl);

    entries = JsonSerializer.Deserialize<List<TimeEntry>>(json) ?? new List<TimeEntry>();
}
catch (Exception ex)
{
    Console.WriteLine("Could not retrieve time entries: " + ex.Message);
    return 1;
}

// Calculate total working hours for each employee
var employeeTotals = entries
    .Where(e => e.DeletedOn == null &&
                !string.IsNullOrWhiteSpace(e.EmployeeName) &&
                e.EndTimeUtc > e.StartTimeUtc)
    .GroupBy(e => e.EmployeeName.Trim())
    .Select(group => new
    {
        Name = group.Key,
        TotalHours = group.Sum(e => (e.EndTimeUtc - e.StartTimeUtc).TotalHours)
    })
    .OrderByDescending(e => e.TotalHours)
    .ThenBy(e => e.Name)
    .ToList();

// Create HTML report
var html = new StringBuilder();

html.AppendLine("<!DOCTYPE html>");
html.AppendLine("<html lang=\"en\">");
html.AppendLine("<head>");
html.AppendLine("<meta charset=\"utf-8\">");
html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
html.AppendLine("<title>Employee time worked</title>");

html.AppendLine("""
<style>
    body {
        font-family: Segoe UI, Arial, sans-serif;
        margin: 2rem;
        color: #222;
    }

    table {
        border-collapse: collapse;
        min-width: 380px;
    }

    th, td {
        padding: .55rem 1rem;
        border: 1px solid #ccc;
        text-align: left;
    }

    th {
        background: #f0f0f0;
    }

    td.num {
        text-align: right;
        font-variant-numeric: tabular-nums;
    }

    tr.low {
        background: #ffd6d6;
    }

    .legend {
        margin-top: 1rem;
        font-size: .9rem;
    }

    .swatch {
        display: inline-block;
        width: 1em;
        height: 1em;
        background: #ffd6d6;
        border: 1px solid #ccc;
        vertical-align: middle;
        margin-right: .4em;
    }
</style>
""");

html.AppendLine("</head>");
html.AppendLine("<body>");
html.AppendLine("<h1>Employees by total time worked</h1>");
html.AppendLine("<table>");
html.AppendLine("<thead><tr><th>Name</th><th>Total time worked</th></tr></thead>");
html.AppendLine("<tbody>");

// Add each employee to the table
foreach (var employee in employeeTotals)
{
    string rowClass = employee.TotalHours < ThresholdHours ? " class=\"low\"" : "";

    int hours = (int)employee.TotalHours;
    int minutes = (int)(employee.TotalHours * 60) % 60;

    string employeeName = WebUtility.HtmlEncode(employee.Name);
    string totalTime = $"{hours}h {minutes:00}m";

    html.AppendLine(
        $"<tr{rowClass}><td>{employeeName}</td><td class=\"num\">{totalTime}</td></tr>");
}

html.AppendLine("</tbody>");
html.AppendLine("</table>");

html.AppendLine(
    $"<p class=\"legend\"><span class=\"swatch\"></span>Worked less than {ThresholdHours} hours</p>");

html.AppendLine("</body>");
html.AppendLine("</html>");

// Save the HTML file
string filePath = Path.GetFullPath(OutputFile);

await File.WriteAllTextAsync(filePath, html.ToString(), new UTF8Encoding(false));

Console.WriteLine($"Created {filePath}");

// Open the report in the default browser
try
{
    Process.Start(new ProcessStartInfo(filePath)
    {
        UseShellExecute = true
    });
}
catch
{
    // Ignore errors if the browser cannot be opened
}

return 0;

// Employee time entry model
class TimeEntry
{
    public string? EmployeeName { get; set; }

    [JsonPropertyName("StarTimeUtc")]
    public DateTime StartTimeUtc { get; set; }

    public DateTime EndTimeUtc { get; set; }

    public DateTime? DeletedOn { get; set; }
}
