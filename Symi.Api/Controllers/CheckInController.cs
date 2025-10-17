using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Symi.Api.Data;

namespace Symi.Api.Controllers;

[ApiController]
[Route("checkin")]
[Authorize(Policy = "Organizer")]
public class CheckInController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<CheckInController> _logger;
    public CheckInController(AppDbContext db, ILogger<CheckInController> logger)
    {
        _db = db; _logger = logger;
    }

    [HttpGet]
    public ContentResult Page()
    {
        var html = @"<!doctype html><html><head><meta charset='utf-8'><title>Check-in</title>
<style>body{font-family:system-ui;margin:20px}input,button{font-size:16px;padding:8px}#log{margin-top:12px;white-space:pre-wrap}</style>
</head><body>
<h1>QR Check-in</h1>
<p>Basit demo: QR token girin veya tarayıcı kamera üzerinden okuyucu eklenebilir.</p>
<input id='token' placeholder='QR token' />
<button onclick='scan()'>Check-in</button>
<pre id='log'></pre>
<script>
async function scan(){
  const token = document.getElementById('token').value.trim();
  if(!token){ alert('Token boş'); return; }
  try{
    const res = await fetch('/checkin/scan', {method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify({qrToken: token})});
    const data = await res.json();
    document.getElementById('log').textContent = JSON.stringify(data,null,2);
  }catch(e){ document.getElementById('log').textContent = e.toString(); }
}
</script>
</body></html>";
        return new ContentResult { Content = html, ContentType = "text/html" };
    }

    public record ScanRequest(string qrToken);

    [HttpPost("scan")]
    public async Task<IActionResult> Scan([FromBody] ScanRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.qrToken)) return BadRequest(new { message = "qrToken required" });
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.QrToken == req.qrToken);
        if (ticket == null) return NotFound(new { message = "Ticket not found" });
        if (ticket.Status == "used") return Conflict(new { message = "Already used" });

        ticket.Status = "used";
        ticket.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { status = "ok", ticketId = ticket.Id, usedAt = ticket.UsedAt });
    }
}