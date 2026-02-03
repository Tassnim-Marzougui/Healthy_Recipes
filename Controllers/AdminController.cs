using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Healthy_Recipes.Models;
using Healthy_Recipes.Data;
using Healthy_Recipes.ViewModels;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Http;
using System.IO;
using System;

namespace Healthy_Recipes.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public AdminController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public IActionResult Index()
        {
            var recipes = _context.Recipes.ToList();
            var viewModels = recipes.Select(r => new RecipeViewModel
            {
                Id = r.Id,
                Title = r.Title,
                Description = r.Description,
                Category = r.Category,
                PreparationTime = r.PreparationTime,
                Ingredients = r.Ingredients,
                Instructions = r.Instructions,
                Calories = r.Calories,
                Protein = r.Protein,
                Carbohydrates = r.Carbohydrates,
                Fat = r.Fat,
                ImageUrl = r.ImageUrl
            }).ToList();
            return View(viewModels);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RecipeViewModel model)
        {
            if (ModelState.IsValid)
            {
                var recipe = new Recipe
                {
                    Title = model.Title,
                    Description = model.Description,
                    Category = model.Category,
                    PreparationTime = model.PreparationTime,
                    Ingredients = model.Ingredients,
                    Instructions = model.Instructions,
                    Calories = model.Calories,
                    Protein = model.Protein,
                    Carbohydrates = model.Carbohydrates,
                    Fat = model.Fat,
                    CreatedAt = DateTime.Now
                };

                if (model.Image != null)
                {
                    var uploads = Path.Combine(_environment.WebRootPath, "uploads");
                    Directory.CreateDirectory(uploads);
                    var fileName = Guid.NewGuid() + Path.GetExtension(model.Image.FileName);
                    var filePath = Path.Combine(uploads, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.Image.CopyToAsync(stream);
                    }
                    recipe.ImageUrl = "/Uploads/" + fileName;
                }

                _context.Recipes.Add(recipe);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public IActionResult Edit(int id)
        {
            var recipe = _context.Recipes.Find(id);
            if (recipe == null)
            {
                return NotFound();
            }

            var viewModel = new RecipeViewModel
            {
                Id = recipe.Id,
                Title = recipe.Title,
                Description = recipe.Description,
                Category = recipe.Category,
                PreparationTime = recipe.PreparationTime,
                Ingredients = recipe.Ingredients,
                Instructions = recipe.Instructions,
                Calories = recipe.Calories,
                Protein = recipe.Protein,
                Carbohydrates = recipe.Carbohydrates,
                Fat = recipe.Fat,
                ImageUrl = recipe.ImageUrl
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RecipeViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var recipe = await _context.Recipes.FindAsync(id);
                if (recipe == null)
                {
                    return NotFound();
                }

                if (model.Image != null)
                {
                    // Delete existing image if it exists
                    if (!string.IsNullOrEmpty(recipe.ImageUrl))
                    {
                        var existingFilePath = Path.Combine(_environment.WebRootPath, recipe.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(existingFilePath))
                        {
                            System.IO.File.Delete(existingFilePath);
                        }
                    }

                    var uploads = Path.Combine(_environment.WebRootPath, "uploads");
                    Directory.CreateDirectory(uploads);
                    var fileName = Guid.NewGuid() + Path.GetExtension(model.Image.FileName);
                    var filePath = Path.Combine(uploads, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.Image.CopyToAsync(stream);
                    }
                    recipe.ImageUrl = "/Uploads/" + fileName;
                }

                recipe.Title = model.Title;
                recipe.Description = model.Description;
                recipe.Category = model.Category;
                recipe.PreparationTime = model.PreparationTime;
                recipe.Ingredients = model.Ingredients;
                recipe.Instructions = model.Instructions;
                recipe.Calories = model.Calories;
                recipe.Protein = model.Protein;
                recipe.Carbohydrates = model.Carbohydrates;
                recipe.Fat = model.Fat;
                recipe.UpdatedAt = DateTime.Now;

                _context.Update(recipe);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var recipe = _context.Recipes.Find(id);
            if (recipe == null)
            {
                return NotFound();
            }

            // Delete associated image if it exists
            if (!string.IsNullOrEmpty(recipe.ImageUrl))
            {
                var filePath = Path.Combine(_environment.WebRootPath, recipe.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            _context.Recipes.Remove(recipe);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }


    }
}