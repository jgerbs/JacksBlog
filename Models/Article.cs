// Jack Gerber - A01266976
// Date: Nov 06, 2025

using System;
using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models
{
    public class Article
    {
        public int ArticleId { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(300)]
        [Display(Name = "Short Description")]
        public string ShortDescription { get; set; } = string.Empty;

        [Display(Name = "Cover Image URL")]
        public string? CoverImageUrl { get; set; }

        [Required]
        [Display(Name = "Article Content")]
        public string ContentHtml { get; set; } = string.Empty;

        public DateTime DatePosted { get; set; } = DateTime.UtcNow;

        [Required]
        public string ContributorUsername { get; set; } = string.Empty;
    }
}
