using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Data;
using Restaurant.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Restaurant.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OrdersController(AppDbContext context)
        {
            _context = context;
        }

        // 1. جلب كافة الطلبات مفلترة برقم المطعم (للدشابورد)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetOrders([FromQuery] int restaurantId)
        {
            // أمان: إذا لم يتم إرسال رقم مطعم صحيح نرجع قائمة فارغة
            if (restaurantId <= 0) return Ok(new List<object>());

            var orders = await _context.Orders
                .Where(o => o.RestaurantId == restaurantId)
                .Include(o => o.Customer)
                .Include(o => o.OrderItems!)
                    .ThenInclude(oi => oi.MenuItem)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            // تحويل البيانات لشكل مبسط (DTO) لسهولة العرض في الفرونت إند
            var result = orders.Select(o => new {
                o.OrderId,
                o.OrderDate,
                o.Status, // (Placed, Preparing, Ready, Delivered, Cancelled)
                o.TotalAmount,
                CustomerName = o.Customer?.FullName ?? "زبون خارجي",
                CustomerPhone = o.Customer?.PhoneNumber ?? "بلا رقم",
                ItemsCount = o.OrderItems?.Count ?? 0,
                Items = o.OrderItems?.Select(i => new {
                    i.OrderItemId,
                    MenuName = i.MenuItem?.Name ?? "صنف غير معروف",
                    i.Quantity,
                    i.UnitPrice,
                    SubTotal = i.Quantity * i.UnitPrice
                })
            });

            return Ok(result);
        }

        // 2. جلب تفاصيل طلب واحد محدد
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetOrder(int id)
        {
            var o = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems!)
                    .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (o == null) return NotFound(new { message = "الطلب غير موجود" });

            return Ok(new {
                o.OrderId,
                o.RestaurantId,
                o.OrderDate,
                o.Status,
                o.TotalAmount,
                CustomerName = o.Customer?.FullName ?? "زبون",
                CustomerPhone = o.Customer?.PhoneNumber ?? "",
                Items = o.OrderItems?.Select(i => new {
                    MenuName = i.MenuItem?.Name ?? "صنف",
                    i.Quantity,
                    i.UnitPrice,
                    Total = i.Quantity * i.UnitPrice
                })
            });
        }

        // 3. إنشاء طلب جديد
        [HttpPost]
        public async Task<ActionResult<Order>> PostOrder(Order order)
        {
            if (order.OrderDate == default) order.OrderDate = DateTime.Now;
            
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetOrder", new { id = order.OrderId }, order);
        }

        // 4. تحديث حالة الطلب أو بياناته (مثلاً تغيير الحالة من Preparing إلى Ready)
        [HttpPut("{id}")]
        public async Task<IActionResult> PutOrder(int id, Order order)
        {
            if (id != order.OrderId) return BadRequest(new { message = "ID mismatch" });

            _context.Entry(order).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Orders.Any(e => e.OrderId == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // 5. حذف طلب
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}