// Jack Gerber - A01266976
// Date: Nov 05, 2025
 
using Microsoft.AspNetCore.Mvc;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;
using BlogApp.Data;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace BlogApp.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET action to display the index page with filtered articles based on date range
    public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate)
    {
        // Get the current logged-in user, if authenticated
        var user = User.Identity.IsAuthenticated ? await _userManager.GetUserAsync(User) : null;

        // Check if the user is an Admin
        bool isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

        // Set the IsAdmin flag in the ViewBag
        ViewBag.IsAdmin = isAdmin;

        // Start with a query to get all articles
        var articlesQuery = _context.Articles.AsQueryable();

        // Filter by date posted instead of event dates
        if (startDate.HasValue && endDate.HasValue)
        {
            articlesQuery = articlesQuery.Where(a => a.DatePosted >= startDate && a.DatePosted <= endDate);
        }
        else if (startDate.HasValue)
        {
            articlesQuery = articlesQuery.Where(a => a.DatePosted >= startDate);
        }
        else if (endDate.HasValue)
        {
            articlesQuery = articlesQuery.Where(a => a.DatePosted <= endDate);
        }

        // Execute the query and get the filtered list of articles
        var articles = await articlesQuery.OrderByDescending(a => a.DatePosted).ToListAsync();

        // Create a model with the user and the list of articles
        var model = new Tuple<ApplicationUser, List<Article>>(user, articles);

        return View(model); // Return the view with the model
    }
}

