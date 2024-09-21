using BDA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BDA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ExcelService _excelService;

        public CustomerController(AppDbContext context, ExcelService excelService)
        {
            _context = context;
            _excelService = excelService;
        }
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Customers>>> GetCustomers()
        {
            return await _context.Customer.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Customers>> GetCustomer(int id)
        {
            var customer = await _context.Customer.FindAsync(id);

            if (customer == null)
            {
                return NotFound();
            }

            return customer;
        }

        [HttpPost]
        public async Task<ActionResult<Customers>> AddCustomer(Customers customer)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Eyni email varmı yoxlayır
            var existingCustomer = await _context.Customer.FirstOrDefaultAsync(c => c.Email == customer.Email);
            if (existingCustomer != null)
            {
                return Conflict("Customer with this email already exists.");
            }

            customer.CreatedAt = DateTime.Now; 

            _context.Customer.Add(customer);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customer);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(int id, Customers customer)
        {
            if (id != customer.Id)
            {
                return BadRequest();
            }

            _context.Entry(customer).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomerExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _context.Customer.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            _context.Customer.Remove(customer);
            await _context.SaveChangesAsync();

            return Ok("Customer deleted successfully.");
        }

        [HttpPost("Import")]
        public async Task<IActionResult> ImportCustomers(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Invalid file.");
            }

            // Müvəqqəti saxlama serverdə
            var filePath = Path.GetTempFileName();
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // məlumatların idxalı
            var customers = _excelService.ImportExcelData(filePath);

            if (customers == null || !customers.Any())
            {
                return BadRequest("No valid customers found in the Excel file.");
            }

            //Databasede saxlama
            foreach (var customer in customers)
            {
                if (!CustomerExistsByEmail(customer.Email))
                {
                    customer.CreatedAt = DateTime.Now; 
                    _context.Customer.Add(customer);
                }
            }

            await _context.SaveChangesAsync();
            return Ok($"Successfully imported {customers.Count} customers.");
        }

        private bool CustomerExists(int id)
        {
            return _context.Customer.Any(e => e.Id == id);
        }

        private bool CustomerExistsByEmail(string email)
        {
            return _context.Customer.Any(e => e.Email == email);
        }


    }
}
