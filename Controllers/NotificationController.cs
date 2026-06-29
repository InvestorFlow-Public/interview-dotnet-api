using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using interview_dotnet_api.Data;
using interview_dotnet_api.DTOs;
using interview_dotnet_api.Models;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace interview_dotnet_api.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    // TASK 1: Missing authentication attribute here
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private static readonly Dictionary<string, string[]> CategoryKeywords = new()
        {
            ["Marketing"] = ["sale", "discount", "offer", "promotion", "newsletter"],
            ["Security"] = ["password", "login", "security", "verification", "suspicious"],
            ["Billing"] = ["invoice", "payment", "billing", "subscription", "charge"],
            ["System"] = ["maintenance", "update", "outage", "system", "service"]
        };

        private readonly NotificationDbContext _context;

        public NotificationController(NotificationDbContext context)
        {
            _context = context;
        }

        // GET: api/notifications
        [HttpGet]
        public async Task<ActionResult<IEnumerable<NotificationItem>>> GetNotifications()
        {
            var userId = GetUserId();

            return await _context.Notifications
                .Where(notification => notification.UserId == userId)
                .ToListAsync();
        }

        // GET: api/notifications/5
        [HttpGet("{id}")]
        public async Task<ActionResult<NotificationItem>> GetNotification(int id)
        {
            var userId = GetUserId();
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(notification => notification.Id == id && notification.UserId == userId);

            if (notification == null)
            {
                return NotFound();
            }

            return notification;
        }

        // POST: api/notifications
        [HttpPost]
        public async Task<ActionResult<NotificationItem>> PostNotification(CreateNotificationDto dto)
        {
            var userId = GetUserId();

            var notification = new NotificationItem
            {
                Title = dto.Title,
                Message = dto.Message,
                UserId = userId,
                Category = CategorizeNotification(dto.Title, dto.Message),
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetNotification), new { id = notification.Id }, notification);
        }

        // PUT: api/notifications/5/read
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = GetUserId();
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(notification => notification.Id == id && notification.UserId == userId);

            if (notification == null)
            {
                return NotFound();
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/notifications/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var userId = GetUserId();
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(notification => notification.Id == id && notification.UserId == userId);

            if (notification == null)
            {
                return NotFound();
            }

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private string GetUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("Authenticated token is missing a user id claim.");
            }

            return userId;
        }

        private static string CategorizeNotification(string title, string message)
        {
            var content = $"{title} {message}";

            foreach (var category in CategoryKeywords)
            {
                if (category.Value.Any(keyword => content.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    return category.Key;
                }
            }

            return "System";
        }
    }
}
