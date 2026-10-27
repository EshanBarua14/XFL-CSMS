using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using XFLCSMS.Models.Issue;

namespace XFLCSMS.Models.Product
{
    /// <summary>An XFL product a ticket is raised for (OST, Smart Trade, ...). Managed by the admin under Setup > Products.</summary>
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Please enter the product name.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "The name must be 2 to 100 characters long.")]
        [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9 .&()/+,_-]*$", ErrorMessage = "Letters, digits, spaces and . , & ( ) / + - _ only.")]
        [Display(Name = "Product name")]
        public string ProductName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>Inactive products are kept on their tickets but are no longer offered for new ones.</summary>
        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Display order")]
        [Range(0, 9999)]
        public int SortOrder { get; set; }

        public ICollection<IssueTable>? Issues { get; set; }
    }
}
