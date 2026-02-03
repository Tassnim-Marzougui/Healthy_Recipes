using Microsoft.AspNetCore.Mvc;
using Healthy_Recipes.Models;
using Healthy_Recipes.Data;
using Healthy_Recipes.ViewModels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.IO;
using System;
using Microsoft.EntityFrameworkCore;

namespace Healthy_Recipes.Controllers
{
    [Authorize]
    public class RecipesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public RecipesController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: Recipes
        public async Task<IActionResult> Index(string searchTerm = "", string category = "")
        {
            var query = _context.Recipes.AsQueryable();

            // Filtrer par terme de recherche
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(r => 
                    r.Title.ToLower().Contains(searchTerm) ||
                    r.Description.ToLower().Contains(searchTerm) ||
                    r.Ingredients.ToLower().Contains(searchTerm) ||
                    r.Instructions.ToLower().Contains(searchTerm));
            }

            // Filtrer par catégorie
            if (!string.IsNullOrEmpty(category) && category != "Toutes les catégories")
            {
                query = query.Where(r => r.Category == category);
            }

            // Récupérer les données
            var recipes = await query.ToListAsync();

            // Tri par pertinence en mémoire
            var sortedRecipes = recipes.OrderByDescending(r => 
                r.Title.ToLower() == searchTerm || 
                r.Description.ToLower() == searchTerm ||
                r.Ingredients.ToLower() == searchTerm ||
                r.Instructions.ToLower() == searchTerm)
                .ThenBy(r => r.Title)
                .ToList();

            var viewModels = sortedRecipes.Select(r => new RecipeViewModel
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

            var searchViewModel = new RecipeSearchViewModel
            {
                SearchTerm = searchTerm,
                Category = category,
                Recipes = viewModels
            };

            return View(searchViewModel);
        }

        // GET: Recipes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recipe = await _context.Recipes
                .FirstOrDefaultAsync(m => m.Id == id);
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

        // GET: Recipes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Recipes/Create
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

                _context.Add(recipe);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: Recipes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recipe = await _context.Recipes.FindAsync(id);
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

        // POST: Recipes/Edit/5
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
                var existingRecipe = await _context.Recipes.FindAsync(id);
                if (existingRecipe == null)
                {
                    return NotFound();
                }

                // Delete existing image if it exists
                if (!string.IsNullOrEmpty(existingRecipe.ImageUrl))
                {
                    var existingFilePath = Path.Combine(_environment.WebRootPath, existingRecipe.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(existingFilePath))
                    {
                        System.IO.File.Delete(existingFilePath);
                    }
                }

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
                    existingRecipe.ImageUrl = "/Uploads/" + fileName;
                }

                existingRecipe.Title = model.Title;
                existingRecipe.Description = model.Description;
                existingRecipe.Category = model.Category;
                existingRecipe.PreparationTime = model.PreparationTime;
                existingRecipe.Ingredients = model.Ingredients;
                existingRecipe.Instructions = model.Instructions;
                existingRecipe.Calories = model.Calories;
                existingRecipe.Protein = model.Protein;
                existingRecipe.Carbohydrates = model.Carbohydrates;
                existingRecipe.Fat = model.Fat;
                existingRecipe.UpdatedAt = DateTime.Now;

                try
                {
                    _context.Update(existingRecipe);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Recipes.AnyAsync(e => e.Id == model.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            var viewModel = new RecipeViewModel
            {
                Id = model.Id,
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
                ImageUrl = model.ImageUrl
            };
            return View(viewModel);
        }

        // GET: Recipes/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recipe = await _context.Recipes.FindAsync(id);
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

        // POST: Recipes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe != null)
            {
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
            return NotFound();
        }
    }
}