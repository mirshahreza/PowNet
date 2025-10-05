using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PowNet.Models
{
    /// <summary>
    /// Person domain model mapped to underlying database table (columns per provided schema).
    /// </summary>
    [Table("Person")] // Adjust if actual table name differs
    public class Person
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CreatedBy { get; set; }

        [Required]
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public int? UpdatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }

        public int? UserId { get; set; }

        [Required]
        public int GenderId { get; set; }

        [MaxLength(16)]
        public string? NationalCode { get; set; }

        [Required, MaxLength(64)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(64)]
        public string LastName { get; set; } = string.Empty;

        public int? BirthYear { get; set; }
        public byte? BirthMonth { get; set; }
        public byte? BirthDay { get; set; }

        [Required, MaxLength(14)]
        public string Mobile { get; set; } = string.Empty;

        [Column("Picture_FileBody")]
        public byte[]? PictureFileBody { get; set; }

        [Column("Picture_FileBody_xs")]
        public byte[]? PictureFileBodyXs { get; set; }
    }
}
