using XFLCSMS.Models.Affected;
using XFLCSMS.Models.Branch;
using XFLCSMS.Models.Brocarage;
using XFLCSMS.Models.Support;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using XFLCSMS.Models.Register;

namespace XFLCSMS.Models.Issue
{
    public class IssueTable
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IssueId { get; set; }

        [Required]
        [ForeignKey("Users")]
        public int UserId { get; set; }
        public User Users { get; set; }
        [Required]
        [ForeignKey("Brokerages")]
        public int BrokerageId { get; set; }
        public Brokerage Brokerages { get; set; }

        public DateTime TDate { get; set; }
        public string TNumber { get; set; } = string.Empty;

        public DateTime? AssignOn { get; set; }
        public string? AssignBy { get; set; } = string.Empty;

        /// <summary>Assigned engineer as a user id (AssignBy holds his name, which breaks when he is renamed).</summary>
        public int? AssignedToUserId { get; set; }

        /// <summary>First time XFL staff acted on the ticket: moved it out of Open or answered in Comments.</summary>
        public DateTime? FirstResponseOn { get; set; }
        public DateTime? ApproveOn { get; set; }
        public string? ApproveBy { get; set; } = string.Empty;

        public DateTime? UpdatedOn { get; set; }

        public string? UpdatedBy { get; set; }= string.Empty;

        [ForeignKey("supportTypes")]
        public int? SupportTypeId { get; set; }
        public SupportType? supportTypes { get; set; }
       
        [ForeignKey("supportCatagorys")]
        public int? SupportCatagoryId { get; set; }
        public SupportCatagory? supportCatagorys { get; set; }
        
        [ForeignKey("SupportSubCatagorys")]
       public int? SupportSubCatagoryId { get; set; }
        public SupportSubCatagory? SupportSubCatagorys { get; set; }
        //[ForeignKey("AffectedSectionId")]
        
        [ForeignKey("affecteds")]
        public int? AffectedSectionId { get; set; }
        public AffectedSection? affecteds { get; set; }
        

        [ForeignKey("Product")]
        public int? ProductId { get; set; }
        public XFLCSMS.Models.Product.Product? Product { get; set; }

        public string Priority { get; set; }=string.Empty;
        public string? ITitle { get; set; } = string.Empty;
        public string? Details { get; set; } 

        public string? Comments { get; set; } 

        public ICollection<Attachment>? attachment { get; set; }

        public string? IStatus { get; set; } = string.Empty;

        public DateTime? ClosedOn { get; set; }
        public string? ClosedBy { get; set; }

        /// <summary>Support level the ticket sits at, 1 to 5 (L1 Service Desk ... L5 Architecture &amp; Vendors). New tickets start at L1.</summary>
        public int SupportLevel { get; set; } = 1;

        /// <summary>When the ticket reached its current level (null = since it was raised).</summary>
        public DateTime? LevelSince { get; set; }

        /// <summary>Development stage when the ticket needs a code change (see DevStages); null = no development.</summary>
        [MaxLength(30)]
        public string? DevStage { get; set; }
        public DateTime? DevStageSince { get; set; }
        public int? DevOwnerUserId { get; set; }
        public DateTime? DevEta { get; set; }
        [MaxLength(200)]
        public string? DevReference { get; set; }

      

    }
}
