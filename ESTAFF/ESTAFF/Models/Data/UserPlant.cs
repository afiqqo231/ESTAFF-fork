using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESTAFF.Models.Data
{
    // Which plants a user may act on. Read-only projection of CLIP.UserPlants —
    // see ClipDbContext.
    public class UserPlant
    {
        public int Id { get; set; }

        // CLIP.AspNetUsers.Id. Left as a plain string rather than a navigation
        // property: ApplicationUser is mapped by ApplicationDbContext, and
        // pulling it into the read-only CLIP context would drag ESTAFF's whole
        // Identity model in with it.
        [Required]
        public string UserId { get; set; }

        [Required]
        public int PlantId { get; set; }

        [ForeignKey("PlantId")]
        public virtual Plant Plant { get; set; }
    }
}
