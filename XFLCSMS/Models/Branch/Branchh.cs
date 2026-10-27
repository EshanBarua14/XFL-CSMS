using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using XFLCSMS.Models.Brocarage;
using XFLCSMS.Models.Issue;

namespace XFLCSMS.Models.Branch
{
    public class Branchh
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "BrokerageHouseName is required")]
        [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9 .&()/+,_-]*$", ErrorMessage = "Letters, digits, spaces and . , & ( ) / + - _ only.")]
        public string BranchName { get; set; }=string.Empty;
      
        [ForeignKey("Brokerage")]
        public int? BrokerageId { get; set; }
        public Brokerage? Brokerage { get; set; }

        public ICollection<IssueTable> Issues { get; set; }

    }
} 
