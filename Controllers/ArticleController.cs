// Jack Gerber - A01266976
// Date: Nov 05, 2025

using BlogApp.Models;
using BlogApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public class ArticleController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;

    public ArticleController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
    {
        _context = context;
        _userManager = userManager;
        _env = env;
    }

    // GET action for creating a new article
    [Authorize(Roles = "Contributor,Admin")]
    public async Task<IActionResult> Create()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(); // Return unauthorized if user not found
        }

        // Pre-populate a new Article model with the logged-in user's username
        var article = new Article
        {
            ContributorUsername = user.UserName,
        };

        return View(article); // Return the view with the new article model
    }

    // POST action for creating a new article
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Contributor,Admin")]
    public async Task<IActionResult> Create(Article article)
    {
        ModelState.Remove(nameof(article.ContributorUsername)); // Remove ContributorUsername error

        if (!ModelState.IsValid)
        {
            return View(article); // Return view with errors if model is invalid
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null || string.IsNullOrEmpty(user.UserName))
        {
            ModelState.AddModelError("", "User information is not available. Please log in again.");
            return View(article); // Return view with error if user is not found
        }

        article.ContributorUsername = user.UserName; // Set ContributorUsername to the logged-in user's username
        article.DatePosted = DateTime.UtcNow;

        _context.Articles.Add(article);
        await _context.SaveChangesAsync(); // Save the article to the database

        return RedirectToAction("Index", "Home"); // Redirect to the home page after saving
    }

    // GET action to edit an existing article
    [Authorize(Roles = "Contributor,Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound(); // Return NotFound if id is null

        var article = await _context.Articles.FindAsync(id);
        if (article == null) return NotFound(); // Return NotFound if article not found

        if (!User.IsInRole("Admin") && article.ContributorUsername != User.Identity.Name)
        {
            return Forbid(); // Contributors can only edit their own articles
        }

        return View(article); // Return the edit view with the article
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Article updatedArticle, IFormFile? ImageFile)
    {
        if (id != updatedArticle.ArticleId)
            return NotFound();

        var article = await _context.Articles.FindAsync(id);
        if (article == null)
            return NotFound();

        // Update title + content
        article.Title = updatedArticle.Title;
        article.ContentHtml = updatedArticle.ContentHtml;
        article.CoverImageUrl = updatedArticle.CoverImageUrl; // default (URL)

        // Handle file upload (if provided)
        if (ImageFile != null)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + ImageFile.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await ImageFile.CopyToAsync(fileStream);
            }

            // Use local uploaded image
            article.CoverImageUrl = "/uploads/" + uniqueFileName;
        }

        await _context.SaveChangesAsync();

        // Redirect to HOME page (which uses the Tuple model)
        return RedirectToAction("Index", "Home");
    }



    // POST action to delete an article (confirm deletion)
    [HttpPost, ActionName("DeleteConfirmed")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Contributor,Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var article = await _context.Articles.FindAsync(id);
        if (article == null) return NotFound(); // Return NotFound if article doesn't exist

        if (!User.IsInRole("Admin") && article.ContributorUsername != User.Identity.Name)
        {
            return Forbid(); // Restrict deletion to the owner or an admin
        }

        _context.Articles.Remove(article);
        await _context.SaveChangesAsync(); // Delete the article from the database

        return RedirectToAction("Index", "Home"); // Redirect to the home page after deletion
    }

    // GET action to view all articles
    public async Task<IActionResult> Index()
    {
        var articles = await _context.Articles
            .Where(a => a.DatePosted <= DateTime.UtcNow)
            .ToListAsync(); // Get list of articles that are currently active

        var user = await _userManager.GetUserAsync(User); // Get the current logged-in user

        // Attach author names to articles
        foreach (var article in articles)
        {
            var author = await _userManager.FindByNameAsync(article.ContributorUsername);
            article.ContributorUsername = author != null ? $"{author.FirstName} {author.LastName}" : "Unknown Author";
        }

        return View(Tuple.Create(user, articles)); // Return the view with the articles and user data
    }

    // GET action to view the details of a specific article
    public async Task<IActionResult> Details(int id)
    {
        var article = await _context.Articles.FirstOrDefaultAsync(m => m.ArticleId == id);
        if (article == null) return NotFound(); // Return NotFound if article doesn't exist

        return View(article); // Return the details view with the article data
    }

    // Helper method to check if an article exists by ID
    private bool ArticleExists(int id)
    {
        return _context.Articles.Any(e => e.ArticleId == id);
    }
}
