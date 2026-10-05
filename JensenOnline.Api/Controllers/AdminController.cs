using JensenOnline.Api.Data;
using JensenOnline.Api.Dtos;
using JensenOnline.Api.Data.Entities;
using JensenOnline.Api.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JensenOnline.Api.Controllers;

// HELA controllern kräver rollen Admin (T9). En kund får 403 på allt här.
[ApiController]
[Route("api/admin")]
[Authorize(Roles = DbSeeder.AdminRole)]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public AdminController(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet("orders")]
    public async Task<ActionResult<IReadOnlyList<AdminOrderDto>>> GetAllOrders()
    {
        var orders = await _db.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.Id)
            .Take(200)
            .Select(o => new AdminOrderDto(o.Id, o.User!.Email ?? "", o.CreatedAt, o.TotalAmount, o.Items.Count))
            .ToListAsync();
        return Ok(orders);
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetUsers()
    {
        var users = await _userManager.Users.AsNoTracking().OrderBy(u => u.Email).Take(500).ToListAsync();

        var result = new List<AdminUserDto>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var locked = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow;
            result.Add(new AdminUserDto(u.Id, u.Email ?? "", roles, locked));
        }
        return Ok(result);
    }

    // Admin kan spärra ett konto, t.ex. vid misstänkt kontoövertagande
    [HttpPut("users/{id}/lock")]
    [Consumes("application/json")]
    public async Task<IActionResult> SetLock(string id, SetLockDto dto)
    {
        if (id == User.GetUserId())
            return Problem(title: "Du kan inte låsa ditt eget konto.", statusCode: StatusCodes.Status400BadRequest);

        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (dto.Locked)
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
        }
        return NoContent();
    }
}