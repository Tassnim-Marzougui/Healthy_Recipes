using Microsoft.AspNetCore.Mvc;
using Healthy_Recipes.Data;
using Healthy_Recipes.Models;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Healthy_Recipes.ViewModels;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Healthy_Recipes.Controllers
{
        public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var recipes = await _context.Recipes.ToListAsync();
            return View(recipes);
        }

        public IActionResult About()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Subscribe(string email)
        {
            try
            {
                // TODO: Implémenter la logique d'inscription à la newsletter
                await Task.Delay(1000); // Simuler une opération asynchrone
                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                return RedirectToAction("Index");
            }
        }
    }
}