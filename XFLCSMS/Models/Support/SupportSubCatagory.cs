using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using XFLCSMS.Models.Issue;

namespace XFLCSMS.Models.Support
{
    public class SupportSubCatagory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SupportSubCatagoryId { get; set; }
        [Required]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "The name must be 2 to 100 characters long.")]
        [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9 .&()/+,_-]*$", ErrorMessage = "Letters, digits, spaces and . , & ( ) / + - _ only.")]
        [Display(Name = "Sub-category")]
        public string SubCatagory { get; set; } = string.Empty;
        public ICollection<IssueTable> issue { get; set; }
    }
}
