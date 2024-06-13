﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Boulderlog.Data.Models
{
    [Table("Franchise")]
    public class Franchise
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int? Id { get; set; }

        [MaxLength(255)]
        public required string Name { get; set; }

        public ICollection<Gym>? Gyms { get; set; }

        public ICollection<Grade>? Grades { get; set; }
    }
}
