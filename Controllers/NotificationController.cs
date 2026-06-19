using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using interview_dotnet_api.Data;
using interview_dotnet_api.DTOs;
using interview_dotnet_api.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using interview_dotnet_api.Services;

namespace interview_dotnet_api.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    
    public class NotificationController : ControllerBase
    {
        private readonly NotificationDbContext _context;

        public NotificationController(NotificationDbContext context)
        {
            _context = context;
        }


        // GET: api/notifications
        [HttpGet]
        public async Task<ActionResult<IEnumerable<NotificationItem>>> GetNotifications()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .ToListAsync();
        }

        // GET: api/notifications/5
        [HttpGet("{id}")]
        public async Task<ActionResult<NotificationItem>> GetNotification(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);

            if (notification == null)
            {
                return NotFound();
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (notification.UserId != userId)
            {
                return Forbid();
            }

            return notification;
        }

        // POST: api/notifications
        [HttpPost]
        public async Task<ActionResult<NotificationItem>> PostNotification(CreateNotificationDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var notification = new NotificationItem
            {
                Title = dto.Title,
                Message = dto.Message,
                UserId = userId!,
                Category = NotificationCategorizer.Categorize(dto.Title, dto.Message),
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
            var notification = await _context.Notifications.FindAsync(id);

            if (notification == null)
            {
                return NotFound();
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (notification.UserId != userId)
            {
                return Forbid();
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/notifications/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);

            if (notification == null)
            {
                return NotFound();
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (notification.UserId != userId)
            {
                return Forbid();
            }

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
