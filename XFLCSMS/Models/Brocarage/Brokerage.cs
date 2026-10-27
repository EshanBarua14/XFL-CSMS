using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using XFLCSMS.Models.Branch;
using XFLCSMS.Models.Issue;

namespace XFLCSMS.Models.Brocarage
{
    public class Brokerage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BrokerageId { get; set; }

        [Required(ErrorMessage = "Please enter the brokerage house name.")]
        [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9 .&()/+,_-]*$", ErrorMessage = "Letters, digits, spaces and . , & ( ) / + - _ only.")]
        [Display(Name = "Brokerage house name")]
        public string BrokerageHouseName { get; set; } = string.Empty;
        [Required(ErrorMessage = "Please enter the acronym.")]
        [RegularExpression(@"^[A-Za-z0-9.&-]+$", ErrorMessage = "Letters, digits and . & - only, no spaces.")]
        [Display(Name = "Acronym")]
        public string BrokerageHouseAcronym { get; set;} = string.Empty;

        public ICollection<Branchh> branches { get; set; }
        public ICollection<IssueTable> Issues { get; set; }


    }
}
