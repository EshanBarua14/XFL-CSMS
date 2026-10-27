using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using XFLCSMS.Models.Brocarage;

namespace XFLCSMS.Models.Branch
{
    public class BranchView
    {


        public int BranchId { get; set; }

        [Required(ErrorMessage = "Please enter the branch name.")]
        [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9 .&()/+,_-]*$", ErrorMessage = "Letters, digits, spaces and . , & ( ) / + - _ only.")]
        [Display(Name = "Branch name")]
        public string BranchName { get; set; } = string.Empty;


        public string BrokerageHouseName { get; set; } = string.Empty;

        [Display(Name = "Brokerage house")]
        public int BrokerageId { get; set;} 

        public List<Brokerage>? brocarage { get; set; }



    }
}
