using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using interview_dotnet_api.Data;
using interview_dotnet_api.DTOs;
using interview_dotnet_api.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace interview_dotnet_api.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    // TASK 1: Missing authentication attribute here
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
            // VULNERABILITY: Returns all user notifications to any anonymous caller
            return await _context.Notifications.ToListAsync();
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

            // VULNERABILITY: Allows User A to view User B's notifications via ID tampering
            return notification;
        }

        // POST: api/notifications
        [HttpPost]
        public async Task<ActionResult<NotificationItem>> PostNotification(CreateNotificationDto dto)
        {
            // TASK 1: Needs to extract the logged-in User ID from token claims
            string fallbackUserId = "unauthenticated-user-id"; 

            var notification = new NotificationItem
            {
                Title = dto.Title,
                Message = dto.Message,
                UserId = fallbackUserId,
                CreatedAt = DateTime.UtcNow
            };

            // TASK 2: Trigger AI-Assisted keyword parsing here before database insertion
            // notification.Category = ...

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

            // VULNERABILITY: Allows unauthorized global modifications
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

            // VULNERABILITY: Missing ownership verification before execution
            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
